using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Simulation;
using NRuleEngine.Application.Contracts.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Scenarios;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class RuleModelAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task PersistsRuleBindingAndSimulationAcrossDraftCopy()
    {
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var provider = ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>();
        var db = await provider.GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var service = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var bindings = ServiceProvider.GetRequiredService<IRuleBindingAppService>();
        var validation = ServiceProvider.GetRequiredService<IRuleValidationAppService>();
        var simulation = ServiceProvider.GetRequiredService<IRuleSimulationAppService>();

        var model = await service.CreateAsync(new CreateRuleModelDto(
            "TEST-ODS", "size-expansion", "1.0", "authoring", "Authoring", null));
        var revision = await service.GetRevisionAsync(Assert.Single(await service.GetRevisionsAsync(model.Id)).Id);
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        revision = await rules.AddRuleAsync(revision.Id, new AddRuleDto
        {
            Name = "constant width", Priority = 10, SortOrder = 1,
            ConditionAstJson = JsonSerializer.Serialize(new
            {
                version = 1, root = new { op = "constant", kind = RuleValueKind.Boolean, value = true }
            }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        var rule = Assert.Single(revision.Rules);
        revision = await rules.AddAssignmentAsync(revision.Id, rule.Id, new AddAssignmentDto
        {
            TargetVariableStableKey = output.StableKey, ValueSourceKind = RuleValueSourceKind.Constant,
            ConstantValueJson = "125", SortOrder = 1,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await bindings.AddBindingAsync(revision.Id, new AddBindingDto
        {
            Priority = 1,
            Criteria = [new BindingCriterionDto
            {
                DimensionCode = "productKind", Operator = RuleBindingOperator.Equals,
                TypedValueJson = "\"A\""
            }],
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        await uow.SaveChangesAsync();
        Assert.True((await validation.ValidateAsync(revision.Id)).IsValid);
        var request = new SimulateRuleDto
        {
            RevisionId = revision.Id,
            ContextJson = new Dictionary<string, string> { ["productKind"] = "\"A\"" },
            InputsJson = new Dictionary<string, string>
            {
                ["inputWidth"] = "120"
            }
        };
        var result = await simulation.SimulateAsync(request);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("125", result.OutputsJson["targetWidth"]);

        var currentModel = await service.GetAsync(model.Id);
        var copy = await service.CreateDraftAsync(model.Id,
            new CreateDraftDto(revision.Id, currentModel.ConcurrencyStamp));
        await uow.SaveChangesAsync();
        Assert.Single(copy.Rules);
        Assert.Single(copy.Bindings);
        Assert.NotEqual(rule.Id, copy.Rules[0].Id);
        Assert.Equal(output.StableKey, copy.Variables.Single(v => v.Code == "targetWidth").StableKey);
        copy = await rules.UpdateRuleAsync(copy.Id, copy.Rules[0].Id, new AddRuleDto
        {
            Name = "updated", Priority = 10, SortOrder = 1,
            ConditionAstJson = rule.ConditionAstJson,
            ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
        });
        copy = await rules.UpdateAssignmentAsync(copy.Id, copy.Rules[0].Id,
            copy.Rules[0].Assignments[0].Id, new AddAssignmentDto
            {
                TargetVariableStableKey = output.StableKey,
                ValueSourceKind = RuleValueSourceKind.Constant,
                ConstantValueJson = "130", SortOrder = 1,
                ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
            });
        copy = await bindings.UpdateBindingAsync(copy.Id, copy.Bindings[0].Id, new AddBindingDto
        {
            Priority = 1,
            Criteria = [new BindingCriterionDto
            {
                DimensionCode = "productKind", Operator = RuleBindingOperator.Equals,
                TypedValueJson = "\"B\""
            }],
            ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
        });
        await uow.SaveChangesAsync();
        db.ChangeTracker.Clear();
        copy = await service.GetRevisionAsync(copy.Id);
        Assert.Equal("updated", copy.Rules[0].Name);
        Assert.Equal("\"B\"", copy.Bindings[0].Criteria[0].TypedValueJson);
        request.RevisionId = copy.Id;
        request.ContextJson["productKind"] = "\"B\"";
        var copiedResult = await simulation.SimulateAsync(request);
        Assert.True(copiedResult.Success, copiedResult.ErrorMessage);
        Assert.Equal("130", copiedResult.OutputsJson["targetWidth"]);
        copy = await rules.RemoveAssignmentAsync(copy.Id, copy.Rules[0].Id,
            copy.Rules[0].Assignments[0].Id, new DeleteDraftItemDto
            {
                ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
            });
        Assert.Contains((await validation.ValidateAsync(copy.Id)).Issues, i => i.Code == "NoAssignment");
        copy = await rules.RemoveRuleAsync(copy.Id, copy.Rules[0].Id, new DeleteDraftItemDto
        {
            ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
        });
        copy = await bindings.RemoveBindingAsync(copy.Id, copy.Bindings[0].Id, new DeleteDraftItemDto
        {
            ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
        });
        Assert.Empty(copy.Rules);
        Assert.Empty(copy.Bindings);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task ListsModelsInCodeOrderAndFiltersByScenario()
    {
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var provider = ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>();
        var db = await provider.GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var service = ServiceProvider.GetRequiredService<IRuleModelAppService>();

        await service.CreateAsync(new CreateRuleModelDto("TEST-ODS", "size-expansion", "1.0", "zeta", "Zeta", null));
        await service.CreateAsync(new CreateRuleModelDto("TEST-ODS", "size-expansion", "1.0", "alpha", "Alpha", null));
        await uow.SaveChangesAsync();

        var models = await service.GetListAsync("TEST-ODS");
        Assert.Equal(["alpha", "zeta"], models.Select(x => x.Code));
        Assert.Equal(2, (await service.GetListAsync("TEST-ODS", "size-expansion")).Count);
        Assert.Empty(await service.GetListAsync("TEST-ODS", "other"));
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task CreatesDraftAndPreservesVariableIdentityAcrossRevisions()
    {
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var provider = ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>();
        var db = await provider.GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var service = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var catalog = ServiceProvider.GetRequiredService<IRuleCatalogAppService>();
        Assert.Equal("TEST-ODS", Assert.Single(await catalog.GetAppsAsync()).AppCode);
        Assert.Equal("size-expansion", Assert.Single(await catalog.GetScenariosAsync("TEST-ODS")).ScenarioCode);

        var model = await service.CreateAsync(new CreateRuleModelDto(
            "TEST-ODS", "size-expansion", "1.0", "width", "Width", null));
        await uow.SaveChangesAsync();
        var initialSummary = Assert.Single(await service.GetRevisionsAsync(model.Id));
        var initial = await service.GetRevisionAsync(initialSummary.Id);
        Assert.Equal(2, initial.Variables.Count);
        var initialStamp = initial.ConcurrencyStamp;

        var changed = await service.AddIntermediateVariableAsync(initial.Id,
            new AddIntermediateVariableDto("delta", "Adjustment", new RuleTypeDescriptor(RuleValueKind.Decimal),
                initialStamp));
        await uow.SaveChangesAsync();
        Assert.Equal(3, changed.Variables.Count);
        Assert.NotEqual(initialStamp, changed.ConcurrencyStamp);
        await Assert.ThrowsAsync<BusinessException>(() => service.AddIntermediateVariableAsync(initial.Id,
            new AddIntermediateVariableDto("stale", "Stale", new RuleTypeDescriptor(RuleValueKind.Decimal),
                initialStamp)));

        var freshModel = await service.GetAsync(model.Id);
        var second = await service.CreateDraftAsync(model.Id,
            new CreateDraftDto(initial.Id, freshModel.ConcurrencyStamp));
        await uow.SaveChangesAsync();
        Assert.Equal(initial.Id, second.BasedOnRevisionId);
        Assert.Equal(changed.Variables.Select(x => x.StableKey), second.Variables.Select(x => x.StableKey));
        Assert.Equal(2, second.RevisionNo);
        await uow.CompleteAsync();
    }
}
