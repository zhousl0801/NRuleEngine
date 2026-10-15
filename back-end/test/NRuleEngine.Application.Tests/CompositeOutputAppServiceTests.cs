using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Application.Contracts.Simulation;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class CompositeOutputAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task PersistsCopiesPublishesAndExecutesStructuredOutputs()
    {
        using (var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            await uow.CompleteAsync();
        }
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var authoring = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var model = await models.CreateAsync(new CreateRuleModelDto("TEST-ODS", "composite-output", "1.0", "composite", "区间与候选", null));
        var revision = await models.GetRevisionAsync((await models.GetRevisionsAsync(model.Id)).Single().Id);
        Assert.Equal(RuleValueShape.NumericRange, revision.Variables.Single(v => v.Code == "range").Type.Shape);
        Assert.Equal(RuleValueShape.List, revision.Variables.Single(v => v.Code == "numbers").Type.Shape);
        var widthKey = revision.Variables.Single(v => v.Code == "width").StableKey;
        revision = await authoring.AddRuleAsync(revision.Id, new AddRuleDto
        {
            Name = "区间与候选", Priority = 1, SortOrder = 1, IsEnabled = true,
            ConditionAstJson = Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        var ruleId = revision.Rules.Single().Id;
        revision = await authoring.AddAssignmentAsync(revision.Id, ruleId, new AddAssignmentDto
        {
            TargetVariableStableKey = revision.Variables.Single(v => v.Code == "range").StableKey,
            ValueSourceKind = RuleValueSourceKind.Expression, SortOrder = 1,
            ExpressionAstJson = Ast(new
            {
                op = "range", args = new object[]
                {
                    new { op = "variable", stableKey = widthKey },
                    new { op = "add", args = new object[] { new { op = "variable", stableKey = widthKey }, new { op = "constant", kind = RuleValueKind.Decimal, value = 10 } } }
                }
            }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await authoring.AddAssignmentAsync(revision.Id, ruleId, new AddAssignmentDto
        {
            TargetVariableStableKey = revision.Variables.Single(v => v.Code == "numbers").StableKey,
            ValueSourceKind = RuleValueSourceKind.Expression, SortOrder = 2,
            ExpressionAstJson = Ast(new { op = "list", args = new object[]
            {
                new { op = "constant", kind = RuleValueKind.Decimal, value = 130 },
                new { op = "variable", stableKey = widthKey }
            } }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await authoring.AddAssignmentAsync(revision.Id, ruleId, new AddAssignmentDto
        {
            TargetVariableStableKey = revision.Variables.Single(v => v.Code == "codes").StableKey,
            ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = "[\"B\",\"A\"]", SortOrder = 3,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await ServiceProvider.GetRequiredService<IRuleBindingAppService>().AddBindingAsync(revision.Id, new AddBindingDto
        {
            Priority = 1, ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        const string expected = "{\"range\":{\"min\":120,\"max\":130},\"numbers\":[130,120],\"codes\":[\"B\",\"A\"]}";
        revision = await release.AddGateCaseAsync(revision.Id, new AddReleaseGateCaseDto
        {
            Name = "完整结构", ContextJson = "{}", InputsJson = "{\"width\":120}", ExpectedOutputsJson = expected,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        var simulation = await ServiceProvider.GetRequiredService<IRuleSimulationAppService>().SimulateAsync(new SimulateRuleDto
        {
            RevisionId = revision.Id, InputsJson = new Dictionary<string, string> { ["width"] = "120" }
        });
        Assert.True(simulation.Success, simulation.ErrorMessage);
        Assert.Equal("[130,120]", simulation.OutputsJson["numbers"]);
        await release.PublishAsync(revision.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(model.Id)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        var result = await ServiceProvider.GetRequiredService<IRuleExecutionAppService>().ExecuteAsync(new ExecuteRuleDto
        {
            AppCode = "TEST-ODS", ScenarioCode = "composite-output",
            Inputs = new Dictionary<string, JsonElement> { ["width"] = JsonSerializer.SerializeToElement(120m) }
        });
        Assert.Equal(RuleExecutionStatus.Succeeded, result.Status);
        Assert.Equal(120, result.Outputs["range"].GetProperty("min").GetDecimal());
        Assert.Equal(130, result.Outputs["range"].GetProperty("max").GetDecimal());
        Assert.Equal(new[] { 130m, 120m }, result.Outputs["numbers"].EnumerateArray().Select(v => v.GetDecimal()).ToArray());
        Assert.Equal(new[] { "B", "A" }, result.Outputs["codes"].EnumerateArray().Select(v => v.GetString()).ToArray());
        var wireResult = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(JsonValueKind.Array, wireResult.GetProperty("outputs").GetProperty("numbers").ValueKind);
        var duplicate = await ServiceProvider.GetRequiredService<IRuleExecutionAppService>().ExecuteAsync(new ExecuteRuleDto
        {
            AppCode = "TEST-ODS", ScenarioCode = "composite-output",
            Inputs = new Dictionary<string, JsonElement> { ["width"] = JsonSerializer.SerializeToElement(130m) }
        });
        Assert.Equal(RuleExecutionStatus.Failed, duplicate.Status);
        Assert.Equal(NRuleEngine.Domain.Shared.NRuleEngineErrorCodes.ExpressionEvaluationFailed, duplicate.Error!.Code);
        Assert.Contains("assignments/", duplicate.Error.Data["ErrorPath"]!.ToString());
        Assert.Empty(duplicate.Outputs);
        var draft = await models.CreateDraftAsync(model.Id, new CreateDraftDto(revision.Id, (await models.GetAsync(model.Id)).ConcurrencyStamp));
        Assert.Equal(RuleValueShape.NumericRange, draft.Variables.Single(v => v.Code == "range").Type.Shape);
        Assert.Equal(revision.Variables.Select(v => v.StableKey).Order(), draft.Variables.Select(v => v.StableKey).Order());
    }

    private static string Ast(object root)
    {
        return JsonSerializer.Serialize(new { version = 1, root });
    }
}
