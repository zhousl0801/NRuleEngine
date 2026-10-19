using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Runtime;

/// <summary>读取活动发布修订并执行规则的应用契约。</summary>
public interface IRuleExecutionAppService : IApplicationService
{
    /// <summary>返回结构化业务结果；调用方取消原样传播，系统故障通过异常返回。</summary>
    Task<RuleExecutionResultDto> ExecuteAsync(ExecuteRuleDto input, CancellationToken cancellationToken = default);
}
