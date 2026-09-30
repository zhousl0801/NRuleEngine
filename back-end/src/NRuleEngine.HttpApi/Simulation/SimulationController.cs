using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.Simulation;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Simulation;

/// <summary>按指定草稿执行一次试算。</summary>
[Authorize(NRuleEnginePermissions.Manage)]
[Area("nrule-engine")]
[Route("api/nrule-engine/models/revisions/simulate")]
public sealed class SimulationController : AbpControllerBase
{
    private readonly IRuleSimulationAppService _simulation;

    public SimulationController(IRuleSimulationAppService service)
    {
        _simulation = service;
    }

    /// <summary>试算草稿修订。</summary>
    [HttpPost]
    public Task<SimulationResultDto> SimulateAsync([FromBody] SimulateRuleDto input)
    {
        return _simulation.SimulateAsync(input);
    }
}
