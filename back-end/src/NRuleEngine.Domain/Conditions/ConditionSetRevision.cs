using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities.Auditing;

namespace NRuleEngine.Domain.Conditions;

/// <summary>条件集的可发布修订；只有草稿允许修改条件。</summary>
public sealed class ConditionSetRevision : AuditedAggregateRoot<Guid>
{
    /// <summary>所属条件集的标识。</summary>
    public Guid ConditionSetId { get; private set; }
    /// <summary>同一条件集内递增的修订序号。</summary>
    public int RevisionNo { get; private set; }
    /// <summary>复制草稿的来源修订；首个草稿为空。</summary>
    public Guid? BasedOnRevisionId { get; private set; }
    /// <summary>草稿、已发布或已放弃状态。</summary>
    public RuleRevisionStatus Status { get; private set; }
    /// <summary>修订锁定的条件场景编码。</summary>
    public string ScenarioCode { get; private set; } = null!;
    /// <summary>修订锁定的场景契约版本。</summary>
    public string ContractVersion { get; private set; } = null!;
    /// <summary>多个候选命中时的选择优先级，值越大越优先。</summary>
    public int Priority { get; private set; }
    /// <summary>供维护者阅读的原始条件文本；不参与执行。</summary>
    public string? ConditionSource { get; private set; }
    /// <summary>返回 Boolean 的版本化条件 AST；草稿可暂时为空。</summary>
    public string? ConditionAstJson { get; private set; }
    /// <summary>发布后固定的语义内容摘要；草稿为空。</summary>
    public string? ContentHash { get; private set; }
    /// <summary>本次发布的维护说明。</summary>
    public string? ChangeSummary { get; private set; }
    private List<ConditionInputDefinition> _inputs = [];
    /// <summary>修订所锁定的场景输入快照。</summary>
    public IReadOnlyCollection<ConditionInputDefinition> Inputs
    {
        get
        {
            return _inputs.AsReadOnly();
        }
    }

    private ConditionSetRevision()
    {
    }

    /// <summary>按固定场景契约创建条件草稿及输入快照。</summary>
    public ConditionSetRevision(Guid id, Guid setId, int revisionNo, ConditionScenarioDescriptor contract) : base(id)
    {
        if (revisionNo < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNo), "修订号必须大于零。");
        }

        ConditionSetId = setId;
        RevisionNo = revisionNo;
        Status = RuleRevisionStatus.Draft;
        ScenarioCode = contract.ScenarioCode;
        ContractVersion = contract.ContractVersion;
        _inputs = contract.Inputs.Select(x => ConditionInputDefinition.FromContract(id, x)).ToList();
    }

    /// <summary>复制条件与输入快照为可编辑草稿，保留输入 StableKey。</summary>
    /// <remarks>核心逻辑：复制输入稳定键和条件内容并分配新实体标识；调用时机：创建条件集新草稿时。</remarks>
    public static ConditionSetRevision CopyDraft(Guid id, Guid setId, int revisionNo, ConditionSetRevision source)
    {
        if (revisionNo < 1 || source.ConditionSetId != setId)
        {
            throw new ArgumentException("源修订无效。");
        }

        return new ConditionSetRevision
        {
            Id = id,
            ConditionSetId = setId,
            RevisionNo = revisionNo,
            BasedOnRevisionId = source.Id,
            Status = RuleRevisionStatus.Draft,
            ScenarioCode = source.ScenarioCode,
            ContractVersion = source.ContractVersion,
            Priority = source.Priority,
            ConditionSource = source.ConditionSource,
            ConditionAstJson = source.ConditionAstJson,
            _inputs = source.Inputs.Select(x => x.CopyTo(id)).ToList()
        };
    }

    /// <summary>仅在草稿中设置返回 Boolean 的条件 AST 和选择优先级。</summary>
    /// <remarks>核心逻辑：校验布尔 AST 与源文本长度后更新草稿条件；调用时机：维护者保存条件编制内容时。</remarks>
    public void SetCondition(string? source, string astJson, int priority)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(astJson))
        {
            throw new ArgumentException("必须提供条件 AST。", nameof(astJson));
        }

        if (source?.Length > 8192)
        {
            throw new ArgumentException("条件源文本不能超过 8192 个字符。", nameof(source));
        }

        if (RuleExpressionEngine.Validate(astJson, Inputs, out _) != RuleValueKind.Boolean)
        {
            throw new ArgumentException("条件求值结果必须是布尔值。", nameof(astJson));
        }

        ConditionSource = source;
        ConditionAstJson = astJson;
        Priority = priority;
    }

    /// <summary>门禁通过后冻结修订与内容摘要。</summary>
    /// <remarks>核心逻辑：确认条件存在后冻结内容哈希与发布状态；调用时机：条件集通过正反样本门禁时。</remarks>
    public void Publish(string contentHash, string? changeSummary)
    {
        EnsureDraft();
        if (ConditionAstJson is null)
        {
            throw new InvalidOperationException("发布前必须设置条件。");
        }

        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("必须提供内容哈希。", nameof(contentHash));
        }

        if (changeSummary?.Length > 2048)
        {
            throw new ArgumentException("变更说明不能超过 2048 个字符。", nameof(changeSummary));
        }

        ContentHash = contentHash;
        ChangeSummary = changeSummary;
        Status = RuleRevisionStatus.Published;
    }

    /// <summary>放弃草稿，保留修订记录但禁止继续修改。</summary>
    public void Abandon()
    {
        EnsureDraft();
        Status = RuleRevisionStatus.Abandoned;
    }

    private void EnsureDraft()
    {
        if (Status != RuleRevisionStatus.Draft)
        {
            throw new InvalidOperationException("只能编辑草稿修订。");
        }
    }
}
