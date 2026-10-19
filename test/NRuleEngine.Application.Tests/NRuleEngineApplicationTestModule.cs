using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace NRuleEngine.Application.Tests;

[DependsOn(
    typeof(NRuleEngineApplicationModule),
    typeof(NRuleEngineEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(NRuleEngineTestBaseModule))]
public sealed class NRuleEngineApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nre-app-test-{Guid.NewGuid():N}.sqlite");
        Configure<AbpDbConnectionOptions>(options => options.ConnectionStrings.Default = $"Data Source={path}");
        Configure<AbpDbContextOptions>(options => options.UseSqlite());
        context.Services.AddSingleton<IRuleAppContributor, SimulatedRuleAppContributor>();
        context.Services.AddSingleton<IRuleScenarioContributor, SimulatedSizeExpansionContributor>();
        context.Services.AddSingleton<IConditionScenarioContributor, SimulatedConditionScenarioContributor>();
    }
}
