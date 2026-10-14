using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Domain.RuleModels;

/// <summary>
/// 规则模型的持久化与目录查询。
/// </summary>
public interface IRuleModelRepository : IRepository<RuleModel, Guid>
{
    /// <summary>检查应用内的模型编码是否已存在。</summary>
    Task<bool> ExistsAsync(string appCode, string code);

    /// <summary>按编码排序并按可选场景过滤模型。</summary>
    Task<List<RuleModel>> GetListAsync(string appCode, string? scenarioCode, CancellationToken cancellationToken = default);
}
