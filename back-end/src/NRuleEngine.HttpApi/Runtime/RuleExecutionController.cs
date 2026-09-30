using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.Runtime;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Runtime;

/// <summary>面向接入宿主的已发布规则执行入口。</summary>
[Authorize(NRuleEnginePermissions.Execute)]
[Area("nrule-engine")]
[Route("api/nrule-engine/execute")]
public sealed class RuleExecutionController : AbpControllerBase
{
    private readonly IRuleExecutionAppService _execution;

    public RuleExecutionController(IRuleExecutionAppService execution)
    {
        _execution = execution;
    }

    /// <summary>返回成功、未匹配、拒绝或计算失败的结构化结果。</summary>
    [HttpPost]
    public Task<RuleExecutionResultDto> ExecuteAsync([FromBody] ExecuteRuleDto input,
        CancellationToken cancellationToken)
    {
        return _execution.ExecuteAsync(input, cancellationToken);
    }
}
