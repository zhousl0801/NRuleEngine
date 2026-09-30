using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Release;

/// <summary>同一应用和场景的数据库协调行；更新时获取数据库写锁。</summary>
public sealed class ReleaseScope : Entity<Guid>
{
    /// <summary>代码注册的接入应用编码。</summary>
    public string AppCode { get; private set; } = null!;
    /// <summary>应用内的场景编码。</summary>
    public string ScenarioCode { get; private set; } = null!;
    /// <summary>每次发布或回滚递增，用于强制数据库写入。</summary>
    public long Version { get; private set; }

    private ReleaseScope()
    {
    }

    /// <summary>首次为场景创建协调行。</summary>
    public ReleaseScope(Guid id, string appCode, string scenarioCode) : base(id)
    {
        AppCode = appCode;
        ScenarioCode = scenarioCode;
    }
}
