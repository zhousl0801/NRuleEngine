using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Domain.Conditions;

/// <summary>条件集目录查询。</summary>
public interface IConditionSetRepository : IRepository<ConditionSet, Guid>
{
    /// <summary>检查应用内是否已有相同业务编码。</summary>
    Task<bool> ExistsAsync(string appCode, string code);
    /// <summary>只读取调用方明确指定的候选编码。</summary>
    Task<List<ConditionSet>> GetByCodesAsync(string appCode, string scenarioCode, IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);
}
