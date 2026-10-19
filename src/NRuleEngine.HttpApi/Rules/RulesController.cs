using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Rules;

/// <summary>管理草稿规则与赋值。</summary>
[Authorize(NRuleEnginePermissions.Manage)]
[Area("nrule-engine")]
[Route("api/nrule-engine/models/revisions/{revisionId:guid}/rules")]
public sealed class RulesController : AbpControllerBase
{
    private readonly IRuleAuthoringAppService _rules;

    public RulesController(IRuleAuthoringAppService service)
    {
        _rules = service;
    }

    /// <summary>新增草稿规则。</summary>
    [HttpPost]
    public Task<RuleRevisionDto> AddRuleAsync(Guid revisionId, [FromBody] AddRuleDto input)
    {
        return _rules.AddRuleAsync(revisionId, input);
    }

    /// <summary>修改草稿规则。</summary>
    [HttpPut("{ruleId:guid}")]
    public Task<RuleRevisionDto> UpdateRuleAsync(Guid revisionId, Guid ruleId, [FromBody] AddRuleDto input)
    {
        return _rules.UpdateRuleAsync(revisionId, ruleId, input);
    }

    /// <summary>删除草稿规则。</summary>
    [HttpDelete("{ruleId:guid}")]
    public Task<RuleRevisionDto> RemoveRuleAsync(Guid revisionId, Guid ruleId, [FromBody] DeleteDraftItemDto input)
    {
        return _rules.RemoveRuleAsync(revisionId, ruleId, input);
    }

    /// <summary>新增规则赋值。</summary>
    [HttpPost("{ruleId:guid}/assignments")]
    public Task<RuleRevisionDto> AddAssignmentAsync(Guid revisionId, Guid ruleId, [FromBody] AddAssignmentDto input)
    {
        return _rules.AddAssignmentAsync(revisionId, ruleId, input);
    }

    /// <summary>修改草稿赋值。</summary>
    [HttpPut("{ruleId:guid}/assignments/{assignmentId:guid}")]
    public Task<RuleRevisionDto> UpdateAssignmentAsync(Guid revisionId, Guid ruleId, Guid assignmentId, [FromBody] AddAssignmentDto input)
    {
        return _rules.UpdateAssignmentAsync(revisionId, ruleId, assignmentId, input);
    }

    /// <summary>删除草稿规则赋值。</summary>
    [HttpDelete("{ruleId:guid}/assignments/{assignmentId:guid}")]
    public Task<RuleRevisionDto> RemoveAssignmentAsync(Guid revisionId, Guid ruleId, Guid assignmentId, [FromBody] DeleteDraftItemDto input)
    {
        return _rules.RemoveAssignmentAsync(revisionId, ruleId, assignmentId, input);
    }
}
