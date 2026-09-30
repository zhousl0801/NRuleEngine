namespace NRuleEngine.Domain.Conditions;

/// <summary>一次判定中由调用方显式指定的已发布候选。</summary>
/// <param name="Set">候选条件集的稳定身份与活动指针。</param>
/// <param name="Revision">活动的已发布修订及其输入快照。</param>
public sealed record ConditionCandidate(ConditionSet Set, ConditionSetRevision Revision);
