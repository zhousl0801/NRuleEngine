using NRuleEngine.Domain.Shared.Release;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>一次已提交的发布或回滚记录。</summary>
[Serializable]
public class ReleaseDto
{
    /// <summary>发布记录标识。</summary>
    public Guid Id { get; set; }
    /// <summary>所属规则模型标识。</summary>
    public Guid RuleModelId { get; set; }
    /// <summary>本次激活的修订标识。</summary>
    public Guid ActivatedRevisionId { get; set; }
    /// <summary>切换前的活动修订标识；首次发布时为空。</summary>
    public Guid? PreviousRevisionId { get; set; }
    /// <summary>模型内递增的发布序号。</summary>
    public int ReleaseNo { get; set; }
    /// <summary>发布或回滚操作类型。</summary>
    public ReleaseKind Kind { get; set; }
    /// <summary>被激活内容的固定哈希。</summary>
    public string ContentHash { get; set; } = string.Empty;
    /// <summary>校验与门禁通过情况的摘要。</summary>
    public string ValidationSummary { get; set; } = string.Empty;
    /// <summary>被激活修订的变更说明。</summary>
    public string? ChangeSummary { get; set; }
    /// <summary>操作人标识；无当前登录用户时为空。</summary>
    public Guid? CreatorId { get; set; }
    /// <summary>操作时间（UTC）。</summary>
    public DateTime CreationTime { get; set; }
    /// <summary>响应时模型的最新并发戳，供下一次写操作使用。</summary>
    public string ModelConcurrencyStamp { get; set; } = string.Empty;
}
