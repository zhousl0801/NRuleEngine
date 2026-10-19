namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 规则值的数据类型。
/// </summary>
public enum RuleValueKind
{
    /// <summary>使用 decimal 计算的十进制数值。</summary>
    Decimal,
    /// <summary>普通文本值。</summary>
    String,
    /// <summary>真或假的布尔值。</summary>
    Boolean,
    /// <summary>由宿主通过 EnumCode 解释的枚举文本值。</summary>
    Enum
}
