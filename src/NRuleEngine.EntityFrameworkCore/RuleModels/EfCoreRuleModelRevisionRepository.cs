using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.RuleModels;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore.RuleModels;

/// <summary>
/// EF Core 规则修订仓储。
/// </summary>
public class EfCoreRuleModelRevisionRepository
    : EfCoreRepository<NRuleEngineDbContext, RuleModelRevision, Guid>, IRuleModelRevisionRepository
{
    public EfCoreRuleModelRevisionRepository(IDbContextProvider<NRuleEngineDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    /// <summary>将子实体变更纳入修订并发边界；ABP 8 不会因集合变化自动更新聚合根戳。</summary>
    public override async Task<RuleModelRevision> UpdateAsync(RuleModelRevision entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var updated = await base.UpdateAsync(entity, autoSave: false, cancellationToken);
        var dbContext = await GetDbContextAsync();
        dbContext.Entry(updated).Property(x => x.ConcurrencyStamp).IsModified = true;
        if (autoSave)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return updated;
    }

    public virtual async Task<RuleModelRevision?> FindWithVariablesAsync(Guid revisionId)
    {
        return await (await GetDbSetAsync()).Include(x => x.Variables).Include(x => x.Rules).ThenInclude(x => x.Assignments).Include(x => x.Bindings).ThenInclude(x => x.Criteria).Include(x => x.GateCases).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == revisionId);
    }

    public virtual async Task<List<RuleModelRevision>> GetListByModelAsync(Guid modelId)
    {
        return await (await GetDbSetAsync()).Where(x => x.RuleModelId == modelId).OrderByDescending(x => x.RevisionNo).ToListAsync();
    }

    /// <summary>批量载入运行候选修订及其全部内容，并按拆分查询避免多集合笛卡尔积。</summary>
    public virtual async Task<List<RuleModelRevision>> GetByIdsWithDetailsAsync(IReadOnlyCollection<Guid> revisionIds, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).Where(x => revisionIds.Contains(x.Id)).Include(x => x.Variables).Include(x => x.Rules).ThenInclude(x => x.Assignments).Include(x => x.Bindings).ThenInclude(x => x.Criteria).Include(x => x.GateCases).AsSplitQuery().ToListAsync(cancellationToken);
    }
}
