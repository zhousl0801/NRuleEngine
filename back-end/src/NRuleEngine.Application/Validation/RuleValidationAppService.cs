using NRuleEngine.Application.Contracts.Validation;
using NRuleEngine.Domain.Validation;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Validation;

/// <summary>加载草稿及锁定的场景契约，执行完整校验。</summary>
public class RuleValidationAppService : ApplicationService, IRuleValidationAppService
{
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;
    private readonly IRuleCatalog _catalog;

    public RuleValidationAppService(IRuleModelRepository models, IRuleModelRevisionRepository revisions,
        IRuleCatalog catalog)
    {
        _models = models;
        _revisions = revisions;
        _catalog = catalog;
    }

    /// <summary>载入完整修订与锁定契约，返回带对象路径的问题清单。</summary>
    public virtual async Task<ValidationReportDto> ValidateAsync(Guid revisionId)
    {
        var revision = await _revisions.FindWithVariablesAsync(revisionId)
            ?? throw new ArgumentException("Revision does not exist.", nameof(revisionId));
        var model = await _models.GetAsync(revision.RuleModelId);
        var contract = _catalog.GetScenario(model.AppCode, revision.ScenarioCode, revision.ContractVersion);
        var issues = RuleRevisionValidator.Validate(revision, contract);
        return new ValidationReportDto
        {
            RevisionId = revisionId,
            IsValid = issues.Count == 0,
            Issues = issues.Select(i => new ValidationIssueDto
            {
                Code = i.Code, Path = i.Path, Message = i.Message, Start = i.Start, Length = i.Length
            }).ToArray()
        };
    }
}
