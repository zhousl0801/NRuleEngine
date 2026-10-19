using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace NRuleEngine.TestBase;

public abstract class NRuleEngineTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}
