using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Rules;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace NRuleEngine.EntityFrameworkCore;

/// <summary>
/// 规则模型、修订和变量的 EF Core 映射。
/// </summary>
public static class NRuleEngineModelBuilderExtensions
{
    public static void ConfigureNRuleEngine(this ModelBuilder builder)
    {
        builder.Entity<ConditionSet>(entity =>
        {
            entity.ToTable("NreConditionSets");
            entity.ConfigureByConvention();
            entity.Property(x => x.AppCode).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ScenarioCode).IsRequired().HasMaxLength(128);
            entity.Property(x => x.InitialContractVersion).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(2048);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.AppCode, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.AppCode, x.ScenarioCode, x.Status });
        });

        builder.Entity<ConditionSetRevision>(entity =>
        {
            entity.ToTable("NreConditionSetRevisions");
            entity.ConfigureByConvention();
            entity.Property(x => x.ScenarioCode).IsRequired().HasMaxLength(128);
            entity.Property(x => x.ContractVersion).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ConditionSource).HasMaxLength(8192);
            entity.Property(x => x.ContentHash).HasMaxLength(128);
            entity.Property(x => x.ChangeSummary).HasMaxLength(2048);
            entity.HasIndex(x => new { x.ConditionSetId, x.RevisionNo }).IsUnique();
            entity.HasOne<ConditionSet>().WithMany().HasForeignKey(x => x.ConditionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Inputs).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Inputs).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ConditionInputDefinition>(entity =>
        {
            entity.ToTable("NreConditionInputDefinitions");
            entity.ConfigureByConvention();
            entity.Property(x => x.Code).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.ValueKind).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.EnumCode).HasMaxLength(128);
            entity.HasIndex(x => new { x.RevisionId, x.StableKey }).IsUnique();
            entity.HasIndex(x => new { x.RevisionId, x.Code }).IsUnique();
        });

        builder.Entity<ConditionSetReleaseRecord>(entity =>
        {
            entity.ToTable("NreConditionSetReleaseRecords");
            entity.ConfigureByConvention();
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ContentHash).IsRequired().HasMaxLength(128);
            entity.Property(x => x.ChangeSummary).HasMaxLength(2048);
            entity.Property(x => x.GateCasesJson);
            entity.HasIndex(x => new { x.ConditionSetId, x.ReleaseNo }).IsUnique();
            entity.HasOne<ConditionSet>().WithMany().HasForeignKey(x => x.ConditionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ConditionSetRevision>().WithMany().HasForeignKey(x => x.ActivatedRevisionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RuleModel>(entity =>
        {
            entity.ToTable("NreRuleModels");
            entity.ConfigureByConvention();
            entity.Property(x => x.AppCode).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ScenarioCode).IsRequired().HasMaxLength(128);
            entity.Property(x => x.InitialContractVersion).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(2048);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.AppCode, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.AppCode, x.ScenarioCode, x.Status });
        });

        builder.Entity<RuleModelRevision>(entity =>
        {
            entity.ToTable("NreRuleModelRevisions");
            entity.ConfigureByConvention();
            entity.Property(x => x.ScenarioCode).IsRequired().HasMaxLength(128);
            entity.Property(x => x.ContractVersion).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ResultCardinality).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ExecutionMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.MultipleWritePolicy).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ContentHash).HasMaxLength(128);
            entity.Property(x => x.ChangeSummary).HasMaxLength(2048);
            entity.HasIndex(x => new { x.RuleModelId, x.RevisionNo }).IsUnique();
            entity.HasOne<RuleModel>().WithMany().HasForeignKey(x => x.RuleModelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Variables).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Variables).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Rules).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Bindings).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Bindings).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.GateCases).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.GateCases).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<VariableDefinition>(entity =>
        {
            entity.ToTable("NreVariableDefinitions");
            entity.ConfigureByConvention();
            entity.Property(x => x.Code).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Origin).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ValueKind).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Shape).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.EnumCode).HasMaxLength(128);
            entity.Property(x => x.DefaultValueJson).HasMaxLength(4096);
            entity.Property(x => x.Unit).HasMaxLength(64);
            entity.HasIndex(x => new { x.RevisionId, x.StableKey }).IsUnique();
            entity.HasIndex(x => new { x.RevisionId, x.Code }).IsUnique();
        });

        builder.Entity<RuleDefinition>(entity =>
        {
            entity.ToTable("NreRuleDefinitions");
            entity.ConfigureByConvention();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.ConditionSource).HasMaxLength(8192);
            entity.Property(x => x.ConditionAstJson).IsRequired();
            entity.HasIndex(x => new { x.RevisionId, x.Priority, x.SortOrder }).IsUnique();
            entity.HasMany(x => x.Assignments).WithOne().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<RuleAssignment>(entity =>
        {
            entity.ToTable("NreRuleAssignments");
            entity.ConfigureByConvention();
            entity.Property(x => x.ValueSourceKind).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ConstantValueJson).HasMaxLength(4096);
            entity.Property(x => x.ExpressionSource).HasMaxLength(8192);
            entity.HasIndex(x => new { x.RuleId, x.TargetVariableStableKey }).IsUnique();
            entity.HasIndex(x => new { x.RuleId, x.SortOrder }).IsUnique();
        });

        builder.Entity<ScenarioBinding>(entity =>
        {
            entity.ToTable("NreScenarioBindings");
            entity.ConfigureByConvention();
            entity.Property(x => x.AppCode).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ScenarioCode).IsRequired().HasMaxLength(128);
            entity.Property(x => x.ContractVersion).IsRequired().HasMaxLength(64);
            entity.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.BindingId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<RuleBindingCriterion>(entity =>
        {
            entity.ToTable("NreRuleBindingCriteria");
            entity.ConfigureByConvention();
            entity.Property(x => x.DimensionCode).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Operator).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.TypedValueJson).IsRequired().HasMaxLength(4096);
            entity.HasIndex(x => new { x.BindingId, x.DimensionCode }).IsUnique();
        });

        builder.Entity<ReleaseGateCase>(entity =>
        {
            entity.ToTable("NreReleaseGateCases");
            entity.ConfigureByConvention();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.ContextJson).IsRequired();
            entity.Property(x => x.InputsJson).IsRequired();
            entity.Property(x => x.ExpectedResultsJson).IsRequired();
            entity.Property(x => x.ExpectedStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.RevisionId, x.Name }).IsUnique();
        });

        builder.Entity<ReleaseRecord>(entity =>
        {
            entity.ToTable("NreReleaseRecords");
            entity.ConfigureByConvention();
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ContentHash).IsRequired().HasMaxLength(128);
            entity.Property(x => x.ValidationSummary).IsRequired().HasMaxLength(512);
            entity.Property(x => x.ChangeSummary).HasMaxLength(2048);
            entity.HasIndex(x => new { x.RuleModelId, x.ReleaseNo }).IsUnique();
            entity.HasOne<RuleModel>().WithMany().HasForeignKey(x => x.RuleModelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RuleModelRevision>().WithMany().HasForeignKey(x => x.ActivatedRevisionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReleaseScope>(entity =>
        {
            entity.ToTable("NreReleaseScopes");
            entity.ConfigureByConvention();
            entity.Property(x => x.AppCode).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ScenarioCode).IsRequired().HasMaxLength(128);
            entity.HasIndex(x => new { x.AppCode, x.ScenarioCode }).IsUnique();
        });
    }
}
