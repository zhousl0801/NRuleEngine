using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Scenarios;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Scenarios;

/// <summary>
/// 查询宿主注册的应用及场景契约。
/// </summary>
[Authorize]
[Area("nrule-engine")]
[Route("api/nrule-engine/catalog")]
public sealed class RuleCatalogController : AbpControllerBase
{
    private readonly IRuleCatalogAppService _service;

    /// <summary>注入只读场景目录应用服务。</summary>
    public RuleCatalogController(IRuleCatalogAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取可用应用列表。
    /// </summary>
    [HttpGet("apps")]
    public Task<IReadOnlyList<RuleAppDescriptor>> GetAppsAsync()
    {
        return _service.GetAppsAsync();
    }

    /// <summary>
    /// 获取指定应用下的场景契约。
    /// </summary>
    [HttpGet("apps/{appCode}/scenarios")]
    public Task<IReadOnlyList<RuleScenarioDescriptor>> GetScenariosAsync(string appCode)
    {
        return _service.GetScenariosAsync(appCode);
    }

    /// <summary>
    /// 获取指定版本的场景契约。
    /// </summary>
    [HttpGet("apps/{appCode}/scenarios/{scenarioCode}/versions/{contractVersion}")]
    public Task<RuleScenarioDescriptor> GetScenarioAsync(string appCode, string scenarioCode, string contractVersion)
    {
        return _service.GetScenarioAsync(appCode, scenarioCode, contractVersion);
    }

    /// <summary>获取应用的条件判定场景契约。</summary>
    [HttpGet("apps/{appCode}/condition-scenarios")]
    public Task<IReadOnlyList<ConditionScenarioDescriptor>> GetConditionScenariosAsync(string appCode)
    {
        return _service.GetConditionScenariosAsync(appCode);
    }

    /// <summary>按精确版本获取条件判定场景契约。</summary>
    [HttpGet("apps/{appCode}/condition-scenarios/{scenarioCode}/versions/{contractVersion}")]
    public Task<ConditionScenarioDescriptor> GetConditionScenarioAsync(string appCode, string scenarioCode, string contractVersion)
    {
        return _service.GetConditionScenarioAsync(appCode, scenarioCode, contractVersion);
    }
}
