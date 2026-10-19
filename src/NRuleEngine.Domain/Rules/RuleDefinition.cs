using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Expressions;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Rules;

/// <summary>修订内的一条规则及其有序赋值。</summary>
public sealed class RuleDefinition : Entity<Guid>
{
    /// <summary>本规则所属的修订标识。</summary>
    public Guid RevisionId { get; private set; }

    /// <summary>维护者可识别的规则名称。</summary>
    public string Name { get; private set; } = null!;

    /// <summary>执行优先级；数值大的规则先评估。</summary>
    public int Priority { get; private set; }

    /// <summary>同一优先级内的唯一执行顺序值。</summary>
    public int SortOrder { get; private set; }

    /// <summary>规则是否参与试算及后续发布执行。</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>供编辑器展示的条件源文本；不作为执行依据。</summary>
    public string? ConditionSource { get; private set; }

    /// <summary>返回 Boolean 的版本化条件 AST JSON。</summary>
    public string ConditionAstJson { get; private set; } = null!;
    private List<RuleAssignment> _assignments = [];
    /// <summary>规则命中后按 SortOrder 依次执行的赋值。</summary>
    public IReadOnlyCollection<RuleAssignment> Assignments => _assignments.AsReadOnly();

    private RuleDefinition()
    {
    }

    /// <summary>创建规则并检查条件表达式的静态类型。</summary>
    internal RuleDefinition(
        Guid id,
        Guid revisionId,
        string name,
        int priority,
        int sortOrder,
        bool isEnabled,
        string? conditionSource,
        string conditionAstJson,
        IReadOnlyCollection<VariableDefinition> variables) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("必须提供规则名称。");
        }

        if (RuleExpressionEngine.Validate(conditionAstJson, variables, out _) != Domain.Shared.Rules.RuleValueKind.Boolean)
        {
            throw new ArgumentException("规则条件必须返回布尔值。");
        }

        RevisionId = revisionId;
        Name = name.Trim();
        Priority = priority;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        ConditionSource = conditionSource;
        ConditionAstJson = conditionAstJson;
    }

    /// <summary>加入赋值并拒绝同规则内的重复目标及顺序值。</summary>
    internal void AddAssignment(RuleAssignment assignment)
    {
        if (_assignments.Any(a => a.TargetVariableStableKey == assignment.TargetVariableStableKey))
        {
            throw new ArgumentException("同一规则不能对同一目标变量重复赋值。");
        }

        if (_assignments.Any(a => a.SortOrder == assignment.SortOrder))
        {
            throw new ArgumentException("同一规则内的赋值排序值不能重复。");
        }

        _assignments.Add(assignment);
    }

    /// <summary>删除属于本规则的赋值。</summary>
    internal void RemoveAssignment(Guid assignmentId)
    {
        var assignment = _assignments.SingleOrDefault(a => a.Id == assignmentId)
            ?? throw new ArgumentException("赋值不属于此规则。");
        _assignments.Remove(assignment);
    }

    /// <summary>重新校验并更新规则条件与执行顺序，不改变规则标识。</summary>
    internal void Update(string name, int priority, int sortOrder, bool isEnabled, string? conditionSource, string conditionAstJson, IReadOnlyCollection<VariableDefinition> variables)
    {
        var checkedRule = new RuleDefinition(Id, RevisionId, name, priority, sortOrder, isEnabled, conditionSource, conditionAstJson, variables);
        Name = checkedRule.Name;
        Priority = checkedRule.Priority;
        SortOrder = checkedRule.SortOrder;
        IsEnabled = checkedRule.IsEnabled;
        ConditionSource = checkedRule.ConditionSource;
        ConditionAstJson = checkedRule.ConditionAstJson;
    }

    /// <summary>更新本规则的一条赋值，并检查其他赋值的目标和顺序冲突。</summary>
    internal void UpdateAssignment(Guid assignmentId, Guid targetKey, Domain.Shared.Rules.RuleValueSourceKind sourceKind, string? constantJson, string? expressionSource, string? expressionAstJson, int sortOrder, IReadOnlyCollection<VariableDefinition> variables)
    {
        var assignment = _assignments.SingleOrDefault(a => a.Id == assignmentId)
            ?? throw new ArgumentException("赋值不属于此规则。");
        if (_assignments.Any(a => a.Id != assignmentId && a.TargetVariableStableKey == targetKey))
        {
            throw new ArgumentException("同一规则不能对同一目标变量重复赋值。");
        }

        if (_assignments.Any(a => a.Id != assignmentId && a.SortOrder == sortOrder))
        {
            throw new ArgumentException("同一规则内的赋值排序值不能重复。");
        }

        assignment.Update(targetKey, sourceKind, constantJson, expressionSource, expressionAstJson, sortOrder, variables);
    }

    /// <summary>复制规则及赋值到新修订，保留 AST 中的变量 StableKey。</summary>
    internal RuleDefinition CopyTo(Guid revisionId, IReadOnlyCollection<VariableDefinition> variables)
    {
        var copy = new RuleDefinition(Guid.NewGuid(), revisionId, Name, Priority, SortOrder, IsEnabled, ConditionSource, ConditionAstJson, variables);
        foreach (var assignment in Assignments)
        {
            copy.AddAssignment(assignment.CopyTo(copy.Id, variables));
        }
        return copy;
    }
}
