using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using Xunit;

namespace NRuleEngine.EntityFrameworkCore.Tests;

public sealed class RuleModelMappingTests
{
    [Fact]
    public void MapsDraftVariablesAndEnforcesModelCodeUniqueness()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<NRuleEngineDbContext>()
            .UseSqlite(connection).Options;

        using (var db = new NRuleEngineDbContext(options))
        {
            db.Database.EnsureCreated();
            var contract = TestContract();
            var model = new RuleModel(Guid.NewGuid(), "TEST-ODS", "size-expansion", "1.0", "width", "Width", null);
            var draft = new RuleModelRevision(Guid.NewGuid(), model.Id, model.AllocateRevisionNo(), contract);
            db.RuleModels.Add(model);
            db.RuleModelRevisions.Add(draft);
            db.SaveChanges();
        }

        using (var db = new NRuleEngineDbContext(options))
        {
            var loaded = db.RuleModelRevisions.Include(x => x.Variables).Single();
            Assert.Equal(3, loaded.Variables.Count);
            Assert.All(loaded.Variables, v => Assert.Equal(loaded.Id, v.RevisionId));
            db.RuleModels.Add(new RuleModel(Guid.NewGuid(), "TEST-ODS", "size-expansion", "1.0", "width", "Duplicate", null));
            Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        }
    }

    private static RuleScenarioDescriptor TestContract()
    {
        return new RuleScenarioDescriptor("TEST-ODS", "size-expansion", "1.0",
            [
                new VariableContract("inputWidth", "Input width", RuleVariableDirection.Input,
                    new RuleTypeDescriptor(RuleValueKind.Decimal)),
                new VariableContract("productKind", "Product kind", RuleVariableDirection.Input,
                    new RuleTypeDescriptor(RuleValueKind.Enum, EnumCode: "test-kind"))
            ],
            [new VariableContract("targetWidth", "Target width", RuleVariableDirection.Output,
                new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new BindingDimensionContract("productKind", "Product kind", RuleValueKind.Enum,
                [RuleBindingOperator.Equals])]);
    }
}
