namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>由场景契约固定的结果行基数。</summary>
public enum RuleResultCardinality
{
    /// <summary>成功时返回一行，允许首条命中或顺序计算。</summary>
    Single,
    /// <summary>每条命中规则独立返回一行，只允许 CollectMatches。</summary>
    Many
}
