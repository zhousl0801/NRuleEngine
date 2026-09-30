using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Conditions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore.Conditions;

/// <summary>载入条件修订及对应的固定输入快照。</summary>
public class EfCoreConditionSetRevisionRepository
    : EfCoreRepository<NRuleEngineDbContext, ConditionSetRevision, Guid>, IConditionSetRevisionRepository
{
    /// <summary>使用组件 DbContext 提供器创建条件修订仓储。</summary>
    public EfCoreConditionSetRevisionRepository(IDbContextProvider<NRuleEngineDbContext> provider) : base(provider)
    {
    }

    /// <summary>读取修订与输入快照，未找到时返回空。</summary>
    public virtual async Task<ConditionSetRevision?> FindWithInputsAsync(Guid revisionId)
    {
        return await (await GetDbSetAsync()).Include(x => x.Inputs)
            .FirstOrDefaultAsync(x => x.Id == revisionId);
    }

    /// <summary>按修订号倒序返回一个条件集的修订。</summary>
    public virtual async Task<List<ConditionSetRevision>> GetListBySetAsync(Guid setId)
    {
        return await (await GetDbSetAsync()).Where(x => x.ConditionSetId == setId)
            .OrderByDescending(x => x.RevisionNo).ToListAsync();
    }

    /// <summary>一次载入运行候选的修订及输入快照。</summary>
    public virtual async Task<List<ConditionSetRevision>> GetByIdsWithInputsAsync(IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).Where(x => revisionIds.Contains(x.Id)).Include(x => x.Inputs)
            .ToListAsync(cancellationToken);
    }
}
