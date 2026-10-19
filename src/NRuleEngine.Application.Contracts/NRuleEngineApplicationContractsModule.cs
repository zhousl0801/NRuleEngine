using NRuleEngine.Domain.Shared;
using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace NRuleEngine.Application.Contracts;

[DependsOn(
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule),
    typeof(NRuleEngineDomainSharedModule))]
public sealed class NRuleEngineApplicationContractsModule : AbpModule
{
}
