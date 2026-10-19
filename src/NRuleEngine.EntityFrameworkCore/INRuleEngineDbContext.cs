using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using Volo.Abp.EntityFrameworkCore;

namespace NRuleEngine.EntityFrameworkCore;

/// <summary>
/// 供组件仓储访问的数据库上下文契约。
/// </summary>
public interface INRuleEngineDbContext : IEfCoreDbContext
{
    /// <summary>条件集的稳定身份及活动修订指针。</summary>
    DbSet<ConditionSet> ConditionSets { get; }
    /// <summary>条件集的草稿与已发布修订。</summary>
    DbSet<ConditionSetRevision> ConditionSetRevisions { get; }
    /// <summary>各条件修订锁定的输入快照。</summary>
    DbSet<ConditionInputDefinition> ConditionInputDefinitions { get; }
    /// <summary>条件集的发布与回滚历史。</summary>
    DbSet<ConditionSetReleaseRecord> ConditionSetReleaseRecords { get; }
    DbSet<RuleModel> RuleModels { get; }
    DbSet<RuleModelRevision> RuleModelRevisions { get; }
    DbSet<VariableDefinition> VariableDefinitions { get; }
    DbSet<ReleaseGateCase> ReleaseGateCases { get; }
    DbSet<ReleaseRecord> ReleaseRecords { get; }
    DbSet<ReleaseScope> ReleaseScopes { get; }
}
