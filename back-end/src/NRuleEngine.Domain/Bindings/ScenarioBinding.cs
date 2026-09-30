using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Bindings;

/// <summary>修订内的场景绑定，条件之间按 AND 组合。</summary>
public sealed class ScenarioBinding : Entity<Guid>
{
    /// <summary>本绑定所属修订的标识。</summary>
    public Guid RevisionId { get; private set; }
    /// <summary>接入应用在代码中注册的编码。</summary>
    public string AppCode { get; private set; } = null!;
    /// <summary>绑定所属场景的编码。</summary>
    public string ScenarioCode { get; private set; } = null!;
    /// <summary>绑定锁定的场景契约版本。</summary>
    public string ContractVersion { get; private set; } = null!;
    /// <summary>跨同一场景的活动模型比较的绑定优先级。</summary>
    public int Priority { get; private set; }
    private List<RuleBindingCriterion> _criteria = [];
    /// <summary>按 AND 关系组合的维度条件。</summary>
    public IReadOnlyCollection<RuleBindingCriterion> Criteria => _criteria.AsReadOnly();

    private ScenarioBinding()
    {
    }

    /// <summary>以修订锁定的契约创建绑定，并保存其身份信息。</summary>
    internal ScenarioBinding(Guid id, Guid revisionId, RuleScenarioDescriptor contract, int priority) : base(id)
    {
        RevisionId = revisionId;
        AppCode = contract.AppCode;
        ScenarioCode = contract.ScenarioCode;
        ContractVersion = contract.ContractVersion;
        Priority = priority;
    }

    /// <summary>按场景契约检查维度、操作符和值类型后加入条件。</summary>
    internal void AddCriterion(Guid id, RuleScenarioDescriptor contract, string code, RuleBindingOperator op,
        string valueJson)
    {
        var dimension = contract.BindingDimensions.SingleOrDefault(d => d.Code == code)
            ?? throw new ArgumentException("Unknown binding dimension.");
        if (_criteria.Any(c => c.DimensionCode == code))
        {
            throw new ArgumentException("Binding dimension is duplicated.");
        }
        if (!dimension.AllowedOperators.Contains(op))
        {
            throw new ArgumentException("Binding operator is not allowed for this dimension.");
        }
        if (op is not (RuleBindingOperator.Equals or RuleBindingOperator.In) &&
            dimension.ValueKind is not (RuleValueKind.Decimal or RuleValueKind.String))
        {
            throw new ArgumentException("Ordered binding operators require decimal or string values.");
        }
        using var document = JsonDocument.Parse(valueJson);
        if (op == RuleBindingOperator.In)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                throw new ArgumentException("In requires a nonempty JSON array.");
            }
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (RuleExpressionEngine.ReadConstant(dimension.ValueKind, element) is null)
                {
                    throw new ArgumentException("Binding values cannot be null.");
                }
            }
        }
        else if (RuleExpressionEngine.ReadConstant(dimension.ValueKind, document.RootElement) is null)
        {
            throw new ArgumentException("Binding values cannot be null.");
        }
        _criteria.Add(new RuleBindingCriterion(id, Id, code, op, valueJson));
    }

    /// <summary>复制绑定和条件到新草稿，重新分配各实体标识。</summary>
    internal ScenarioBinding CopyTo(Guid revisionId)
    {
        var copy = new ScenarioBinding
        {
            Id = Guid.NewGuid(), RevisionId = revisionId, AppCode = AppCode,
            ScenarioCode = ScenarioCode, ContractVersion = ContractVersion, Priority = Priority,
            _criteria = []
        };
        copy._criteria = Criteria.Select(c => c.CopyTo(copy.Id)).ToList();
        return copy;
    }

    /// <summary>先校验完整的新条件集合，再整体替换当前条件。</summary>
    internal void Update(RuleScenarioDescriptor contract, int priority,
        IReadOnlyCollection<(string Code, RuleBindingOperator Operator, string ValueJson)> criteria)
    {
        var checkedBinding = new ScenarioBinding(Id, RevisionId, contract, priority);
        foreach (var criterion in criteria)
        {
            checkedBinding.AddCriterion(Guid.NewGuid(), contract, criterion.Code, criterion.Operator, criterion.ValueJson);
        }
        Priority = checkedBinding.Priority;
        _criteria.Clear();
        _criteria.AddRange(checkedBinding.Criteria);
    }
}
