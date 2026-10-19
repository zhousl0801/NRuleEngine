using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace NRuleEngine.Domain.Shared;

[DependsOn(typeof(AbpDddDomainSharedModule))]
public sealed class NRuleEngineDomainSharedModule : AbpModule
{
}
