using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>条件修订的编制状态和输入快照。</summary>
[Serializable]
public sealed class ConditionSetRevisionDto
{
    /// <summary>修订标识。</summary>
    public Guid Id { get; set; }
    /// <summary>所属条件集标识。</summary>
    public Guid ConditionSetId { get; set; }
    /// <summary>条件集内的修订序号。</summary>
    public int RevisionNo { get; set; }
    /// <summary>复制草稿的来源修订。</summary>
    public Guid? BasedOnRevisionId { get; set; }
    /// <summary>草稿、已发布或已放弃状态。</summary>
    public RuleRevisionStatus Status { get; set; }
    /// <summary>修订锁定的场景编码。</summary>
    public string ScenarioCode { get; set; }
    /// <summary>修订锁定的契约版本。</summary>
    public string ContractVersion { get; set; }
    /// <summary>候选命中时的选择优先级。</summary>
    public int Priority { get; set; }
    /// <summary>仅供展示的原始条件文本。</summary>
    public string? ConditionSource { get; set; }
    /// <summary>可执行的版本化布尔 AST。</summary>
    public string? ConditionAstJson { get; set; }
    /// <summary>发布后固定的语义内容摘要。</summary>
    public string? ContentHash { get; set; }
    /// <summary>修订锁定的输入快照。</summary>
    public IReadOnlyList<ConditionInputDto> Inputs { get; set; }
    /// <summary>后续编制操作所需的乐观并发戳。</summary>
    public string ConcurrencyStamp { get; set; }

    /// <summary>映射修订状态、条件及输入快照。</summary>
    public ConditionSetRevisionDto(Guid id, Guid conditionSetId, int revisionNo, Guid? basedOnRevisionId,
        RuleRevisionStatus status, string scenarioCode, string contractVersion, int priority,
        string? conditionSource, string? conditionAstJson, string? contentHash,
        IReadOnlyList<ConditionInputDto> inputs, string concurrencyStamp)
    {
        Id = id;
        ConditionSetId = conditionSetId;
        RevisionNo = revisionNo;
        BasedOnRevisionId = basedOnRevisionId;
        Status = status;
        ScenarioCode = scenarioCode;
        ContractVersion = contractVersion;
        Priority = priority;
        ConditionSource = conditionSource;
        ConditionAstJson = conditionAstJson;
        ContentHash = contentHash;
        Inputs = inputs;
        ConcurrencyStamp = concurrencyStamp;
    }
}
