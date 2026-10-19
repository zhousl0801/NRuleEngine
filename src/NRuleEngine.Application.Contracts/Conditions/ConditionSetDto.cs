using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>条件集身份及当前活动修订。</summary>
[Serializable]
public sealed class ConditionSetDto
{
    /// <summary>条件集实体标识。</summary>
    public Guid Id { get; set; }
    /// <summary>已注册的应用编码。</summary>
    public string AppCode { get; set; }
    /// <summary>条件判定场景编码。</summary>
    public string ScenarioCode { get; set; }
    /// <summary>跨修订稳定的业务编码。</summary>
    public string Code { get; set; }
    /// <summary>展示名称。</summary>
    public string Name { get; set; }
    /// <summary>可选业务说明。</summary>
    public string? Description { get; set; }
    /// <summary>活动或已归档状态。</summary>
    public RuleModelStatus Status { get; set; }
    /// <summary>活动发布修订；首次发布前为空。</summary>
    public Guid? ActiveRevisionId { get; set; }
    /// <summary>后续写操作所需的乐观并发戳。</summary>
    public string ConcurrencyStamp { get; set; }

    /// <summary>映射条件集身份和活动状态。</summary>
    public ConditionSetDto(Guid id, string appCode, string scenarioCode, string code, string name,
        string? description, RuleModelStatus status, Guid? activeRevisionId, string concurrencyStamp)
    {
        Id = id;
        AppCode = appCode;
        ScenarioCode = scenarioCode;
        Code = code;
        Name = name;
        Description = description;
        Status = status;
        ActiveRevisionId = activeRevisionId;
        ConcurrencyStamp = concurrencyStamp;
    }
}
