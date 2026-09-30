using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Conditions;
using NRuleEngine.Application.Contracts.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Conditions;

/// <summary>条件集编制、发布与明确候选集判定 API。</summary>
[Area("nrule-engine")]
[Route("api/nrule-engine/condition-sets")]
public sealed class ConditionSetsController : AbpControllerBase
{
    private readonly IConditionSetAppService _service;

    /// <summary>注入条件集应用服务。</summary>
    public ConditionSetsController(IConditionSetAppService service)
    {
        _service = service;
    }

    /// <summary>创建条件集及首个草稿。</summary>
    [HttpPost]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<ConditionSetDto> CreateAsync([FromBody] CreateConditionSetDto input)
    {
        return _service.CreateAsync(input);
    }

    /// <summary>读取条件集身份和活动修订。</summary>
    [HttpGet("{setId:guid}")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<ConditionSetDto> GetAsync(Guid setId)
    {
        return _service.GetAsync(setId);
    }

    /// <summary>列出条件集修订。</summary>
    [HttpGet("{setId:guid}/revisions")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<IReadOnlyList<ConditionRevisionSummaryDto>> GetRevisionsAsync(Guid setId)
    {
        return _service.GetRevisionsAsync(setId);
    }

    /// <summary>读取发布与回滚历史。</summary>
    [HttpGet("{setId:guid}/history")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<IReadOnlyList<ConditionReleaseDto>> GetHistoryAsync(Guid setId)
    {
        return _service.GetHistoryAsync(setId);
    }

    /// <summary>归档条件集。</summary>
    [HttpPost("{setId:guid}/archive")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<ConditionSetDto> ArchiveAsync(Guid setId, [FromBody] ArchiveConditionSetDto input)
    {
        return _service.ArchiveAsync(setId, input);
    }

    /// <summary>读取修订条件和输入稳定键。</summary>
    [HttpGet("revisions/{revisionId:guid}")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<ConditionSetRevisionDto> GetRevisionAsync(Guid revisionId)
    {
        return _service.GetRevisionAsync(revisionId);
    }

    /// <summary>复制条件草稿。</summary>
    [HttpPost("{setId:guid}/drafts")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<ConditionSetRevisionDto> CreateDraftAsync(Guid setId, [FromBody] CreateConditionDraftDto input)
    {
        return _service.CreateDraftAsync(setId, input);
    }

    /// <summary>更新草稿布尔条件。</summary>
    [HttpPut("revisions/{revisionId:guid}/condition")]
    [Authorize(NRuleEnginePermissions.Manage)]
    public Task<ConditionSetRevisionDto> UpdateConditionAsync(Guid revisionId, [FromBody] UpdateConditionDto input)
    {
        return _service.UpdateConditionAsync(revisionId, input);
    }

    /// <summary>通过样本门禁并发布条件修订。</summary>
    [HttpPost("revisions/{revisionId:guid}/publish")]
    [Authorize(NRuleEnginePermissions.Publish)]
    public Task<ConditionSetDto> PublishAsync(Guid revisionId, [FromBody] PublishConditionSetDto input)
    {
        return _service.PublishAsync(revisionId, input);
    }

    /// <summary>重新激活历史发布修订。</summary>
    [HttpPost("{setId:guid}/rollback")]
    [Authorize(NRuleEnginePermissions.Publish)]
    public Task<ConditionSetDto> RollbackAsync(Guid setId, [FromBody] RollbackConditionSetDto input)
    {
        return _service.RollbackAsync(setId, input);
    }

    /// <summary>判定明确指定的候选条件集。</summary>
    [HttpPost("evaluate")]
    [Authorize(NRuleEnginePermissions.Execute)]
    public Task<ConditionEvaluationDto> EvaluateAsync([FromBody] EvaluateConditionSetsDto input)
    {
        return _service.EvaluateAsync(input);
    }
}
