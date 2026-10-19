using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Domain.Release;

/// <summary>通过数据库写入协调同一应用场景的竞争发布。</summary>
public interface IReleaseScopeRepository : IRepository<ReleaseScope, Guid>
{
    /// <summary>在调用方事务内更新或创建场景行，以取得数据库级写入协调。</summary>
    Task AcquireAsync(string appCode, string scenarioCode);
}
