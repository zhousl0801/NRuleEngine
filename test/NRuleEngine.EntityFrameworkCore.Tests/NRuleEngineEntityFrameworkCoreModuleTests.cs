using NRuleEngine.TestBase;
using Xunit;

namespace NRuleEngine.EntityFrameworkCore.Tests;

public sealed class NRuleEngineEntityFrameworkCoreModuleTests : NRuleEngineTestBase<NRuleEngineEntityFrameworkCoreTestModule>
{
    [Fact]
    public void ShouldInitializeEntityFrameworkCoreModule()
    {
        Assert.NotNull(ServiceProvider);
    }
}
