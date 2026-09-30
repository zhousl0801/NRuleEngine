using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Release;

/// <summary>维护发布门禁、发布草稿并查询或回滚历史版本。</summary>
[Area("nrule-engine")]
[Route("api/nrule-engine/models")]
public sealed class ReleaseController : AbpControllerBase
{
    private readonly IRuleReleaseAppService _release;

    public ReleaseController(IRuleReleaseAppService release)
    {
        _release = release;
    }

    /// <summary>为草稿增加发布回归用例。</summary>
    [Authorize(NRuleEnginePermissions.Manage)]
    [HttpPost("revisions/{revisionId:guid}/gate-cases")]
    public Task<RuleRevisionDto> AddGateCaseAsync(Guid revisionId, [FromBody] AddReleaseGateCaseDto input)
    {
        return _release.AddGateCaseAsync(revisionId, input);
    }

    /// <summary>删除草稿发布回归用例。</summary>
    [Authorize(NRuleEnginePermissions.Manage)]
    [HttpDelete("revisions/{revisionId:guid}/gate-cases/{gateCaseId:guid}")]
    public Task<RuleRevisionDto> RemoveGateCaseAsync(Guid revisionId, Guid gateCaseId,
        [FromBody] DeleteDraftItemDto input)
    {
        return _release.RemoveGateCaseAsync(revisionId, gateCaseId, input);
    }

    /// <summary>发布通过完整校验和门禁的草稿。</summary>
    [Authorize(NRuleEnginePermissions.Publish)]
    [HttpPost("revisions/{revisionId:guid}/publish")]
    public Task<ReleaseDto> PublishAsync(Guid revisionId, [FromBody] PublishRuleDto input)
    {
        return _release.PublishAsync(revisionId, input);
    }

    /// <summary>重新激活模型的历史发布修订。</summary>
    [Authorize(NRuleEnginePermissions.Publish)]
    [HttpPost("{modelId:guid}/rollback")]
    public Task<ReleaseDto> RollbackAsync(Guid modelId, [FromBody] RollbackRuleDto input)
    {
        return _release.RollbackAsync(modelId, input);
    }

    /// <summary>列出模型发布与回滚的审计记录。</summary>
    [Authorize(NRuleEnginePermissions.Publish)]
    [HttpGet("{modelId:guid}/releases")]
    public Task<IReadOnlyList<ReleaseDto>> GetHistoryAsync(Guid modelId)
    {
        return _release.GetHistoryAsync(modelId);
    }
}
