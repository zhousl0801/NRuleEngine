using NRuleEngine.Domain.Shared.Release;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>跨规则模型和条件集的发布或回滚记录。</summary>
[Serializable]
public class ReleaseHistoryDto
{
    /// <summary>发布记录标识。</summary>
    public Guid Id { get; set; }

    /// <summary>记录所属对象类型。</summary>
    public ReleaseTargetType TargetType { get; set; }

    /// <summary>记录所属对象标识。</summary>
    public Guid TargetId { get; set; }

    /// <summary>记录所属对象编码。</summary>
    public string TargetCode { get; set; } = string.Empty;

    /// <summary>记录所属对象名称。</summary>
    public string TargetName { get; set; } = string.Empty;

    /// <summary>对象内递增的发布序号。</summary>
    public int ReleaseNo { get; set; }

    /// <summary>本次激活的修订标识。</summary>
    public Guid ActivatedRevisionId { get; set; }

    /// <summary>本次激活的修订号；修订缺失时为零。</summary>
    public int RevisionNo { get; set; }

    /// <summary>发布或回滚操作类型。</summary>
    public ReleaseKind Kind { get; set; }

    /// <summary>被激活内容的固定哈希。</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>维护者填写的发布或回滚说明。</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>操作时间（UTC）。</summary>
    public DateTime CreationTime { get; set; }
}
