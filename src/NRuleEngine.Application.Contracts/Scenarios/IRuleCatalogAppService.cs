using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Scenarios;

/// <summary>
/// 查询代码注册的应用与场景契约。
/// </summary>
public interface IRuleCatalogAppService : IApplicationService
{
    /// <summary>获取所有已注册应用。</summary>
    Task<IReadOnlyList<RuleAppDescriptor>> GetAppsAsync();

    /// <summary>获取指定应用的场景契约。</summary>
    Task<IReadOnlyList<RuleScenarioDescriptor>> GetScenariosAsync(string appCode);

    /// <summary>按场景编码及契约版本获取场景契约。</summary>
    Task<RuleScenarioDescriptor> GetScenarioAsync(string appCode, string scenarioCode, string contractVersion);

    /// <summary>获取指定应用的条件判定场景契约。</summary>
    Task<IReadOnlyList<ConditionScenarioDescriptor>> GetConditionScenariosAsync(string appCode);

    /// <summary>获取指定版本的条件判定场景契约。</summary>
    Task<ConditionScenarioDescriptor> GetConditionScenarioAsync(string appCode, string scenarioCode, string contractVersion);
}
