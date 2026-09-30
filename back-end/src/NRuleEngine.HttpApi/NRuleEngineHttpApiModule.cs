using NRuleEngine.Application.Contracts;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace NRuleEngine.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(NRuleEngineApplicationContractsModule))]
public sealed class NRuleEngineHttpApiModule : AbpModule
{
}
