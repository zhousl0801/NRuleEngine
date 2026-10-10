using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Shared.Rules;
using Xunit;

namespace NRuleEngine.EntityFrameworkCore.Tests;

public sealed class ConditionSetMappingTests
{
    [Fact]
    public void PersistsInputSnapshotAndEnforcesStableCodeUniqueness()
    {
        // 保存输入快照后重读，再触发唯一约束验证数据库映射。
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<NRuleEngineDbContext>().UseSqlite(connection).Options;
        var contract = new ConditionScenarioDescriptor("ZT-ODS", "mic-design", "1.0",
            [new VariableContract("THICKNESS", "厚度", RuleVariableDirection.Input,
                new RuleTypeDescriptor(RuleValueKind.Decimal))]);
        using (var db = new NRuleEngineDbContext(options))
        {
            db.Database.EnsureCreated();
            var set = new ConditionSet(Guid.NewGuid(), "ZT-ODS", "mic-design", "1.0", "MIC-1", "MIC-1", null);
            db.ConditionSets.Add(set);
            db.ConditionSetRevisions.Add(new ConditionSetRevision(Guid.NewGuid(), set.Id,
                set.AllocateRevisionNo(), contract));
            db.SaveChanges();
        }
        using (var db = new NRuleEngineDbContext(options))
        {
            var revision = db.ConditionSetRevisions.Include(x => x.Inputs).Single();
            Assert.Equal("THICKNESS", Assert.Single(revision.Inputs).Code);
            Assert.Equal(revision.Id, revision.Inputs.Single().RevisionId);
            db.ConditionSets.Add(new ConditionSet(Guid.NewGuid(), "ZT-ODS", "mic-design", "1.0",
                "MIC-1", "duplicate", null));
            Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        }
    }
}
