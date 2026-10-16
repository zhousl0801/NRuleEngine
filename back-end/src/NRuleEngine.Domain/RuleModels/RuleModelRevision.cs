using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.Rules;
using NRuleEngine.Domain.Scenarios;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared.Runtime;
using Volo.Abp;
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

    /// <summary>创建草稿时锁定的场景结果基数。</summary>
    public RuleResultCardinality ResultCardinality { get; private set; }

    /// <summary>与结果基数相容的规则执行方式。</summary>
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

    /// <summary>按稳定键获取修订内变量；不存在时返回稳定业务错误码。</summary>
    public VariableDefinition GetVariable(Guid stableKey)
    {
        return _variables.SingleOrDefault(v => v.StableKey == stableKey)
            ?? throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.VariableNotFound);
    }
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
    public RuleModelRevision(
        Guid id,
        Guid modelId,
        int revisionNo,
        RuleScenarioDescriptor contract,
        Guid? basedOnRevisionId = null) : base(id)
    {
        if (revisionNo < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNo), "修订号必须大于零。");
        }

        RuleModelId = modelId;
        RevisionNo = revisionNo;
        BasedOnRevisionId = basedOnRevisionId;
        Status = RuleRevisionStatus.Draft;
        ScenarioCode = contract.ScenarioCode;
        ContractVersion = contract.ContractVersion;
        ResultCardinality = contract.ResultCardinality;
        ExecutionMode = ResultCardinality == RuleResultCardinality.Many ? RuleExecutionMode.CollectMatches : RuleExecutionMode.FirstMatch;
        MultipleWritePolicy = MultipleWritePolicy.ErrorOnMultipleWrite;
        foreach (var variable in contract.Inputs.Concat(contract.RequiredOutputs))
        {
            _variables.Add(VariableDefinition.FromContract(Guid.NewGuid(), id, variable));
        }
    }

    /// <summary>复制修订内容到新草稿；实体 ID 更新，变量 StableKey 保留。</summary>
    /// <remarks>核心逻辑：重建规则、绑定和门禁实体并保留变量稳定键；调用时机：从历史或活动修订创建草稿时。</remarks>
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
            ResultCardinality = source.ResultCardinality,
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
    /// <remarks>核心逻辑：检查草稿状态与排序组合后加入规则；调用时机：编制规则草稿时。</remarks>
    public RuleDefinition AddRule(Guid id, string name, int priority, int sortOrder, bool isEnabled, string? conditionSource, string conditionAstJson)
    {
        EnsureDraft();
        if (_rules.Any(r => r.Priority == priority && r.SortOrder == sortOrder))
        {
            throw new ArgumentException("规则的优先级与排序值组合不能重复。");
        }

        var rule = new RuleDefinition(id, Id, name, priority, sortOrder, isEnabled, conditionSource, conditionAstJson, Variables);
        _rules.Add(rule);
        return rule;
    }

    /// <summary>设置草稿的命中模式和多次写入裁决策略。</summary>
    public void SetExecutionSemantics(RuleExecutionMode mode, MultipleWritePolicy writePolicy)
    {
        EnsureDraft();
        if (!Enum.IsDefined(mode) || !Enum.IsDefined(writePolicy))
        {
            throw new ArgumentException("不支持的执行语义。");
        }

        if ((ResultCardinality == RuleResultCardinality.Many) != (mode == RuleExecutionMode.CollectMatches) ||
            mode == RuleExecutionMode.CollectMatches && writePolicy != MultipleWritePolicy.ErrorOnMultipleWrite)
        {
            throw new ArgumentException("执行方式必须与场景结果基数一致，多行模式不使用跨规则写入策略。");
        }

        ExecutionMode = mode;
        MultipleWritePolicy = writePolicy;
    }

    /// <summary>给本修订的指定规则增加赋值，并校验目标和值类型。</summary>
    public RuleAssignment AddAssignment(Guid ruleId, Guid assignmentId, Guid targetKey, RuleValueSourceKind sourceKind, string? constantJson, string? expressionSource, string? expressionAstJson, int sortOrder)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("规则不属于此修订。");
        var assignment = new RuleAssignment(assignmentId, ruleId, targetKey, sourceKind, constantJson, expressionSource, expressionAstJson, sortOrder, Variables);
        rule.AddAssignment(assignment);
        return assignment;
    }

    /// <summary>按本修订锁定的场景契约加入一组绑定条件。</summary>
    public ScenarioBinding AddBinding(Guid id, RuleScenarioDescriptor contract, int priority, IReadOnlyCollection<(string Code, RuleBindingOperator Operator, string ValueJson)> criteria)
    {
        EnsureDraft();
        if (contract.ScenarioCode != ScenarioCode || contract.ContractVersion != ContractVersion)
        {
            throw new ArgumentException("绑定契约与修订契约不一致。");
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
            ?? throw new ArgumentException("规则不属于此修订。");
        _rules.Remove(rule);
    }

    /// <summary>更新规则条件和顺序，不更换规则标识。</summary>
    public void UpdateRule(Guid ruleId, string name, int priority, int sortOrder, bool isEnabled, string? conditionSource, string conditionAstJson)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("规则不属于此修订。");
        if (_rules.Any(r => r.Id != ruleId && r.Priority == priority && r.SortOrder == sortOrder))
        {
            throw new ArgumentException("规则的优先级与排序值组合不能重复。");
        }

        rule.Update(name, priority, sortOrder, isEnabled, conditionSource, conditionAstJson, Variables);
    }

    /// <summary>只删除本修订指定规则下的赋值。</summary>
    public void RemoveAssignment(Guid ruleId, Guid assignmentId)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("规则不属于此修订。");
        rule.RemoveAssignment(assignmentId);
    }

    /// <summary>更新指定赋值的目标、来源和值，并保持赋值标识。</summary>
    public void UpdateAssignment(Guid ruleId, Guid assignmentId, Guid targetKey, RuleValueSourceKind sourceKind, string? constantJson, string? expressionSource, string? expressionAstJson, int sortOrder)
    {
        EnsureDraft();
        var rule = _rules.SingleOrDefault(r => r.Id == ruleId)
            ?? throw new ArgumentException("规则不属于此修订。");
        rule.UpdateAssignment(assignmentId, targetKey, sourceKind, constantJson, expressionSource, expressionAstJson, sortOrder, Variables);
    }

    /// <summary>从草稿移除指定场景绑定。</summary>
    public void RemoveBinding(Guid bindingId)
    {
        EnsureDraft();
        var binding = _bindings.SingleOrDefault(b => b.Id == bindingId)
            ?? throw new ArgumentException("绑定不属于此修订。");
        _bindings.Remove(binding);
    }

    /// <summary>按锁定契约替换指定绑定的优先级和全部条件。</summary>
    public void UpdateBinding(Guid bindingId, RuleScenarioDescriptor contract, int priority, IReadOnlyCollection<(string Code, RuleBindingOperator Operator, string ValueJson)> criteria)
    {
        EnsureDraft();
        var binding = _bindings.SingleOrDefault(b => b.Id == bindingId)
            ?? throw new ArgumentException("绑定不属于此修订。");
        if (contract.ScenarioCode != ScenarioCode || contract.ContractVersion != ContractVersion)
        {
            throw new ArgumentException("绑定契约与修订契约不一致。");
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
            throw new ArgumentException("必须提供变量编码和名称。");
        }

        code = code.Trim();
        name = name.Trim();
        if (Variables.Any(v => v.Code == code))
        {
            throw new InvalidOperationException($"变量编码重复：{code}。");
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
            throw new ArgumentException("必须提供变量编码和名称。");
        }

        code = code.Trim();
        name = name.Trim();
        var variable = GetVariable(stableKey);
        if (Variables.Any(v => v != variable && v.Code == code))
        {
            throw new InvalidOperationException($"变量编码重复：{code}。");
        }

        variable.Rename(code, name);
    }

    /// <summary>设置草稿契约入口的默认值。</summary>
    public void SetInputDefault(Guid stableKey, string? defaultValueJson)
    {
        EnsureDraft();
        var variable = GetVariable(stableKey);
        variable.SetInputDefault(defaultValueJson);
    }

    /// <summary>维护草稿中间变量属性，并先核对现有常量赋值的精度。</summary>
    public void UpdateIntermediateProperties(Guid stableKey, string? defaultValueJson, string? unit, int? precision)
    {
        EnsureDraft();
        var variable = GetVariable(stableKey);
        var constants = Rules.SelectMany(r => r.Assignments).Where(a => a.TargetVariableStableKey == stableKey && a.ValueSourceKind == RuleValueSourceKind.Constant).Select(a => a.ConstantValueJson!).ToArray();
        variable.UpdateIntermediateProperties(defaultValueJson, unit, precision, constants);
    }

    /// <summary>仅删除没有引用的草稿中间变量；契约变量及已发布修订保持不变。</summary>
    /// <remarks>核心逻辑：先查找所有引用，再仅删除未被引用的用户变量；调用时机：维护者删除草稿中间变量时。</remarks>
    public void RemoveIntermediate(Guid stableKey)
    {
        EnsureDraft();
        var variable = GetVariable(stableKey);
        if (variable.Origin != RuleVariableOrigin.UserDefined ||
            variable.Direction != RuleVariableDirection.Intermediate)
        {
            throw new ArgumentException("只能删除用户定义的中间变量。");
        }

        var references = RuleRevisionImpactAnalyzer.GetVariableReferences(this, stableKey).Where(x => x.BlocksVariableDeletion).ToArray();
        if (references.Length > 0)
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.VariableReferenced).WithData("References", references.Select(x => x.Path).Distinct().ToArray());
        }

        _variables.Remove(variable);
    }

    /// <summary>将尚未发布的草稿标记为放弃。</summary>
    public void Abandon()
    {
        EnsureDraft();
        Status = RuleRevisionStatus.Abandoned;
    }

    /// <summary>增加以场景上下文、输入和预期输出组成的发布门禁用例。</summary>
    public ReleaseGateCase AddGateCase(Guid id, string name, string contextJson, string inputsJson, string expectedResultsJson, RuleExecutionStatus expectedStatus = RuleExecutionStatus.Succeeded)
    {
        EnsureDraft();
        if (_gateCases.Any(c => c.Name == name?.Trim()))
        {
            throw new ArgumentException("同一修订内的门禁用例名称不能重复。");
        }

        var gateCase = new ReleaseGateCase(id, Id, name, contextJson, inputsJson, expectedResultsJson, expectedStatus);
        _gateCases.Add(gateCase);
        return gateCase;
    }

    /// <summary>删除草稿中的指定发布门禁用例。</summary>
    public void RemoveGateCase(Guid gateCaseId)
    {
        EnsureDraft();
        var gateCase = _gateCases.SingleOrDefault(c => c.Id == gateCaseId)
            ?? throw new ArgumentException("门禁用例不属于此修订。");
        _gateCases.Remove(gateCase);
    }

    /// <summary>通过门禁后冻结本修订及其内容哈希。</summary>
    /// <remarks>核心逻辑：检查草稿状态并冻结内容哈希与变更说明；调用时机：发布服务通过全部门禁后。</remarks>
    public void Publish(string contentHash, string? changeSummary)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("必须提供内容哈希。", nameof(contentHash));
        }

        if (changeSummary?.Length > 2048)
        {
            throw new ArgumentException("变更说明不能超过 2048 个字符。", nameof(changeSummary));
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
            throw new InvalidOperationException("只能编辑草稿修订。");
        }
    }
}
