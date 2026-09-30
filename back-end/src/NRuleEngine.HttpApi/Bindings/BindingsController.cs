using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.RuleModels;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Bindings;

/// <summary>管理草稿场景绑定。</summary>
[Authorize(NRuleEnginePermissions.Manage)]
[Area("nrule-engine")]
[Route("api/nrule-engine/models/revisions/{revisionId:guid}/bindings")]
public sealed class BindingsController : AbpControllerBase
{
    private readonly IRuleBindingAppService _bindings;

    public BindingsController(IRuleBindingAppService service)
    {
        _bindings = service;
    }

    /// <summary>新增草稿绑定。</summary>
    [HttpPost]
    public Task<RuleRevisionDto> AddBindingAsync(Guid revisionId, [FromBody] AddBindingDto input)
    {
        return _bindings.AddBindingAsync(revisionId, input);
    }

    /// <summary>修改草稿绑定。</summary>
    [HttpPut("{bindingId:guid}")]
    public Task<RuleRevisionDto> UpdateBindingAsync(Guid revisionId, Guid bindingId, [FromBody] AddBindingDto input)
    {
        return _bindings.UpdateBindingAsync(revisionId, bindingId, input);
    }

    /// <summary>删除草稿绑定。</summary>
    [HttpDelete("{bindingId:guid}")]
    public Task<RuleRevisionDto> RemoveBindingAsync(Guid revisionId, Guid bindingId, [FromBody] DeleteDraftItemDto input)
    {
        return _bindings.RemoveBindingAsync(revisionId, bindingId, input);
    }
}
