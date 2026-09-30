using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Conditions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore.Conditions;

/// <summary>按明确候选编码查询条件集。</summary>
public class EfCoreConditionSetRepository : EfCoreRepository<NRuleEngineDbContext, ConditionSet, Guid>, IConditionSetRepository
{
    /// <summary>使用组件 DbContext 提供器创建条件集仓储。</summary>
    public EfCoreConditionSetRepository(IDbContextProvider<NRuleEngineDbContext> provider) : base(provider)
    {
    }

    /// <summary>检查应用内业务编码是否重复。</summary>
    public virtual async Task<bool> ExistsAsync(string appCode, string code)
    {
        return await (await GetDbSetAsync()).AnyAsync(x => x.AppCode == appCode && x.Code == code);
    }

    /// <summary>按场景与明确候选编码批量载入条件集。</summary>
    public virtual async Task<List<ConditionSet>> GetByCodesAsync(string appCode, string scenarioCode,
        IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).Where(x => x.AppCode == appCode &&
                x.ScenarioCode == scenarioCode && codes.Contains(x.Code))
            .ToListAsync(cancellationToken);
    }
}
