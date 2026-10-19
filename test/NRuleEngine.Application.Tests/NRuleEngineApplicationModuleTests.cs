using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts.Permissions;
using NRuleEngine.TestBase;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class NRuleEngineApplicationModuleTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task ShouldInitializeApplicationModule()
    {
        Assert.NotNull(ServiceProvider);
        var definitions = ServiceProvider.GetRequiredService<IPermissionDefinitionManager>();
        Assert.NotNull(await definitions.GetAsync(NRuleEnginePermissions.Manage));
    }
}
