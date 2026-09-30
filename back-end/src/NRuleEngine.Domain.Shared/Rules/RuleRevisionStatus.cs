namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 规则修订的状态。
/// </summary>
public enum RuleRevisionStatus
{
    /// <summary>可继续编制的草稿。</summary>
    Draft,
    /// <summary>发布后的不可变修订。</summary>
    Published,
    /// <summary>不再继续编制的草稿。</summary>
    Abandoned
}
