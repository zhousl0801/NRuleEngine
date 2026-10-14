using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.RuleModels;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore.RuleModels;

/// <summary>
/// EF Core 规则模型仓储。
/// </summary>
public class EfCoreRuleModelRepository : EfCoreRepository<NRuleEngineDbContext, RuleModel, Guid>, IRuleModelRepository
{
    public EfCoreRuleModelRepository(IDbContextProvider<NRuleEngineDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> ExistsAsync(string appCode, string code)
    {
        return await (await GetDbSetAsync()).AnyAsync(x => x.AppCode == appCode && x.Code == code);
    }

    public virtual async Task<List<RuleModel>> GetListAsync(string appCode, string? scenarioCode, CancellationToken cancellationToken = default)
    {
        var query = (await GetDbSetAsync()).Where(x => x.AppCode == appCode);
        if (scenarioCode is not null)
        {
            query = query.Where(x => x.ScenarioCode == scenarioCode);
        }

        return await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
    }
}
