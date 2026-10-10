using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities.Auditing;

namespace NRuleEngine.Domain.Conditions;

/// <summary>一个场景下可独立维护、跨修订保持业务编码的布尔条件集。</summary>
public sealed class ConditionSet : AuditedAggregateRoot<Guid>
{
    /// <summary>代码注册的接入应用编码。</summary>
    public string AppCode { get; private set; } = null!;
    /// <summary>条件集所属的业务场景编码。</summary>
    public string ScenarioCode { get; private set; } = null!;
    /// <summary>创建首个草稿时使用的场景契约版本。</summary>
    public string InitialContractVersion { get; private set; } = null!;
    /// <summary>应用内唯一、跨修订稳定的条件集业务编码。</summary>
    public string Code { get; private set; } = null!;
    /// <summary>供维护者识别的条件集名称。</summary>
    public string Name { get; private set; } = null!;
    /// <summary>条件集的可选业务说明。</summary>
    public string? Description { get; private set; }
    /// <summary>当前活动的已发布修订；首次发布前为空。</summary>
    public Guid? ActiveRevisionId { get; private set; }
    /// <summary>活动或已归档状态；归档后不能新增草稿或激活修订。</summary>
    public RuleModelStatus Status { get; private set; }
    /// <summary>下一个模型内修订序号。</summary>
    public int NextRevisionNo { get; private set; }
    /// <summary>下一个发布或回滚审计序号。</summary>
    public int NextReleaseNo { get; private set; }

    private ConditionSet()
    {
    }

    /// <summary>按已注册条件场景创建尚未发布的条件集。</summary>
    public ConditionSet(Guid id, string appCode, string scenarioCode, string contractVersion,
        string code, string name, string? description) : base(id)
    {
        AppCode = Required(appCode, nameof(appCode));
        ScenarioCode = Required(scenarioCode, nameof(scenarioCode));
        InitialContractVersion = Required(contractVersion, nameof(contractVersion));
        Code = Required(code, nameof(code));
        Name = Required(name, nameof(name));
        Description = description;
        Status = RuleModelStatus.Active;
        NextRevisionNo = 1;
        NextReleaseNo = 1;
    }

    /// <summary>给新草稿分配连续修订号；已归档时拒绝分配。</summary>
    public int AllocateRevisionNo()
    {
        EnsureActive();
        return NextRevisionNo++;
    }

    /// <summary>切换活动修订，并分配发布历史序号。</summary>
    public int Activate(Guid revisionId)
    {
        EnsureActive();
        if (revisionId == Guid.Empty)
        {
            throw new ArgumentException("必须提供修订标识。", nameof(revisionId));
        }

        ActiveRevisionId = revisionId;
        return NextReleaseNo++;
    }

    /// <summary>停止运行和后续编制，保留修订及发布历史。</summary>
    public void Archive()
    {
        Status = RuleModelStatus.Archived;
    }

    private void EnsureActive()
    {
        if (Status != RuleModelStatus.Active)
        {
            throw new InvalidOperationException("已归档的条件集不能修改。");
        }
    }

    private static string Required(string value, string name)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("必须提供值。", name)
            : value.Trim();
    }
}
