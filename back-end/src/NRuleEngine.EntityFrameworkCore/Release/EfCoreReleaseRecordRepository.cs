using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Release;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore.Release;

/// <summary>EF Core 发布审计记录仓储。</summary>
public class EfCoreReleaseRecordRepository
    : EfCoreRepository<NRuleEngineDbContext, ReleaseRecord, Guid>, IReleaseRecordRepository
{
    public EfCoreReleaseRecordRepository(IDbContextProvider<NRuleEngineDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    /// <summary>按模型内发布序号倒序读取历史。</summary>
    public virtual async Task<List<ReleaseRecord>> GetListByModelAsync(Guid modelId)
    {
        return await (await GetDbSetAsync()).Where(x => x.RuleModelId == modelId)
            .OrderByDescending(x => x.ReleaseNo).ToListAsync();
    }
}
