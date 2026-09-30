using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Domain.Scenarios;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace NRuleEngine.Domain;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(NRuleEngineDomainSharedModule))]
public sealed class NRuleEngineDomainModule : AbpModule
{
    /// <summary>装配领域仓储和代码注册目录所需的服务。</summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<RuleCatalog>();
        context.Services.AddSingleton<IRuleCatalog>(provider => provider.GetRequiredService<RuleCatalog>());
    }

    /// <summary>在宿主初始化时一次性冻结应用与场景契约目录。</summary>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<RuleCatalog>().Initialize(
            context.ServiceProvider.GetServices<IRuleAppContributor>(),
            context.ServiceProvider.GetServices<IRuleScenarioContributor>(),
            context.ServiceProvider.GetServices<IConditionScenarioContributor>());
    }
}
