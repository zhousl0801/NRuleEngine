using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.Rules;
using NRuleEngine.Domain.Scenarios;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities.Auditing;

namespace NRuleEngine.Domain.RuleModels;

/// <summary>
/// 规则模型的一次修订；草稿变量归属该修订。
/// </summary>
public sealed class RuleModelRevision : AuditedAggregateRoot<Guid>
{
    /// <summary>本修订所属规则模型的标识。</summary>
    public Guid RuleModelId { get; private set; }
    /// <summary>同一模型内递增的修订序号。</summary>
    public int RevisionNo { get; private set; }
    /// <summary>复制草稿所依据的源修订；首个草稿为空。</summary>
    public Guid? BasedOnRevisionId { get; private set; }
    /// <summary>修订状态；只有草稿允许修改内容。</summary>
    public RuleRevisionStatus Status { get; private set; }
    /// <summary>修订锁定的场景编码。</summary>
    public string ScenarioCode { get; private set; } = null!;
    /// <summary>修订锁定的场景契约版本。</summary>
    public string ContractVersion { get; private set; } = null!;
    /// <summary>FirstMatch 或 Sequential 规则执行方式。</summary>
    public RuleExecutionMode ExecutionMode { get; private set; }
    /// <summary>Sequential 模式下同一变量被多次写入的裁决策略。</summary>
    public MultipleWritePolicy MultipleWritePolicy { get; private set; }
    /// <summary>发布后固定的内容摘要；草稿阶段可以为空。</summary>
    public string? ContentHash { get; private set; }
    /// <summary>维护者填写的本次修订说明。</summary>
    public string? ChangeSummary { get; private set; }
    private List<VariableDefinition> _variables = [];
    /// <summary>修订中的契约变量和中间变量。</summary>
    public IReadOnlyCollection<VariableDefinition> Variables => _variables.AsReadOnly();
    private List<RuleDefinition> _rules = [];
    /// <summary>修订中的规则及其赋值。</summary>
    public IReadOnlyCollection<RuleDefinition> Rules => _rules.AsReadOnly();
    private List<ScenarioBinding> _bindings = [];
    /// <summary>修订中的场景绑定。</summary>
    public IReadOnlyCollection<ScenarioBinding> Bindings => _bindings.AsReadOnly();
    private List<ReleaseGateCase> _gateCases = [];
    /// <summary>发布时必须通过的回归用例。</summary>
    public IReadOnlyCollection<ReleaseGateCase> GateCases => _gateCases.AsReadOnly();

    private RuleModelRevision()
    {
    }

    /// <summary>按固定场景契约创建草稿，并实例化契约入口与必需出口变量。</summary>
    public RuleModelRevision(Guid id, Guid modelId, int revisionNo, RuleScenarioDescriptor contract, Guid? basedOnRevisionId = null)
        : base(id)
    {
        if (revisionNo < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNo));
        }
        RuleModelId = modelId;
        RevisionNo = revisionNo;
        BasedOnRevisionId = basedOnRevisionId;
        Status = RuleRevisionStatus.Draft;
        ScenarioCode = contract.ScenarioCode;
        ContractVersion = contract.ContractVersion;
        ExecutionMode = RuleExecutionMode.FirstMatch;
        MultipleWritePolicy = MultipleWritePolicy.ErrorOnMultipleWrite;
        foreach (var variable in contract.Inputs.Concat(contract.RequiredOutputs))
        {
            _variables.Add(VariableDefinition.FromContract(Guid.NewGuid(), id, variable));
        }
    }

    /// <summary>复制修订内容到新草稿；实体 ID 更新，变量 StableKey 保留。</summary>
    public static RuleModelRevision CopyDraft(Guid id, Guid modelId, int revisionNo, RuleModelRevision source)
    {
        // 新草稿保留跨修订稳定标识，但每个变量获得新的实体标识。
        var copy = new RuleModelRevision
        {
            Id = id,
            RuleModelId = modelId,
            RevisionNo = revisionNo,
            BasedOnRevisionId = source.Id,
            Status = RuleRevisionStatus.Draft,
            ScenarioCode = source.ScenarioCode,
            ContractVersion = source.ContractVersion,
            ExecutionMode = source.ExecutionMode,
            MultipleWritePolicy = source.MultipleWritePolicy,
            _variables = source.Variables.Select(v => v.CopyTo(id)).ToList()
        };
        copy._rules = source.Rules.Select(r => r.CopyTo(id, copy.Variables)).ToList();
        copy._bindings = source.Bindings.Select(b => b.CopyTo(id)).ToList();
        copy._gateCases = source.GateCases.Select(c => c.CopyTo(id)).ToList();
        return copy;
    }

    /// <summary>新增规则，并阻止相同优先级下的顺序值冲突。</summary>
    public RuleDefinition AddRule(Guid id, string name, int priority, int sortOrder, bool isEnabled,
        string? conditionSource, string conditionAstJson)
    {
        EnsureDraft();
        if (_rules.Any(r => r.Priority == priority && r.SortOrder == sortOrder))
        {
            throw new ArgumentException("Rule priority and sort order must be unique together.");
        }
        var rule = new RuleDefinition(id, Id, name, priority, sortOrder, isEnabled, conditionSource,
            conditionAstJson, Variables);
        _rules.Add(rule);
        return rule;
    }

    /// <summary>设置草稿的命中模式和多次写入裁决策略。</summary>
    public void SetExecutionSemantics(RuleExecutionMode mode, MultipleWritePolicy writePolicy)
    {
        EnsureDraft();
        if (!Enum.IsDefined(mode) || !Enum.IsDefined(writePolicy))
        {
            throw new ArgumentException("Unsupported execution semantics.");
        }
        ExecutionMode = mode;
        MultipleWritePolicy = writePolicy;
    }

    /// <summary>给本修订的指定规则增加赋值，并校验目标和值类型。</summary>
    public RuleAssignment AddAssignment(Guid ruleId, Guid assignmentId, Guid targetKey,
        RuleValueSourceKind sourceKind, string? constantJson, string? expressionSource,
        string? expressionAstJson, int sortOrder)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("Rule does not belong to this revision.");
        var assignment = new RuleAssignment(assignmentId, ruleId, targetKey, sourceKind, constantJson,
            expressionSource, expressionAstJson, sortOrder, Variables);
        rule.AddAssignment(assignment);
        return assignment;
    }

    /// <summary>按本修订锁定的场景契约加入一组绑定条件。</summary>
    public ScenarioBinding AddBinding(Guid id, RuleScenarioDescriptor contract, int priority,
        IReadOnlyCollection<(string Code, RuleBindingOperator Operator, string ValueJson)> criteria)
    {
        EnsureDraft();
        if (contract.ScenarioCode != ScenarioCode || contract.ContractVersion != ContractVersion)
        {
            throw new ArgumentException("Binding contract differs from revision contract.");
        }
        var binding = new ScenarioBinding(id, Id, contract, priority);
        foreach (var criterion in criteria)
        {
            binding.AddCriterion(Guid.NewGuid(), contract, criterion.Code, criterion.Operator, criterion.ValueJson);
        }
        _bindings.Add(binding);
        return binding;
    }

    /// <summary>从草稿移除规则；其赋值随规则一并移除。</summary>
    public void RemoveRule(Guid ruleId)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("Rule does not belong to this revision.");
        _rules.Remove(rule);
    }

    /// <summary>更新规则条件和顺序，不更换规则标识。</summary>
    public void UpdateRule(Guid ruleId, string name, int priority, int sortOrder, bool isEnabled,
        string? conditionSource, string conditionAstJson)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("Rule does not belong to this revision.");
        if (_rules.Any(r => r.Id != ruleId && r.Priority == priority && r.SortOrder == sortOrder))
        {
            throw new ArgumentException("Rule priority and sort order must be unique together.");
        }
        rule.Update(name, priority, sortOrder, isEnabled, conditionSource, conditionAstJson, Variables);
    }

    /// <summary>只删除本修订指定规则下的赋值。</summary>
    public void RemoveAssignment(Guid ruleId, Guid assignmentId)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("Rule does not belong to this revision.");
        rule.RemoveAssignment(assignmentId);
    }

    /// <summary>更新指定赋值的目标、来源和值，并保持赋值标识。</summary>
    public void UpdateAssignment(Guid ruleId, Guid assignmentId, Guid targetKey,
        RuleValueSourceKind sourceKind, string? constantJson, string? expressionSource,
        string? expressionAstJson, int sortOrder)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("Rule does not belong to this revision.");
        rule.UpdateAssignment(assignmentId, targetKey, sourceKind, constantJson, expressionSource,
            expressionAstJson, sortOrder, Variables);
    }

    /// <summary>从草稿移除指定场景绑定。</summary>
    public void RemoveBinding(Guid bindingId)
    {
        EnsureDraft();
        var binding = _bindings.SingleOrDefault(b => b.Id == bindingId)
            ?? throw new ArgumentException("Binding does not belong to this revision.");
        _bindings.Remove(binding);
    }

    /// <summary>按锁定契约替换指定绑定的优先级和全部条件。</summary>
    public void UpdateBinding(Guid bindingId, RuleScenarioDescriptor contract, int priority,
        IReadOnlyCollection<(string Code, RuleBindingOperator Operator, string ValueJson)> criteria)
    {
        EnsureDraft();
        var binding = _bindings.SingleOrDefault(b => b.Id == bindingId)
            ?? throw new ArgumentException("Binding does not belong to this revision.");
        if (contract.ScenarioCode != ScenarioCode || contract.ContractVersion != ContractVersion)
        {
            throw new ArgumentException("Binding contract differs from revision contract.");
        }
        binding.Update(contract, priority, criteria);
    }

    /// <summary>在草稿中创建只能内部使用的中间变量，并检查编码冲突。</summary>
    public VariableDefinition AddIntermediate(Guid id, string code, string name, RuleTypeDescriptor type)
    {
        EnsureDraft();
        RuleCatalog.ValidateType(type);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable code and name are required.");
        }
        code = code.Trim();
        name = name.Trim();
        if (Variables.Any(v => v.Code == code))
        {
            throw new InvalidOperationException($"Duplicate variable code: {code}.");
        }
        var variable = VariableDefinition.NewIntermediate(id, Id, Guid.NewGuid(), code, name, type);
        _variables.Add(variable);
        return variable;
    }

    /// <summary>按 StableKey 重命名中间变量；契约变量不可改名。</summary>
    public void RenameIntermediate(Guid stableKey, string code, string name)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable code and name are required.");
        }
        code = code.Trim();
        name = name.Trim();
        var variable = Variables.Single(v => v.StableKey == stableKey);
        if (Variables.Any(v => v != variable && v.Code == code))
        {
            throw new InvalidOperationException($"Duplicate variable code: {code}.");
        }
        variable.Rename(code, name);
    }

    /// <summary>将尚未发布的草稿标记为放弃。</summary>
    public void Abandon()
    {
        EnsureDraft();
        Status = RuleRevisionStatus.Abandoned;
    }

    /// <summary>增加以场景上下文、输入和预期输出组成的发布门禁用例。</summary>
    public ReleaseGateCase AddGateCase(Guid id, string name, string contextJson, string inputsJson,
        string expectedOutputsJson)
    {
        EnsureDraft();
        if (_gateCases.Any(c => c.Name == name?.Trim()))
        {
            throw new ArgumentException("Gate case name must be unique in a revision.");
        }
        var gateCase = new ReleaseGateCase(id, Id, name, contextJson, inputsJson, expectedOutputsJson);
        _gateCases.Add(gateCase);
        return gateCase;
    }

    /// <summary>删除草稿中的指定发布门禁用例。</summary>
    public void RemoveGateCase(Guid gateCaseId)
    {
        EnsureDraft();
        var gateCase = _gateCases.SingleOrDefault(c => c.Id == gateCaseId)
            ?? throw new ArgumentException("Gate case does not belong to this revision.");
        _gateCases.Remove(gateCase);
    }

    /// <summary>通过门禁后冻结本修订及其内容哈希。</summary>
    public void Publish(string contentHash, string? changeSummary)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("Content hash is required.", nameof(contentHash));
        }
        if (changeSummary?.Length > 2048)
        {
            throw new ArgumentException("Change summary exceeds 2048 characters.", nameof(changeSummary));
        }
        ContentHash = contentHash;
        ChangeSummary = changeSummary;
        Status = RuleRevisionStatus.Published;
    }

    /// <summary>守住修订不可变边界，阻止对非草稿内容的写入。</summary>
    private void EnsureDraft()
    {
        if (Status != RuleRevisionStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft can be edited.");
        }
    }
}
