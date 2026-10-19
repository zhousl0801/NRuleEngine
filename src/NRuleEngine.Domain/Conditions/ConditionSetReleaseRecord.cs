using NRuleEngine.Domain.Shared.Release;
using Volo.Abp.Domain.Entities.Auditing;

namespace NRuleEngine.Domain.Conditions;

/// <summary>条件集发布或回滚的不可变审计记录。</summary>
public sealed class ConditionSetReleaseRecord : CreationAuditedEntity<Guid>
{
    /// <summary>发生发布或回滚的条件集标识。</summary>
    public Guid ConditionSetId { get; private set; }

    /// <summary>本次切换后活动的发布修订。</summary>
    public Guid ActivatedRevisionId { get; private set; }

    /// <summary>切换前的活动修订；首次发布前为空。</summary>
    public Guid? PreviousRevisionId { get; private set; }

    /// <summary>条件集内连续的发布历史序号。</summary>
    public int ReleaseNo { get; private set; }

    /// <summary>本次切换是发布还是回滚。</summary>
    public ReleaseKind Kind { get; private set; }

    /// <summary>目标修订发布时固定的语义内容摘要。</summary>
    public string ContentHash { get; private set; } = null!;

    /// <summary>维护者填写的发布或回滚说明。</summary>
    public string? ChangeSummary { get; private set; }

    /// <summary>发布门禁样本的 JSON；回滚记录为空。</summary>
    public string? GateCasesJson { get; private set; }

    private ConditionSetReleaseRecord()
    {
    }

    /// <summary>记录一次活动修订切换及其发布证据。</summary>
    public ConditionSetReleaseRecord(
        Guid id,
        Guid setId,
        Guid revisionId,
        Guid? previousId,
        int releaseNo,
        ReleaseKind kind,
        string hash,
        string? summary,
        string? gateCasesJson = null) : base(id)
    {
        ConditionSetId = setId;
        ActivatedRevisionId = revisionId;
        PreviousRevisionId = previousId;
        ReleaseNo = releaseNo;
        Kind = kind;
        ContentHash = hash;
        ChangeSummary = summary;
        GateCasesJson = gateCasesJson;
    }
}
