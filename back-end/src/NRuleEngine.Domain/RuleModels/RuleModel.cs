using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities.Auditing;

namespace NRuleEngine.Domain.RuleModels;

/// <summary>
/// 规则模型聚合根，保存场景归属和修订序号。
/// </summary>
public sealed class RuleModel : AuditedAggregateRoot<Guid>
{
    /// <summary>接入应用在代码中注册的编码。</summary>
    public string AppCode { get; private set; } = null!;
    /// <summary>模型所属的场景编码。</summary>
    public string ScenarioCode { get; private set; } = null!;
    /// <summary>创建首个草稿时使用的场景契约版本。</summary>
    public string InitialContractVersion { get; private set; } = null!;
    /// <summary>应用内唯一的模型业务编码。</summary>
    public string Code { get; private set; } = null!;
    /// <summary>模型的展示名称。</summary>
    public string Name { get; private set; } = null!;
    /// <summary>供维护者阅读的模型说明。</summary>
    public string? Description { get; private set; }
    /// <summary>当前激活的发布修订；尚未发布时为空。</summary>
    public Guid? ActiveRevisionId { get; private set; }
    /// <summary>模型是否仍允许创建草稿。</summary>
    public RuleModelStatus Status { get; private set; }
    /// <summary>下次创建修订时将分配的模型内序号。</summary>
    public int NextRevisionNo { get; private set; }
    /// <summary>下次发布或回滚记录使用的模型内序号。</summary>
    public int NextReleaseNo { get; private set; }

    private RuleModel()
    {
    }

    /// <summary>按已选场景契约创建尚未发布的规则模型。</summary>
    public RuleModel(Guid id, string appCode, string scenarioCode, string contractVersion, string code, string name, string? description)
        : base(id)
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

    /// <summary>为新草稿分配不重复的修订号；已归档模型不再分配。</summary>
    public int AllocateRevisionNo()
    {
        if (Status != RuleModelStatus.Active)
        {
            throw new InvalidOperationException("已归档的模型不能创建草稿。");
        }

        return NextRevisionNo++;
    }

    /// <summary>阻止模型继续创建草稿，保留已有修订历史。</summary>
    public void Archive()
    {
        Status = RuleModelStatus.Archived;
    }

    /// <summary>切换活动发布修订，并分配连续的发布记录序号。</summary>
    public int Activate(Guid revisionId)
    {
        if (Status != RuleModelStatus.Active)
        {
            throw new InvalidOperationException("已归档的模型不能激活修订。");
        }
        ActiveRevisionId = revisionId;
        return NextReleaseNo++;
    }

    private static string Required(string value, string name)
    {
        return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("必须提供值。", name) : value.Trim();
    }
}
