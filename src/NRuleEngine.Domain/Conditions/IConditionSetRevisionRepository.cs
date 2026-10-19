using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Domain.Conditions;

/// <summary>条件修订及其输入快照的持久化查询。</summary>
public interface IConditionSetRevisionRepository : IRepository<ConditionSetRevision, Guid>
{
    /// <summary>读取指定修订及其全部输入快照。</summary>
    Task<ConditionSetRevision?> FindWithInputsAsync(Guid revisionId);
    /// <summary>按修订号倒序读取一个条件集的修订摘要。</summary>
    Task<List<ConditionSetRevision>> GetListBySetAsync(Guid setId);
    /// <summary>批量载入活动候选的修订与输入快照。</summary>
    Task<List<ConditionSetRevision>> GetByIdsWithInputsAsync(IReadOnlyCollection<Guid> revisionIds, CancellationToken cancellationToken = default);
}
