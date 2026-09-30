using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 规则修订的列表摘要。
/// </summary>
[Serializable]
public class RuleRevisionSummaryDto
{
    /// <summary>
    /// 标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 修订号。
    /// </summary>
    public int RevisionNo { get; set; }

    /// <summary>
    /// 源修订标识。
    /// </summary>
    public Guid? BasedOnRevisionId { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public RuleRevisionStatus Status { get; set; }

    /// <summary>
    /// 场景编码。
    /// </summary>
    public string ScenarioCode { get; set; } = string.Empty;

    /// <summary>
    /// 场景契约版本。
    /// </summary>
    public string ContractVersion { get; set; } = string.Empty;

    /// <summary>
    /// 并发戳。
    /// </summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;

    public RuleRevisionSummaryDto()
    {
    }

    public RuleRevisionSummaryDto(
        Guid id, int revisionNo, Guid? basedOnRevisionId,
        RuleRevisionStatus status, string scenarioCode, string contractVersion,
        string concurrencyStamp)
    {
        Id = id;
        RevisionNo = revisionNo;
        BasedOnRevisionId = basedOnRevisionId;
        Status = status;
        ScenarioCode = scenarioCode;
        ContractVersion = contractVersion;
        ConcurrencyStamp = concurrencyStamp;
    }
}
