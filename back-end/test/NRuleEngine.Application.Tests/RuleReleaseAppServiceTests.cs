using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Release;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class RuleReleaseAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task PublishesAndRollsBackWithoutChangingHistoricalRevision()
    {
        // 发布后回滚到历史修订，比较活动指针与历史修订内容。
        var (modelId, first) = await CreatePreparedDraftAsync("release-history", "A", 125);
        var service = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var authoring = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var firstRelease = await release.PublishAsync(first.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await service.GetAsync(modelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp,
            ChangeSummary = "Initial width rule"
        });
        Assert.Equal(ReleaseKind.Publish, firstRelease.Kind);
        Assert.StartsWith("sha256:v1:", firstRelease.ContentHash);
        Assert.Equal("Initial width rule", firstRelease.ChangeSummary);
        Assert.Equal(first.Id, (await service.GetAsync(modelId)).ActiveRevisionId);
        Assert.Equal(RuleRevisionStatus.Published, (await service.GetRevisionAsync(first.Id)).Status);

        var publishedStamp = (await service.GetRevisionAsync(first.Id)).ConcurrencyStamp;
        await Assert.ThrowsAsync<InvalidOperationException>(() => authoring.AddAssignmentAsync(first.Id,
            first.Rules[0].Id, new AddAssignmentDto
            {
                TargetVariableStableKey = first.Variables.Single(x => x.Code == "targetWidth").StableKey,
                ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = "999", SortOrder = 2,
                ExpectedRevisionConcurrencyStamp = publishedStamp
            }));

        var second = await service.CreateDraftAsync(modelId, new CreateDraftDto(first.Id, (await service.GetAsync(modelId)).ConcurrencyStamp));
        Assert.Single(second.GateCases);
        second = await authoring.UpdateAssignmentAsync(second.Id, second.Rules[0].Id,
            second.Rules[0].Assignments[0].Id, new AddAssignmentDto
            {
                TargetVariableStableKey = second.Variables.Single(x => x.Code == "targetWidth").StableKey,
                ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = "130", SortOrder = 1,
                ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp
            });
        second = await release.RemoveGateCaseAsync(second.Id, second.GateCases[0].Id, new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp });
        second = await release.AddGateCaseAsync(second.Id, GateCase("A", 130, second.ConcurrencyStamp));
        var secondRelease = await release.PublishAsync(second.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await service.GetAsync(modelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp
        });
        Assert.Equal(first.Id, secondRelease.PreviousRevisionId);
        Assert.NotEqual(firstRelease.ContentHash, secondRelease.ContentHash);

        var historical = await service.GetRevisionAsync(first.Id);
        var rollback = await release.RollbackAsync(modelId, new RollbackRuleDto
        {
            TargetRevisionId = first.Id, ExpectedActiveRevisionId = second.Id,
            ExpectedModelConcurrencyStamp = (await service.GetAsync(modelId)).ConcurrencyStamp,
            ExpectedTargetRevisionConcurrencyStamp = historical.ConcurrencyStamp
        });
        Assert.Equal(ReleaseKind.Rollback, rollback.Kind);
        Assert.Equal(firstRelease.ContentHash, rollback.ContentHash);
        Assert.Equal("Initial width rule", rollback.ChangeSummary);
        Assert.Equal(first.Id, (await service.GetAsync(modelId)).ActiveRevisionId);
        Assert.Equal([3, 2, 1], (await release.GetHistoryAsync(modelId)).Select(x => x.ReleaseNo));
        Assert.Equal(RuleRevisionStatus.Published, (await service.GetRevisionAsync(second.Id)).Status);
    }

    [Fact]
    public async Task FailedGateDoesNotActivateOrWriteReleaseRecord()
    {
        // 让门禁失败，确认事务没有切换活动修订或写入发布记录。
        var (modelId, revision) = await CreatePreparedDraftAsync("gate-failure", "A", 999);
        var service = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var modelStamp = (await service.GetAsync(modelId)).ConcurrencyStamp;
        var error = await Assert.ThrowsAsync<BusinessException>(() => release.PublishAsync(revision.Id,
            new PublishRuleDto
            {
                ExpectedModelConcurrencyStamp = modelStamp,
                ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
            }));
        Assert.Equal(NRuleEngineErrorCodes.ReleaseGateFailed, error.Code);
        Assert.Null((await service.GetAsync(modelId)).ActiveRevisionId);
        Assert.Equal(RuleRevisionStatus.Draft, (await service.GetRevisionAsync(revision.Id)).Status);
        Assert.Empty(await release.GetHistoryAsync(modelId));
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
        Assert.Empty(await db.ReleaseScopes.ToListAsync());
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task InvalidRevisionCannotPassPublicationValidation()
    {
        // 用不完整草稿发布，确认完整校验先于活动指针切换。
        var (modelId, revision) = await CreatePreparedDraftAsync("invalid-release", "A", 125);
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        revision = await rules.RemoveRuleAsync(revision.Id, revision.Rules[0].Id, new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp });
        var modelStamp = (await models.GetAsync(modelId)).ConcurrencyStamp;
        var error = await Assert.ThrowsAsync<BusinessException>(() => release.PublishAsync(revision.Id,
            new PublishRuleDto
            {
                ExpectedModelConcurrencyStamp = modelStamp,
                ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
            }));
        Assert.Equal(NRuleEngineErrorCodes.PublishValidationFailed, error.Code);
        Assert.Null((await models.GetAsync(modelId)).ActiveRevisionId);
        Assert.Empty(await release.GetHistoryAsync(modelId));
    }

    [Fact]
    public async Task RejectsCrossModelAmbiguityAndStaleModelStamp()
    {
        // 分别构造过期并发戳和跨模型绑定重叠，验证发布保护。
        var (firstModelId, first) = await CreatePreparedDraftAsync("first-model", "A", 125);
        var (secondModelId, second) = await CreatePreparedDraftAsync("second-model", "A", 125);
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var firstStamp = (await models.GetAsync(firstModelId)).ConcurrencyStamp;
        await release.PublishAsync(first.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = firstStamp,
            ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp
        });
        var staleError = await Assert.ThrowsAsync<BusinessException>(() => release.PublishAsync(first.Id,
            new PublishRuleDto
            {
                ExpectedModelConcurrencyStamp = firstStamp,
                ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp
            }));
        Assert.Equal(NRuleEngineErrorCodes.ConcurrencyConflict, staleError.Code);

        var secondStamp = (await models.GetAsync(secondModelId)).ConcurrencyStamp;
        var conflict = await Assert.ThrowsAsync<BusinessException>(() => release.PublishAsync(second.Id,
            new PublishRuleDto
            {
                ExpectedModelConcurrencyStamp = secondStamp,
                ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp
            }));
        Assert.Equal(NRuleEngineErrorCodes.AmbiguousActiveBinding, conflict.Code);
        Assert.Null((await models.GetAsync(secondModelId)).ActiveRevisionId);
        Assert.Empty(await release.GetHistoryAsync(secondModelId));
    }

    [Fact]
    public async Task PersistsGateCasesAndReleaseRecordsInOneDatabase()
    {
        // 写入门禁用例并发布后重读数据库，确认审计与用例一并持久化。
        var (modelId, revision) = await CreatePreparedDraftAsync("persist-release", "A", 125);
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var published = await release.PublishAsync(revision.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(modelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
        Assert.Equal(1, await db.ReleaseGateCases.CountAsync());
        Assert.Equal(published.Id, (await db.ReleaseRecords.SingleAsync()).Id);
        Assert.Equal(1, await db.ReleaseScopes.CountAsync());
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task AllowsDisjointBindingsAtTheSamePriorityAcrossModels()
    {
        // 构造互斥绑定，证明跨模型同优先级本身不会导致发布失败。
        var (firstModelId, first) = await CreatePreparedDraftAsync("disjoint-a", "A", 125);
        var (secondModelId, second) = await CreatePreparedDraftAsync("disjoint-b", "B", 125);
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        await release.PublishAsync(first.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(firstModelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp
        });
        await release.PublishAsync(second.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(secondModelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp
        });
        Assert.Equal(second.Id, (await models.GetAsync(secondModelId)).ActiveRevisionId);
    }

    [Fact]
    public async Task RollbackRejectsBindingThatNowConflictsWithAnotherModel()
    {
        // 先发布其他模型再回滚，确认历史绑定仍需重新检查当前冲突。
        var (firstModelId, first) = await CreatePreparedDraftAsync("rollback-conflict", "A", 125);
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var bindings = ServiceProvider.GetRequiredService<IRuleBindingAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        await release.PublishAsync(first.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(firstModelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp
        });
        var second = await models.CreateDraftAsync(firstModelId, new CreateDraftDto(first.Id, (await models.GetAsync(firstModelId)).ConcurrencyStamp));
        second = await bindings.UpdateBindingAsync(second.Id, second.Bindings[0].Id, new AddBindingDto
        {
            Priority = 1,
            Criteria = [new BindingCriterionDto
            {
                DimensionCode = "productKind", Operator = RuleBindingOperator.Equals,
                TypedValueJson = "\"B\""
            }],
            ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp
        });
        second = await release.RemoveGateCaseAsync(second.Id, second.GateCases[0].Id, new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp });
        second = await release.AddGateCaseAsync(second.Id, GateCase("B", 125, second.ConcurrencyStamp));
        await release.PublishAsync(second.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(firstModelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = second.ConcurrencyStamp
        });

        var (otherModelId, other) = await CreatePreparedDraftAsync("occupies-a", "A", 125);
        await release.PublishAsync(other.Id, new PublishRuleDto
        {
            ExpectedModelConcurrencyStamp = (await models.GetAsync(otherModelId)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = other.ConcurrencyStamp
        });
        var modelStamp = (await models.GetAsync(firstModelId)).ConcurrencyStamp;
        var targetStamp = (await models.GetRevisionAsync(first.Id)).ConcurrencyStamp;
        var error = await Assert.ThrowsAsync<BusinessException>(() => release.RollbackAsync(firstModelId,
            new RollbackRuleDto
            {
                TargetRevisionId = first.Id, ExpectedActiveRevisionId = second.Id,
                ExpectedModelConcurrencyStamp = modelStamp,
                ExpectedTargetRevisionConcurrencyStamp = targetStamp
            }));
        Assert.Equal(NRuleEngineErrorCodes.AmbiguousActiveBinding, error.Code);
        Assert.Equal(second.Id, (await models.GetAsync(firstModelId)).ActiveRevisionId);
        Assert.Equal(2, (await release.GetHistoryAsync(firstModelId)).Count);
    }

    private async Task<(Guid ModelId, RuleRevisionDto Revision)> CreatePreparedDraftAsync(string code, string productKind, int expectedWidth)
    {
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var bindings = ServiceProvider.GetRequiredService<IRuleBindingAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var model = await models.CreateAsync(new CreateRuleModelDto("TEST-ODS", "size-expansion", "1.0", code, code, null));
        var revision = await models.GetRevisionAsync(Assert.Single(await models.GetRevisionsAsync(model.Id)).Id);
        revision = await rules.AddRuleAsync(revision.Id, new AddRuleDto
        {
            Name = "Set width", Priority = 10, SortOrder = 1,
            ConditionAstJson = JsonSerializer.Serialize(new
            {
                version = 1, root = new { op = "constant", kind = RuleValueKind.Boolean, value = true }
            }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await rules.AddAssignmentAsync(revision.Id, revision.Rules[0].Id, new AddAssignmentDto
        {
            TargetVariableStableKey = revision.Variables.Single(x => x.Code == "targetWidth").StableKey,
            ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = "125", SortOrder = 1,
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await bindings.AddBindingAsync(revision.Id, new AddBindingDto
        {
            Priority = 1,
            Criteria = [new BindingCriterionDto
            {
                DimensionCode = "productKind", Operator = RuleBindingOperator.Equals,
                TypedValueJson = JsonSerializer.Serialize(productKind)
            }],
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await release.AddGateCaseAsync(revision.Id, GateCase(productKind, expectedWidth, revision.ConcurrencyStamp));
        await uow.CompleteAsync();
        return (model.Id, revision);
    }

    private static AddReleaseGateCaseDto GateCase(string productKind, int expectedWidth, string stamp)
    {
        return new AddReleaseGateCaseDto
        {
            Name = "default case",
            ContextJson = JsonSerializer.Serialize(new { productKind }),
            InputsJson = "{\"inputWidth\":120}",
            ExpectedOutputsJson = JsonSerializer.Serialize(new { targetWidth = expectedWidth }),
            ExpectedRevisionConcurrencyStamp = stamp
        };
    }
}
