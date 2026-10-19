using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.TestBase;
using Volo.Abp.Modularity;

namespace NRuleEngine.Domain.Tests;

[DependsOn(
    typeof(NRuleEngineDomainModule),
    typeof(NRuleEngineTestBaseModule))]
public sealed class NRuleEngineDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IRuleAppContributor, SimulatedRuleAppContributor>();
        context.Services.AddSingleton<IRuleScenarioContributor, SimulatedSizeExpansionContributor>();
    }
}
