namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>一次命中的条件集业务编码和发布修订。</summary>
[Serializable]
public sealed class ConditionMatchDto
{
    /// <summary>跨修订稳定的条件集业务编码。</summary>
    public string Code { get; set; }
    /// <summary>命中条件集的实体标识。</summary>
    public Guid ConditionSetId { get; set; }
    /// <summary>参与判定的发布修订。</summary>
    public Guid RevisionId { get; set; }
    /// <summary>该修订的选择优先级。</summary>
    public int Priority { get; set; }

    /// <summary>映射一次条件集命中。</summary>
    public ConditionMatchDto(string code, Guid conditionSetId, Guid revisionId, int priority)
    {
        Code = code;
        ConditionSetId = conditionSetId;
        RevisionId = revisionId;
        Priority = priority;
    }
}
