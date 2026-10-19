namespace NRuleEngine.Domain.Simulation;

/// <summary>一次试算中某条规则的条件结果。</summary>
/// <param name="RuleId">被评估的规则标识。</param>
/// <param name="RuleName">规则展示名称。</param>
/// <param name="Matched">条件是否命中。</param>
public sealed record RuleConditionOutcome(Guid RuleId, string RuleName, bool Matched);
