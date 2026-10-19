namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 规则的执行方式。
/// </summary>
public enum RuleExecutionMode
{
    /// <summary>按顺序评估，首条命中规则执行后停止。</summary>
    FirstMatch,
    /// <summary>依次评估规则，赋值写回上下文供后续规则使用。</summary>
    Sequential,
    /// <summary>每条规则独立计算，按执行顺序返回全部命中的完整结果行。</summary>
    CollectMatches
}
