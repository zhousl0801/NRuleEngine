using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Simulation;

/// <summary>试算指定草稿，不影响活动修订。</summary>
public interface IRuleSimulationAppService : IApplicationService
{
    /// <summary>按场景上下文和入口值试算，不修改修订或发布状态。</summary>
    Task<SimulationResultDto> SimulateAsync(SimulateRuleDto input);
}
