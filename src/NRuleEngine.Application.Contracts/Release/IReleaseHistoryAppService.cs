using NRuleEngine.Domain.Shared.Release;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>跨对象发布与回滚历史的查询契约。</summary>
public interface IReleaseHistoryAppService : IApplicationService
{
    /// <summary>按应用、可选场景和对象类型读取最近的发布记录。</summary>
    Task<IReadOnlyList<ReleaseHistoryDto>> GetListAsync(string appCode, string? scenarioCode = null,
        ReleaseTargetType? targetType = null, int maxResultCount = 200);
}
