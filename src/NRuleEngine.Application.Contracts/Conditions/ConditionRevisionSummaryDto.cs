using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>条件集修订列表中的状态和版本摘要。</summary>
[Serializable]
public sealed class ConditionRevisionSummaryDto
{
    /// <summary>修订标识。</summary>
    public Guid Id { get; set; }
    /// <summary>条件集内的修订序号。</summary>
    public int RevisionNo { get; set; }
    /// <summary>复制草稿的来源修订。</summary>
    public Guid? BasedOnRevisionId { get; set; }
    /// <summary>草稿、已发布或已放弃状态。</summary>
    public RuleRevisionStatus Status { get; set; }
    /// <summary>修订锁定的契约版本。</summary>
    public string ContractVersion { get; set; }
    /// <summary>修订当前的乐观并发戳。</summary>
    public string ConcurrencyStamp { get; set; }

    /// <summary>映射修订列表中的一项。</summary>
    public ConditionRevisionSummaryDto(Guid id, int revisionNo, Guid? basedOnRevisionId,
        RuleRevisionStatus status, string contractVersion, string concurrencyStamp)
    {
        Id = id;
        RevisionNo = revisionNo;
        BasedOnRevisionId = basedOnRevisionId;
        Status = status;
        ContractVersion = contractVersion;
        ConcurrencyStamp = concurrencyStamp;
    }
}
