using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Rules;

/// <summary>规则赋值详情。</summary>
[Serializable]
public class RuleAssignmentDto
{
    /// <summary>赋值的唯一标识。</summary>
    public Guid Id { get; set; }
    /// <summary>赋值目标变量的 StableKey。</summary>
    public Guid TargetVariableStableKey { get; set; }
    /// <summary>常量或表达式赋值来源。</summary>
    public RuleValueSourceKind ValueSourceKind { get; set; }
    /// <summary>常量来源时的 JSON 值。</summary>
    public string? ConstantValueJson { get; set; }
    /// <summary>供编辑器展示的表达式文本。</summary>
    public string? ExpressionSource { get; set; }
    /// <summary>表达式来源时的版本化 AST JSON。</summary>
    public string? ExpressionAstJson { get; set; }
    /// <summary>同一规则内的赋值执行顺序。</summary>
    public int SortOrder { get; set; }
}
