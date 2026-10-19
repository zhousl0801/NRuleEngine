using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Domain;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.EntityFrameworkCore.Conditions;
using NRuleEngine.EntityFrameworkCore.Release;
using NRuleEngine.EntityFrameworkCore.RuleModels;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace NRuleEngine.EntityFrameworkCore;

[DependsOn(
    typeof(AbpEntityFrameworkCoreModule),
    typeof(NRuleEngineDomainModule))]
public sealed class NRuleEngineEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<NRuleEngineDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<RuleModel, EfCoreRuleModelRepository>();
            options.AddRepository<ConditionSet, EfCoreConditionSetRepository>();
            options.AddRepository<ConditionSetRevision, EfCoreConditionSetRevisionRepository>();
            options.AddRepository<RuleModelRevision, EfCoreRuleModelRevisionRepository>();
            options.AddRepository<ReleaseScope, EfCoreReleaseScopeRepository>();
            options.AddRepository<ReleaseRecord, EfCoreReleaseRecordRepository>();
        });
    }
}
