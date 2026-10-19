using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Rules;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore;

/// <summary>
/// 规则模型、修订及变量的 EF Core 上下文。
/// </summary>
public sealed class NRuleEngineDbContext(DbContextOptions<NRuleEngineDbContext> options)
    : AbpDbContext<NRuleEngineDbContext>(options), INRuleEngineDbContext
{
    /// <summary>条件集的稳定身份及活动修订指针。</summary>
    public DbSet<ConditionSet> ConditionSets => Set<ConditionSet>();
    /// <summary>条件集的草稿与已发布修订。</summary>
    public DbSet<ConditionSetRevision> ConditionSetRevisions => Set<ConditionSetRevision>();
    /// <summary>各条件修订锁定的输入快照。</summary>
    public DbSet<ConditionInputDefinition> ConditionInputDefinitions => Set<ConditionInputDefinition>();
    /// <summary>条件集的发布与回滚历史。</summary>
    public DbSet<ConditionSetReleaseRecord> ConditionSetReleaseRecords => Set<ConditionSetReleaseRecord>();
    public DbSet<RuleModel> RuleModels => Set<RuleModel>();
    public DbSet<RuleModelRevision> RuleModelRevisions => Set<RuleModelRevision>();
    public DbSet<VariableDefinition> VariableDefinitions => Set<VariableDefinition>();
    public DbSet<RuleDefinition> RuleDefinitions => Set<RuleDefinition>();
    public DbSet<RuleAssignment> RuleAssignments => Set<RuleAssignment>();
    public DbSet<ScenarioBinding> ScenarioBindings => Set<ScenarioBinding>();
    public DbSet<RuleBindingCriterion> RuleBindingCriteria => Set<RuleBindingCriterion>();
    public DbSet<ReleaseGateCase> ReleaseGateCases => Set<ReleaseGateCase>();
    public DbSet<ReleaseRecord> ReleaseRecords => Set<ReleaseRecord>();
    public DbSet<ReleaseScope> ReleaseScopes => Set<ReleaseScope>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureNRuleEngine();
    }
}
