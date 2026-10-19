using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 规则模型的当前状态。
/// </summary>
[Serializable]
public class RuleModelDto
{
    /// <summary>
    /// 标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属应用编码。
    /// </summary>
    public string AppCode { get; set; } = string.Empty;

    /// <summary>
    /// 场景编码。
    /// </summary>
    public string ScenarioCode { get; set; } = string.Empty;

    /// <summary>
    /// 编码。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public RuleModelStatus Status { get; set; }

    /// <summary>
    /// 当前活动修订标识。
    /// </summary>
    public Guid? ActiveRevisionId { get; set; }

    /// <summary>
    /// 并发戳。
    /// </summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;

    public RuleModelDto()
    {
    }

    public RuleModelDto(
        Guid id, string appCode, string scenarioCode,
        string code, string name, string? description,
        RuleModelStatus status, Guid? activeRevisionId, string concurrencyStamp)
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
