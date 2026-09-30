using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Domain.RuleModels;

/// <summary>
/// 查询规则修订及其变量定义。
/// </summary>
public interface IRuleModelRevisionRepository : IRepository<RuleModelRevision, Guid>
{
    /// <summary>读取修订及其变量集合；不存在时返回空。</summary>
    Task<RuleModelRevision?> FindWithVariablesAsync(Guid revisionId);

    /// <summary>按修订号倒序读取模型的修订。</summary>
    Task<List<RuleModelRevision>> GetListByModelAsync(Guid modelId);

    /// <summary>一次读取活动模型对应的多份完整修订，避免逐模型数据库查询。</summary>
    Task<List<RuleModelRevision>> GetByIdsWithDetailsAsync(IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default);
}
