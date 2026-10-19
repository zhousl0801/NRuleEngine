using NRuleEngine.TestBase;
using Volo.Abp.Modularity;

namespace NRuleEngine.EntityFrameworkCore.Tests;

[DependsOn(
    typeof(NRuleEngineEntityFrameworkCoreModule),
    typeof(NRuleEngineTestBaseModule))]
public sealed class NRuleEngineEntityFrameworkCoreTestModule : AbpModule
{
}
