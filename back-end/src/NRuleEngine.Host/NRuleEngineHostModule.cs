using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.HttpApi;
using NRuleEngine.Host.Scenarios;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace NRuleEngine.Host;

/// <summary>仅供本机前端联调的 ABP 宿主。</summary>
[DependsOn(
    typeof(NRuleEngineApplicationModule),
    typeof(NRuleEngineEntityFrameworkCoreModule),
    typeof(NRuleEngineHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCoreSqliteModule))]
public sealed class NRuleEngineHostModule : AbpModule
{
    public const string CorsPolicyName = "LocalFrontend";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<AbpDbConnectionOptions>(options =>
            options.ConnectionStrings.Default = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is required."));
        Configure<AbpDbContextOptions>(options => options.UseSqlite());

        context.Services.AddSingleton<IRuleAppContributor, DemoRuleAppContributor>();
        context.Services.AddSingleton<IRuleScenarioContributor, DemoRuleScenarioContributor>();
        context.Services.AddSingleton<IConditionScenarioContributor, DemoConditionScenarioContributor>();

        context.Services.AddAuthentication(LocalDevelopmentAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, LocalDevelopmentAuthenticationHandler>(LocalDevelopmentAuthenticationHandler.SchemeName, _ => { });
        context.Services.AddAlwaysAllowAuthorization();

        var origins = configuration.GetSection("Cors:Origins").GetChildren().Select(origin => origin.Value!).ToArray();
        context.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
    }
}
