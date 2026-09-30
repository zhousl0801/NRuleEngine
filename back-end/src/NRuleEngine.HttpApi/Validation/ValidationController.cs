using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.Application.Contracts.Validation;
using Volo.Abp.AspNetCore.Mvc;

namespace NRuleEngine.HttpApi.Validation;

/// <summary>检查整份草稿修订。</summary>
[Authorize(NRuleEnginePermissions.Manage)]
[Area("nrule-engine")]
[Route("api/nrule-engine/models/revisions/{revisionId:guid}/validation")]
public sealed class ValidationController : AbpControllerBase
{
    private readonly IRuleValidationAppService _validation;

    public ValidationController(IRuleValidationAppService service)
    {
        _validation = service;
    }

    /// <summary>校验草稿修订。</summary>
    [HttpGet]
    public Task<ValidationReportDto> ValidateAsync(Guid revisionId)
    {
        return _validation.ValidateAsync(revisionId);
    }
}
