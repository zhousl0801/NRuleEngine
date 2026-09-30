using NRuleEngine.Domain.Shared.Release;
using Volo.Abp.Domain.Entities.Auditing;

namespace NRuleEngine.Domain.Release;

/// <summary>一次发布或回滚的不可变审计记录。</summary>
public sealed class ReleaseRecord : CreationAuditedEntity<Guid>
{
    /// <summary>发生切换的模型标识。</summary>
    public Guid RuleModelId { get; private set; }
    /// <summary>切换后激活的修订标识。</summary>
    public Guid ActivatedRevisionId { get; private set; }
    /// <summary>切换前的活动修订；首次发布时为空。</summary>
    public Guid? PreviousRevisionId { get; private set; }
    /// <summary>模型内递增的发布记录序号。</summary>
    public int ReleaseNo { get; private set; }
    /// <summary>发布或回滚的操作类型。</summary>
    public ReleaseKind Kind { get; private set; }
    /// <summary>被激活修订的固定内容哈希。</summary>
    public string ContentHash { get; private set; } = null!;
    /// <summary>本次发布校验与门禁用例的摘要。</summary>
    public string ValidationSummary { get; private set; } = null!;
    /// <summary>激活修订时记录的变更说明，回滚时沿用目标修订的说明。</summary>
    public string? ChangeSummary { get; private set; }

    private ReleaseRecord()
    {
    }

    /// <summary>记录已完成校验的活动修订切换。</summary>
    public ReleaseRecord(Guid id, Guid modelId, Guid activatedRevisionId, Guid? previousRevisionId,
        int releaseNo, ReleaseKind kind, string contentHash, string validationSummary,
        string? changeSummary) : base(id)
    {
        RuleModelId = modelId;
        ActivatedRevisionId = activatedRevisionId;
        PreviousRevisionId = previousRevisionId;
        ReleaseNo = releaseNo;
        Kind = kind;
        ContentHash = contentHash;
        ValidationSummary = validationSummary;
        ChangeSummary = changeSummary;
    }
}
