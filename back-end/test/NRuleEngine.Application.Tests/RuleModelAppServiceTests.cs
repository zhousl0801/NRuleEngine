using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Simulation;
using NRuleEngine.Application.Contracts.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Scenarios;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Runtime;
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
        // 经应用服务保存规则和绑定，再复制草稿并试算，验证持久化内容可复用。
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
        // 创建多模型后核对排序与场景过滤，避免列表结果依赖数据库自然顺序。
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
        // 创建后续草稿并核对稳定键，确保已有规则引用仍能定位变量。
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

    [Fact]
    public async Task CopiesModelWithDefaultsAndGateCasesAndBlocksReferencedVariableRemoval()
    {
        // 复制默认值与门禁内容后，验证引用检查会阻止删除被使用的变量。
        RuleModelDto sourceModel;
        RuleRevisionDto source;
        using (var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>()
                .GetDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
            var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
            var bindings = ServiceProvider.GetRequiredService<IRuleBindingAppService>();
            var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
            sourceModel = await models.CreateAsync(new CreateRuleModelDto(
                "TEST-ODS", "size-expansion", "1.0", "source-copy", "Source", null));
            source = await models.GetRevisionAsync(Assert.Single(await models.GetRevisionsAsync(sourceModel.Id)).Id);
            var input = source.Variables.Single(v => v.Code == "inputWidth");
            source = await models.SetInputDefaultAsync(source.Id, input.StableKey, new SetInputDefaultDto
            {
                DefaultValueJson = "120", ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
            });
            source = await models.AddIntermediateVariableAsync(source.Id,
                new AddIntermediateVariableDto("extra", "Extra", new RuleTypeDescriptor(RuleValueKind.Decimal),
                    source.ConcurrencyStamp));
            var middle = source.Variables.Single(v => v.Code == "extra");
            source = await models.UpdateIntermediatePropertiesAsync(source.Id, middle.StableKey,
                new UpdateIntermediatePropertiesDto
                {
                    DefaultValueJson = "5", Unit = "mm",
                    ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
                });
            source = await models.SetIntermediatePrecisionAsync(source.Id, middle.StableKey,
                new SetIntermediatePrecisionDto
                {
                    Precision = 28, ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
                });
            source = await models.UpdateIntermediatePropertiesAsync(source.Id, middle.StableKey,
                new UpdateIntermediatePropertiesDto
                {
                    DefaultValueJson = "5", Unit = "mm",
                    ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
                });
            Assert.Equal(28, source.Variables.Single(v => v.Code == "extra").Type.Precision);
            source = await rules.AddRuleAsync(source.Id, new AddRuleDto
            {
                Name = "from defaults", Priority = 1, SortOrder = 1,
                ConditionAstJson = JsonSerializer.Serialize(new
                {
                    version = 1, root = new { op = "constant", kind = RuleValueKind.Boolean, value = true }
                }),
                ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
            });
            source = await rules.AddAssignmentAsync(source.Id, source.Rules[0].Id, new AddAssignmentDto
            {
                TargetVariableStableKey = source.Variables.Single(v => v.Code == "targetWidth").StableKey,
                ValueSourceKind = RuleValueSourceKind.Expression,
                ExpressionAstJson = JsonSerializer.Serialize(new
                {
                    version = 1, root = new { op = "add", args = new object[]
                    {
                        new { op = "variable", stableKey = input.StableKey },
                        new { op = "variable", stableKey = middle.StableKey }
                    } }
                }),
                SortOrder = 1, ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
            });
            source = await bindings.AddBindingAsync(source.Id, new AddBindingDto
            {
                Priority = 1,
                Criteria = [new BindingCriterionDto
                {
                    DimensionCode = "productKind", Operator = RuleBindingOperator.Equals,
                    TypedValueJson = "\"A\""
                }],
                ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
            });
            source = await release.AddGateCaseAsync(source.Id, new AddReleaseGateCaseDto
            {
                Name = "defaults", ContextJson = "{\"productKind\":\"A\"}", InputsJson = "{}",
                ExpectedOutputsJson = "{\"targetWidth\":125}",
                ExpectedRevisionConcurrencyStamp = source.ConcurrencyStamp
            });
            await uow.CompleteAsync();
        }

        var service = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var copyModel = await service.CopyAsync(sourceModel.Id, new CopyRuleModelDto
        {
            SourceRevisionId = source.Id, Code = "copied-model", Name = "Copied",
            ExpectedSourceRevisionConcurrencyStamp = source.ConcurrencyStamp
        });
        var copy = await service.GetRevisionAsync(Assert.Single(await service.GetRevisionsAsync(copyModel.Id)).Id);
        Assert.Equal(source.Variables.Select(v => v.StableKey).Order(),
            copy.Variables.Select(v => v.StableKey).Order());
        Assert.Equal("120", copy.Variables.Single(v => v.Code == "inputWidth").DefaultValueJson);
        Assert.Equal("mm", copy.Variables.Single(v => v.Code == "extra").Unit);
        Assert.Equal(source.GateCases[0].InputsJson, copy.GateCases[0].InputsJson);
        Assert.NotEqual(source.GateCases[0].Id, copy.GateCases[0].Id);
        var middleKey = copy.Variables.Single(v => v.Code == "extra").StableKey;
        var impact = await service.GetVariableReferencesAsync(copy.Id, middleKey);
        Assert.True(impact.BlocksOperation);
        Assert.Equal(copy.ConcurrencyStamp, impact.ConcurrencyStamp);
        Assert.Contains(impact.References, x => x.Kind == "AssignmentExpression");
        var blocked = await Assert.ThrowsAsync<BusinessException>(() => service.RemoveIntermediateVariableAsync(
            copy.Id, middleKey,
            new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp }));
        Assert.Equal(NRuleEngineErrorCodes.VariableReferenced, blocked.Code);
        var ruleRemovalImpact = await service.GetRuleRemovalImpactAsync(copy.Id, copy.Rules[0].Id);
        Assert.False(ruleRemovalImpact.BlocksOperation);
        Assert.Contains(ruleRemovalImpact.References, x => x.Kind == "AffectedVariable");
        var assignmentRemovalImpact = await service.GetAssignmentRemovalImpactAsync(copy.Id, copy.Rules[0].Id,
            copy.Rules[0].Assignments[0].Id);
        Assert.False(assignmentRemovalImpact.BlocksOperation);
        Assert.Contains(assignmentRemovalImpact.References, x => x.Kind == "AffectedVariable");

        await ServiceProvider.GetRequiredService<IRuleReleaseAppService>().PublishAsync(copy.Id,
            new PublishRuleDto
            {
                ExpectedModelConcurrencyStamp = (await service.GetAsync(copyModel.Id)).ConcurrencyStamp,
                ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
            });
        var result = await ServiceProvider.GetRequiredService<IRuleExecutionAppService>().ExecuteAsync(new ExecuteRuleDto
        {
            AppCode = "TEST-ODS", ScenarioCode = "size-expansion",
            Context = new() { ["productKind"] = JsonSerializer.SerializeToElement("A") }
        });
        Assert.Equal(RuleExecutionStatus.Succeeded, result.Status);
        Assert.Equal(125, result.Outputs["targetWidth"].GetDecimal());
        Assert.Contains((await service.GetArchiveImpactAsync(copyModel.Id)).References,
            x => x.Kind == "ActiveRevision");
        Assert.Contains((await service.GetArchiveImpactAsync(copyModel.Id)).References,
            x => x.Kind == "RuntimeSelection");
        Assert.Null((await service.GetAsync(sourceModel.Id)).ActiveRevisionId);

        var published = await service.GetRevisionAsync(copy.Id);
        var fromPublished = await service.CopyAsync(copyModel.Id, new CopyRuleModelDto
        {
            SourceRevisionId = copy.Id, Code = "copied-published", Name = "From published",
            ExpectedSourceRevisionConcurrencyStamp = published.ConcurrencyStamp
        });
        var publishedCopy = await service.GetRevisionAsync(
            Assert.Single(await service.GetRevisionsAsync(fromPublished.Id)).Id);
        Assert.Equal(RuleRevisionStatus.Draft, publishedCopy.Status);
        Assert.Null(fromPublished.ActiveRevisionId);
        Assert.Equal(published.GateCases[0].InputsJson, publishedCopy.GateCases[0].InputsJson);
        Assert.Equal(published.Variables.Select(v => v.StableKey).Order(),
            publishedCopy.Variables.Select(v => v.StableKey).Order());
    }
}
