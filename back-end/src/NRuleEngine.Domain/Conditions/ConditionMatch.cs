namespace NRuleEngine.Domain.Conditions;

/// <summary>命中的稳定业务编码及具体发布修订。</summary>
/// <param name="Code">跨修订稳定的条件集业务编码。</param>
/// <param name="ConditionSetId">命中条件集的实体标识。</param>
/// <param name="RevisionId">参与本次判定的已发布修订。</param>
/// <param name="Priority">命中修订的选择优先级。</param>
public sealed record ConditionMatch(string Code, Guid ConditionSetId, Guid RevisionId, int Priority);
