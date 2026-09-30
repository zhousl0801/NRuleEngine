using System.ComponentModel.DataAnnotations;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Rules;

/// <summary>给草稿规则增加一个常量或表达式赋值。</summary>
[Serializable]
public class AddAssignmentDto
{
    /// <summary>待写入变量的 StableKey，不能指向入口变量。</summary>
    public Guid TargetVariableStableKey { get; set; }
    /// <summary>选择常量值或表达式求值。</summary>
    public RuleValueSourceKind ValueSourceKind { get; set; }
    /// <summary>常量来源时必填的 JSON 值；表达式来源时留空。</summary>
    public string? ConstantValueJson { get; set; }
    /// <summary>供编辑器展示的表达式文本，不作为执行依据。</summary>
    public string? ExpressionSource { get; set; }
    /// <summary>表达式来源时必填的版本化 AST JSON。</summary>
    public string? ExpressionAstJson { get; set; }
    /// <summary>同一规则内的唯一赋值顺序值。</summary>
    public int SortOrder { get; set; }
    /// <summary>调用方读取草稿时获得的并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
