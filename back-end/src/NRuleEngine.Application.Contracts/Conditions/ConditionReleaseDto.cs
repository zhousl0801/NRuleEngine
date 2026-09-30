using NRuleEngine.Domain.Shared.Release;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>条件集发布或回滚的历史记录。</summary>
[Serializable]
public sealed class ConditionReleaseDto
{
    /// <summary>发布记录标识。</summary>
    public Guid Id { get; set; }
    /// <summary>切换后的活动修订。</summary>
    public Guid ActivatedRevisionId { get; set; }
    /// <summary>切换前的活动修订。</summary>
    public Guid? PreviousRevisionId { get; set; }
    /// <summary>条件集内的发布历史序号。</summary>
    public int ReleaseNo { get; set; }
    /// <summary>发布或回滚类型。</summary>
    public ReleaseKind Kind { get; set; }
    /// <summary>目标修订的内容摘要。</summary>
    public string ContentHash { get; set; }
    /// <summary>维护者填写的变更说明。</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>映射一次发布历史记录。</summary>
    public ConditionReleaseDto(Guid id, Guid activatedRevisionId, Guid? previousRevisionId,
        int releaseNo, ReleaseKind kind, string contentHash, string? changeSummary)
    {
        Id = id;
        ActivatedRevisionId = activatedRevisionId;
        PreviousRevisionId = previousRevisionId;
        ReleaseNo = releaseNo;
        Kind = kind;
        ContentHash = contentHash;
        ChangeSummary = changeSummary;
    }
}
