using NRuleEngine.Application.Contracts;
using NRuleEngine.Domain;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace NRuleEngine.Application;

[DependsOn(
    typeof(AbpDddApplicationModule),
    typeof(NRuleEngineDomainModule),
    typeof(NRuleEngineApplicationContractsModule))]
public sealed class NRuleEngineApplicationModule : AbpModule
{
}
