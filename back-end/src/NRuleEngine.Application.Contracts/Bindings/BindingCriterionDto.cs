using System.ComponentModel.DataAnnotations;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Bindings;

/// <summary>绑定维度、操作符及 JSON 类型值。</summary>
[Serializable]
public class BindingCriterionDto
{
    /// <summary>场景契约声明的绑定维度编码。</summary>
    [Required]
    public string DimensionCode { get; set; } = string.Empty;
    /// <summary>该维度契约允许的比较操作符。</summary>
    public RuleBindingOperator Operator { get; set; }
    /// <summary>维度类型对应的 JSON 值；In 运算使用非空数组。</summary>
    [Required]
    public string TypedValueJson { get; set; } = string.Empty;
}
