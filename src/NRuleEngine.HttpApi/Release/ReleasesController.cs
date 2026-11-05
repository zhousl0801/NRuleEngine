using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Domain.Shared.Release;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Release;

/// <summary>查询规则模型和条件集的跨对象发布历史。</summary>
[Area("nrule-engine")]
[Route("api/nrule-engine/releases")]
public sealed class ReleasesController : AbpControllerBase
{
    private readonly IReleaseHistoryAppService _service;

    /// <summary>注入跨对象发布历史查询服务。</summary>
    public ReleasesController(IReleaseHistoryAppService service)
    {
        _service = service;
    }

    /// <summary>按应用、场景和对象类型列出最近的发布记录。</summary>
    [Authorize(NRuleEnginePermissions.Publish)]
    [HttpGet]
    public Task<IReadOnlyList<ReleaseHistoryDto>> GetListAsync([FromQuery] string appCode,
        [FromQuery] string? scenarioCode, [FromQuery] ReleaseTargetType? targetType,
        [FromQuery] int maxResultCount = 200)
    {
        return _service.GetListAsync(appCode, scenarioCode, targetType, maxResultCount);
    }
}
