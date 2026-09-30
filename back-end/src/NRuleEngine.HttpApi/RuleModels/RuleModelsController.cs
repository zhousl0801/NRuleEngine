using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.RuleModels;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.RuleModels;

/// <summary>
/// 管理规则模型、草稿修订及中间变量。
/// </summary>
[Authorize(NRuleEnginePermissions.Manage)]
[Area("nrule-engine")]
[Route("api/nrule-engine/models")]
public sealed class RuleModelsController : AbpControllerBase
{
    private readonly IRuleModelAppService _service;

    public RuleModelsController(IRuleModelAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 创建模型及首个草稿。
    /// </summary>
    [HttpPost]
    public Task<RuleModelDto> CreateAsync([FromBody] CreateRuleModelDto input)
    {
        return _service.CreateAsync(input);
    }

    /// <summary>
    /// 获取模型详情。
    /// </summary>
    [HttpGet("{modelId:guid}")]
    public Task<RuleModelDto> GetAsync(Guid modelId)
    {
        return _service.GetAsync(modelId);
    }

    /// <summary>
    /// 按应用和可选场景查询模型。
    /// </summary>
    [HttpGet]
    public Task<IReadOnlyList<RuleModelDto>> GetListAsync([FromQuery] string appCode, [FromQuery] string? scenarioCode)
    {
        return _service.GetListAsync(appCode, scenarioCode);
    }

    /// <summary>
    /// 归档规则模型。
    /// </summary>
    [HttpPost("{modelId:guid}/archive")]
    public Task<RuleModelDto> ArchiveAsync(Guid modelId, [FromBody] ArchiveRuleModelDto input)
    {
        return _service.ArchiveAsync(modelId, input);
    }

    /// <summary>
    /// 从源修订创建草稿。
    /// </summary>
    [HttpPost("{modelId:guid}/drafts")]
    public Task<RuleRevisionDto> CreateDraftAsync(Guid modelId, [FromBody] CreateDraftDto input)
    {
        return _service.CreateDraftAsync(modelId, input);
    }

    /// <summary>
    /// 获取修订及变量详情。
    /// </summary>
    [HttpGet("revisions/{revisionId:guid}")]
    public Task<RuleRevisionDto> GetRevisionAsync(Guid revisionId)
    {
        return _service.GetRevisionAsync(revisionId);
    }

    /// <summary>
    /// 获取模型的修订摘要列表。
    /// </summary>
    [HttpGet("{modelId:guid}/revisions")]
    public Task<IReadOnlyList<RuleRevisionSummaryDto>> GetRevisionsAsync(Guid modelId)
    {
        return _service.GetRevisionsAsync(modelId);
    }

    /// <summary>
    /// 在草稿中增加中间变量。
    /// </summary>
    [HttpPost("revisions/{revisionId:guid}/intermediate-variables")]
    public Task<RuleRevisionDto> AddIntermediateVariableAsync(Guid revisionId, [FromBody] AddIntermediateVariableDto input)
    {
        return _service.AddIntermediateVariableAsync(revisionId, input);
    }

    /// <summary>
    /// 重命名草稿中的中间变量。
    /// </summary>
    [HttpPut("revisions/{revisionId:guid}/intermediate-variables/{stableKey:guid}")]
    public Task<RuleRevisionDto> RenameIntermediateVariableAsync(Guid revisionId, Guid stableKey,
        [FromBody] RenameIntermediateVariableDto input)
    {
        return _service.RenameIntermediateVariableAsync(revisionId, stableKey, input);
    }

    /// <summary>设置草稿执行语义。</summary>
    [HttpPut("revisions/{revisionId:guid}/execution-semantics")]
    public Task<RuleRevisionDto> UpdateExecutionSemanticsAsync(Guid revisionId,
        [FromBody] UpdateExecutionSemanticsDto input)
    {
        return _service.UpdateExecutionSemanticsAsync(revisionId, input);
    }

}
