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

    public virtual async Task<RuleModelRevision?> FindWithVariablesAsync(Guid revisionId)
    {
        return await (await GetDbSetAsync()).Include(x => x.Variables)
            .Include(x => x.Rules).ThenInclude(x => x.Assignments)
            .Include(x => x.Bindings).ThenInclude(x => x.Criteria)
            .Include(x => x.GateCases)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == revisionId);
    }

    public virtual async Task<List<RuleModelRevision>> GetListByModelAsync(Guid modelId)
    {
        return await (await GetDbSetAsync()).Where(x => x.RuleModelId == modelId)
            .OrderByDescending(x => x.RevisionNo).ToListAsync();
    }

    /// <summary>批量载入运行候选修订及其全部内容，并按拆分查询避免多集合笛卡尔积。</summary>
    public virtual async Task<List<RuleModelRevision>> GetByIdsWithDetailsAsync(IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).Where(x => revisionIds.Contains(x.Id))
            .Include(x => x.Variables)
            .Include(x => x.Rules).ThenInclude(x => x.Assignments)
            .Include(x => x.Bindings).ThenInclude(x => x.Criteria)
            .Include(x => x.GateCases)
            .AsSplitQuery().ToListAsync(cancellationToken);
    }
}
