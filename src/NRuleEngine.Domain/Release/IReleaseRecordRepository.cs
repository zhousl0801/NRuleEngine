using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Domain.Release;

/// <summary>持久化发布审计并按模型查询历史。</summary>
public interface IReleaseRecordRepository : IRepository<ReleaseRecord, Guid>
{
    /// <summary>按发布时间倒序读取指定模型的完整发布历史。</summary>
    Task<List<ReleaseRecord>> GetListByModelAsync(Guid modelId);
}
