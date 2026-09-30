using NRuleEngine.Application.Contracts.Scenarios;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Scenarios;

/// <summary>
/// 向管理端提供已注册应用与场景契约的只读查询。
/// </summary>
public class RuleCatalogAppService : ApplicationService, IRuleCatalogAppService
{
    private readonly IRuleCatalog _catalog;

    /// <summary>注入已完成注册的只读规则目录。</summary>
    public RuleCatalogAppService(IRuleCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>获取所有已注册应用。</summary>
    public virtual Task<IReadOnlyList<RuleAppDescriptor>> GetAppsAsync()
    {
        return Task.FromResult(_catalog.GetApps());
    }

    /// <summary>获取应用的赋值型规则场景契约。</summary>
    public virtual Task<IReadOnlyList<RuleScenarioDescriptor>> GetScenariosAsync(string appCode)
    {
        return Task.FromResult(_catalog.GetScenarios(appCode));
    }

    /// <summary>按精确版本获取赋值型规则场景契约。</summary>
    public virtual Task<RuleScenarioDescriptor> GetScenarioAsync(string appCode, string scenarioCode, string contractVersion)
    {
        return Task.FromResult(_catalog.GetScenario(appCode, scenarioCode, contractVersion));
    }

    /// <summary>获取应用注册的全部条件判定场景版本。</summary>
    public virtual Task<IReadOnlyList<ConditionScenarioDescriptor>> GetConditionScenariosAsync(string appCode)
    {
        return Task.FromResult(_catalog.GetConditionScenarios(appCode));
    }

    /// <summary>按精确版本获取条件判定场景契约。</summary>
    public virtual Task<ConditionScenarioDescriptor> GetConditionScenarioAsync(string appCode, string scenarioCode,
        string contractVersion)
    {
        return Task.FromResult(_catalog.GetConditionScenario(appCode, scenarioCode, contractVersion));
    }
}
