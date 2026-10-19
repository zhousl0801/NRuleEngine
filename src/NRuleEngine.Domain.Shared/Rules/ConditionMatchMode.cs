namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>候选条件集命中后的裁决方式。</summary>
public enum ConditionMatchMode
{
    /// <summary>返回全部命中项。</summary>
    AllMatches,
    /// <summary>只返回唯一最高优先级命中项，并列时拒绝裁决。</summary>
    FirstMatch
}
