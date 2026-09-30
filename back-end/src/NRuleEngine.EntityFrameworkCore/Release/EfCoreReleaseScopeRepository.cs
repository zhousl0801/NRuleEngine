using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Release;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore.Release;

/// <summary>使用同一场景协调行的原子更新取得数据库写锁。</summary>
public class EfCoreReleaseScopeRepository
    : EfCoreRepository<NRuleEngineDbContext, ReleaseScope, Guid>, IReleaseScopeRepository
{
    public EfCoreReleaseScopeRepository(IDbContextProvider<NRuleEngineDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    /// <summary>更新已有协调行；首次发布插入唯一行，竞争创建会由数据库唯一约束拒绝。</summary>
    public virtual async Task AcquireAsync(string appCode, string scenarioCode)
    {
        var changed = await (await GetDbSetAsync())
            .Where(x => x.AppCode == appCode && x.ScenarioCode == scenarioCode)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Version, s => s.Version + 1));
        if (changed == 0)
        {
            await InsertAsync(new ReleaseScope(Guid.NewGuid(), appCode, scenarioCode), autoSave: true);
        }
    }
}
