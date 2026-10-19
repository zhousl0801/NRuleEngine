namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 绑定维度支持的比较运算。
/// </summary>
public enum RuleBindingOperator
{
    /// <summary>绑定值与上下文值相等。</summary>
    Equals,
    /// <summary>上下文值属于绑定的非空候选集合。</summary>
    In,
    /// <summary>上下文值严格大于绑定值。</summary>
    GreaterThan,
    /// <summary>上下文值大于或等于绑定值。</summary>
    GreaterThanOrEqual,
    /// <summary>上下文值严格小于绑定值。</summary>
    LessThan,
    /// <summary>上下文值小于或等于绑定值。</summary>
    LessThanOrEqual
}
