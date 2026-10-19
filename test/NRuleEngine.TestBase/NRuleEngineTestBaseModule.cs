using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace NRuleEngine.TestBase;

[DependsOn(typeof(AbpTestBaseModule), typeof(AbpAutofacModule))]
public sealed class NRuleEngineTestBaseModule : AbpModule
{
}
