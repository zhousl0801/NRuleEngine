using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.TestBase;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class NRuleEngineDomainModuleTests : NRuleEngineTestBase<NRuleEngineDomainTestModule>
{
    [Fact]
    public void ShouldInitializeDomainModule()
    {
        Assert.NotNull(ServiceProvider);
        Assert.Equal("1.0", ServiceProvider.GetRequiredService<IRuleCatalog>().GetScenario("TEST-ODS", "size-expansion", "1.0").ContractVersion);
    }
}
