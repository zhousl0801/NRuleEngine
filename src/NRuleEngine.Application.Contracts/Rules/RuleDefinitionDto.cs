namespace NRuleEngine.Application.Contracts.Rules;

/// <summary>规则及其赋值详情。</summary>
[Serializable]
public class RuleDefinitionDto
{
    /// <summary>规则的唯一标识。</summary>
    public Guid Id { get; set; }
    /// <summary>规则的展示名称。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>规则执行优先级；数值越大越优先。</summary>
    public int Priority { get; set; }
    /// <summary>同级规则的执行顺序值。</summary>
    public int SortOrder { get; set; }
    /// <summary>规则是否参与执行。</summary>
    public bool IsEnabled { get; set; }
    /// <summary>供编辑器展示的条件文本。</summary>
    public string? ConditionSource { get; set; }
    /// <summary>实际用于校验与试算的条件 AST JSON。</summary>
    public string ConditionAstJson { get; set; } = string.Empty;
    /// <summary>按 SortOrder 排列的规则赋值。</summary>
    public IReadOnlyList<RuleAssignmentDto> Assignments { get; set; } = [];
}
