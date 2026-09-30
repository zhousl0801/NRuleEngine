using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Validation;

/// <summary>校验整份草稿修订。</summary>
public interface IRuleValidationAppService : IApplicationService
{
    /// <summary>校验修订中的规则、依赖、必需出口与绑定。</summary>
    Task<ValidationReportDto> ValidateAsync(Guid revisionId);
}
