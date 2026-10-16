using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Application.Contracts.Simulation;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class CollectMatchesAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task PersistsPublishesCopiesAndRollsBackOrderedRowsAndNoMatchGates()
    {
        await InitializeAsync();
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var authoring = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var execution = ServiceProvider.GetRequiredService<IRuleExecutionAppService>();
        var model = await models.CreateAsync(new CreateRuleModelDto("TEST-ODS", "matched-results", "1.0", "rows", "候选结果", null));
        var revision = await models.GetRevisionAsync((await models.GetRevisionsAsync(model.Id)).Single().Id);
        Assert.Equal(RuleResultCardinality.Many, revision.ResultCardinality);
        Assert.Equal(RuleExecutionMode.CollectMatches, revision.ExecutionMode);
        foreach (var value in new[] { 100, 200 })
        {
            revision = await AddRuleAsync(authoring, revision, value);
        }
        revision = await ServiceProvider.GetRequiredService<IRuleBindingAppService>().AddBindingAsync(revision.Id, new AddBindingDto
        {
            Priority = 1, ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await AddGatesAsync(release, revision, 100, 200);
        var simulation = await ServiceProvider.GetRequiredService<IRuleSimulationAppService>().SimulateAsync(new SimulateRuleDto
        {
            RevisionId = revision.Id, InputsJson = new Dictionary<string, string> { ["width"] = "1" }
        });
        Assert.True(simulation.Success, simulation.ErrorMessage);
        Assert.Equal(RuleExecutionStatus.Succeeded, simulation.Status);
        Assert.Equal(new[] { 100m, 200m }, Widths(simulation.Results));
        await PublishAsync(models, release, model.Id, revision);
        var result = await execution.ExecuteAsync(Request(1));
        Assert.Equal(RuleExecutionStatus.Succeeded, result.Status);
        Assert.Equal(new[] { 100m, 200m }, Widths(result.Results));
        Assert.All(result.Results, row => Assert.Single(row.SourceRuleIds));
        var wire = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(wire.TryGetProperty("outputs", out _));
        Assert.Equal(2, wire.GetProperty("results").GetArrayLength());
        Assert.Equal(JsonValueKind.Object, wire.GetProperty("results")[0].GetProperty("outputs").GetProperty("tolerance").ValueKind);
        var none = await execution.ExecuteAsync(Request(0));
        Assert.Equal(RuleExecutionStatus.NotMatched, none.Status);
        Assert.Equal(NRuleEngineErrorCodes.NoMatchingRule, none.Error!.Code);
        Assert.Empty(none.Results);

        var draft = await models.CreateDraftAsync(model.Id, new CreateDraftDto(revision.Id, (await models.GetAsync(model.Id)).ConcurrencyStamp));
        Assert.Equal(RuleResultCardinality.Many, draft.ResultCardinality);
        Assert.Equal(RuleExecutionStatus.NotMatched, draft.GateCases.Single(x => x.Name == "未命中").ExpectedStatus);
        var last = draft.Rules.Last();
        var target = draft.Variables.Single(x => x.Code == "targetWidth").StableKey;
        var assignment = last.Assignments.Single(x => x.TargetVariableStableKey == target);
        draft = await authoring.UpdateAssignmentAsync(draft.Id, last.Id, assignment.Id, new AddAssignmentDto
        {
            TargetVariableStableKey = target, SortOrder = assignment.SortOrder,
            ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = "300",
            ExpectedRevisionConcurrencyStamp = draft.ConcurrencyStamp
        });
        draft = await release.RemoveGateCaseAsync(draft.Id, draft.GateCases.Single(x => x.Name == "两行").Id,
            new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = draft.ConcurrencyStamp });
        draft = await AddPositiveGateAsync(release, draft, 100, 300);
        await PublishAsync(models, release, model.Id, draft);
        Assert.Equal(new[] { 100m, 300m }, Widths((await execution.ExecuteAsync(Request(1))).Results));
        var active = await models.GetAsync(model.Id);
        await release.RollbackAsync(model.Id, new RollbackRuleDto
        {
            TargetRevisionId = revision.Id, ExpectedActiveRevisionId = draft.Id,
            ExpectedModelConcurrencyStamp = active.ConcurrencyStamp,
            ExpectedTargetRevisionConcurrencyStamp = (await models.GetRevisionAsync(revision.Id)).ConcurrencyStamp
        });
        Assert.Equal(new[] { 100m, 200m }, Widths((await execution.ExecuteAsync(Request(1))).Results));
    }

    private async Task InitializeAsync()
    {
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await uow.CompleteAsync();
    }

    private static async Task<RuleRevisionDto> AddRuleAsync(IRuleAuthoringAppService authoring, RuleRevisionDto revision, int value)
    {
        revision = await authoring.AddRuleAsync(revision.Id, new AddRuleDto
        {
            Name = $"候选{value}", SortOrder = value, Priority = 0, IsEnabled = true,
            ConditionAstJson = JsonSerializer.Serialize(new
            {
                version = 1, root = new
                {
                    op = "gt", args = new object[]
                    {
                        new { op = "variable", stableKey = revision.Variables.Single(x => x.Code == "width").StableKey },
                        new { op = "constant", kind = RuleValueKind.Decimal, value = 0 }
                    }
                }
            }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        var rule = revision.Rules.Single(x => x.SortOrder == value);
        foreach (var (code, json, order) in new[] { ("targetWidth", value.ToString(), 1), ("tolerance", "{\"min\":-1,\"max\":2}", 2) })
        {
            revision = await authoring.AddAssignmentAsync(revision.Id, rule.Id, new AddAssignmentDto
            {
                TargetVariableStableKey = revision.Variables.Single(x => x.Code == code).StableKey,
                ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = json, SortOrder = order,
                ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
            });
        }
        return revision;
    }

    private static async Task<RuleRevisionDto> AddGatesAsync(IRuleReleaseAppService release, RuleRevisionDto revision, int first, int second)
    {
        revision = await AddPositiveGateAsync(release, revision, first, second);
        return await release.AddGateCaseAsync(revision.Id, new AddReleaseGateCaseDto
        {
            Name = "未命中", InputsJson = "{\"width\":0}", ExpectedResultsJson = "[]",
            ExpectedStatus = RuleExecutionStatus.NotMatched, ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
    }

    private static async Task<RuleRevisionDto> AddPositiveGateAsync(IRuleReleaseAppService release, RuleRevisionDto revision, int first, int second)
    {
        return await release.AddGateCaseAsync(revision.Id, new AddReleaseGateCaseDto
        {
            Name = "两行", InputsJson = "{\"width\":1}",
            ExpectedResultsJson = JsonSerializer.Serialize(new[] { first, second }.Select(x => new { targetWidth = x, tolerance = new { min = -1, max = 2 } })),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
    }

    private static async Task PublishAsync(IRuleModelAppService models, IRuleReleaseAppService release, Guid modelId, RuleRevisionDto revision)
    {
        await release.PublishAsync(revision.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(modelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
    }

    private static decimal[] Widths(IReadOnlyList<RuleResultRowDto> rows)
    {
        return rows.Select(x => x.Outputs["targetWidth"].GetDecimal()).ToArray();
    }

    private static ExecuteRuleDto Request(decimal width)
    {
        return new ExecuteRuleDto
        {
            AppCode = "TEST-ODS", ScenarioCode = "matched-results",
            Inputs = new Dictionary<string, JsonElement> { ["width"] = JsonSerializer.SerializeToElement(width) }
        };
    }
}
