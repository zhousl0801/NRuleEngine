using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Application.Runtime;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class RuleExecutionAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task RunsOnlyPublishedRevisionAndClassifiesExpectedOutcomes()
    {
        // 按未发布、无匹配、缺输入和成功路径验证运行状态及稳定错误码。
        var (modelId, revision) = await PrepareDraftAsync("runtime-basic", "A", 1, 125);
        var execution = ServiceProvider.GetRequiredService<IRuleExecutionAppService>();
        var noPublished = await execution.ExecuteAsync(Request("A"));
        Assert.Equal(RuleExecutionStatus.Rejected, noPublished.Status);
        Assert.Equal(NRuleEngineErrorCodes.NoPublishedModel, noPublished.Error!.Code);

        await PublishAsync(modelId, revision);
        var success = await execution.ExecuteAsync(Request("A"));
        Assert.Equal(RuleExecutionStatus.Succeeded, success.Status);
        Assert.NotEqual(Guid.Empty, success.ExecutionId);
        Assert.Equal(modelId, success.RuleModelId);
        Assert.Equal(revision.Id, success.RevisionId);
        Assert.Equal(125, success.Outputs["targetWidth"].GetDecimal());

        var noMatch = await execution.ExecuteAsync(Request("B"));
        Assert.Equal(RuleExecutionStatus.NotMatched, noMatch.Status);
        Assert.Equal(NRuleEngineErrorCodes.NoMatchingBinding, noMatch.Error!.Code);
        var missingContext = await execution.ExecuteAsync(new ExecuteRuleDto
        {
            AppCode = "TEST-ODS", ScenarioCode = "size-expansion",
            Inputs = new() { ["inputWidth"] = JsonSerializer.SerializeToElement(120) }
        });
        Assert.Equal(NRuleEngineErrorCodes.RequiredContextMissing, missingContext.Error!.Code);

        var unknown = Request("A");
        unknown.Inputs["extra"] = JsonSerializer.SerializeToElement(1);
        Assert.Equal(NRuleEngineErrorCodes.UnknownInput,
            (await execution.ExecuteAsync(unknown)).Error!.Code);
        var missingInput = Request("A");
        missingInput.Inputs.Clear();
        Assert.Equal(NRuleEngineErrorCodes.RequiredInputMissing,
            (await execution.ExecuteAsync(missingInput)).Error!.Code);
        var badType = Request("A");
        badType.Inputs["inputWidth"] = JsonSerializer.SerializeToElement("bad");
        Assert.Equal(NRuleEngineErrorCodes.InputTypeMismatch,
            (await execution.ExecuteAsync(badType)).Error!.Code);
        var constraint = Request("A");
        constraint.Inputs["inputWidth"] = JsonSerializer.SerializeToElement(0.123456789012m);
        Assert.Equal(NRuleEngineErrorCodes.InputConstraintViolated,
            (await execution.ExecuteAsync(constraint)).Error!.Code);
    }

    [Fact]
    public async Task UsesGlobalBindingPriorityAndRollbackChangesExecutionResult()
    {
        // 发布多个模型后比较全场景优先级，再回滚验证活动指针影响结果。
        var (firstModelId, first) = await PrepareDraftAsync("runtime-low", "A", 1, 125);
        var (secondModelId, second) = await PrepareDraftAsync("runtime-high", "A", 2, 130);
        var execution = ServiceProvider.GetRequiredService<IRuleExecutionAppService>();
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        await PublishAsync(firstModelId, first);
        await PublishAsync(secondModelId, second);
        var winner = await execution.ExecuteAsync(Request("A"));
        Assert.Equal(RuleExecutionStatus.Succeeded, winner.Status);
        Assert.Equal(secondModelId, winner.RuleModelId);
        Assert.Equal(130, winner.Outputs["targetWidth"].GetDecimal());

        var updated = await models.CreateDraftAsync(secondModelId, new CreateDraftDto(second.Id,
            (await models.GetAsync(secondModelId)).ConcurrencyStamp));
        updated = await rules.UpdateAssignmentAsync(updated.Id, updated.Rules[0].Id,
            updated.Rules[0].Assignments[0].Id, new AddAssignmentDto
            {
                TargetVariableStableKey = updated.Variables.Single(x => x.Code == "targetWidth").StableKey,
                ValueSourceKind = RuleValueSourceKind.Constant, ConstantValueJson = "140", SortOrder = 1,
                ExpectedRevisionConcurrencyStamp = updated.ConcurrencyStamp
            });
        updated = await release.RemoveGateCaseAsync(updated.Id, updated.GateCases[0].Id,
            new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = updated.ConcurrencyStamp });
        updated = await release.AddGateCaseAsync(updated.Id, new AddReleaseGateCaseDto
        {
            Name = "runtime gate", ContextJson = "{\"productKind\":\"A\"}",
            InputsJson = "{\"inputWidth\":120}", ExpectedOutputsJson = "{\"targetWidth\":140}",
            ExpectedRevisionConcurrencyStamp = updated.ConcurrencyStamp
        });
        await PublishAsync(secondModelId, updated);
        Assert.Equal(140, (await execution.ExecuteAsync(Request("A"))).Outputs["targetWidth"].GetDecimal());
        await release.RollbackAsync(secondModelId, new RollbackRuleDto
        {
            TargetRevisionId = second.Id, ExpectedActiveRevisionId = updated.Id,
            ExpectedModelConcurrencyStamp = (await models.GetAsync(secondModelId)).ConcurrencyStamp,
            ExpectedTargetRevisionConcurrencyStamp = (await models.GetRevisionAsync(second.Id)).ConcurrencyStamp
        });
        Assert.Equal(130, (await execution.ExecuteAsync(Request("A"))).Outputs["targetWidth"].GetDecimal());
    }

    [Fact]
    public async Task RejectsAmbiguousHighestBindingEvenWhenStoredPublishedDataWasAltered()
    {
        // 直接改变持久化发布数据，确认运行入口仍拒绝最高优先级歧义。
        var (firstModelId, first) = await PrepareDraftAsync("runtime-a", "A", 1, 125);
        var (secondModelId, second) = await PrepareDraftAsync("runtime-b", "B", 1, 130);
        await PublishAsync(firstModelId, first);
        await PublishAsync(secondModelId, second);

        // 模拟数据库中出现绕过发布门禁的并列绑定，并同步哈希，使测试直达运行时保护。
        using (var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>()
                .GetDbContextAsync();
            await db.RuleBindingCriteria.Where(x => x.BindingId == second.Bindings[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.TypedValueJson, "\"A\""));
            db.ChangeTracker.Clear();
            var changed = await db.RuleModelRevisions.Where(x => x.Id == second.Id)
                .Include(x => x.Variables).Include(x => x.Rules).ThenInclude(x => x.Assignments)
                .Include(x => x.Bindings).ThenInclude(x => x.Criteria)
                .Include(x => x.GateCases).AsSplitQuery().SingleAsync();
            db.Entry(changed).Property(x => x.ContentHash).CurrentValue =
                RuleRevisionContentHasher.Compute("TEST-ODS", changed);
            await db.SaveChangesAsync();
            await uow.CompleteAsync();
        }
        var result = await ServiceProvider.GetRequiredService<IRuleExecutionAppService>()
            .ExecuteAsync(Request("A"));
        Assert.Equal(RuleExecutionStatus.Rejected, result.Status);
        Assert.Equal(NRuleEngineErrorCodes.AmbiguousBinding, result.Error!.Code);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public async Task PropagatesCallerCancellationAndReportsInternalTimeout()
    {
        // 分别触发调用方取消与内部时间预算耗尽，检查两类信号不会混淆。
        var (modelId, revision) = await PrepareDraftAsync("runtime-cancel", "A", 1, 125);
        await PublishAsync(modelId, revision);
        var execution = ServiceProvider.GetRequiredService<IRuleExecutionAppService>();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            execution.ExecuteAsync(Request("A"), cancelled.Token));

        ServiceProvider.GetRequiredService<IOptions<RuleExecutionOptions>>().Value.Timeout =
            TimeSpan.FromTicks(1);
        var error = await Assert.ThrowsAsync<BusinessException>(() => execution.ExecuteAsync(Request("A")));
        Assert.Equal(NRuleEngineErrorCodes.ExecutionTimeout, error.Code);
        Assert.Contains("规则执行超过允许时间", error.Message);
    }

    [Fact]
    public async Task ReportsEvaluationFailureAfterAValidReleaseGate()
    {
        // 让发布门禁通过而运行输入触发求值错误，验证运行失败分类。
        var (modelId, revision) = await PrepareDraftAsync("runtime-failure", "A", 1, 125);
        var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var inputKey = revision.Variables.Single(x => x.Code == "inputWidth").StableKey;
        revision = await rules.UpdateAssignmentAsync(revision.Id, revision.Rules[0].Id,
            revision.Rules[0].Assignments[0].Id, new AddAssignmentDto
            {
                TargetVariableStableKey = revision.Variables.Single(x => x.Code == "targetWidth").StableKey,
                ValueSourceKind = RuleValueSourceKind.Expression,
                ExpressionAstJson = JsonSerializer.Serialize(new
                {
                    version = 1, root = new
                    {
                        op = "div", args = new object[]
                        {
                            new { op = "variable", stableKey = inputKey },
                            new { op = "variable", stableKey = inputKey }
                        }
                    }
                }),
                SortOrder = 1, ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
            });
        revision = await release.RemoveGateCaseAsync(revision.Id, revision.GateCases[0].Id,
            new DeleteDraftItemDto { ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp });
        revision = await release.AddGateCaseAsync(revision.Id, new AddReleaseGateCaseDto
        {
            Name = "nonzero width", ContextJson = "{\"productKind\":\"A\"}",
            InputsJson = "{\"inputWidth\":120}", ExpectedOutputsJson = "{\"targetWidth\":1}",
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        await PublishAsync(modelId, revision);
        var request = Request("A");
        request.Inputs["inputWidth"] = JsonSerializer.SerializeToElement(0);
        var result = await ServiceProvider.GetRequiredService<IRuleExecutionAppService>()
            .ExecuteAsync(request);
        Assert.Equal(RuleExecutionStatus.Failed, result.Status);
        Assert.Equal(NRuleEngineErrorCodes.ExpressionEvaluationFailed, result.Error!.Code);
        Assert.Equal(modelId, result.RuleModelId);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public async Task DetectsPublishedContentChangedOutsideTheReleaseProtocol()
    {
        // 绕过发布协议更改内容，确认执行前的哈希校验能发现损坏。
        var (modelId, revision) = await PrepareDraftAsync("runtime-integrity", "A", 1, 125);
        await PublishAsync(modelId, revision);
        using (var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>()
                .GetDbContextAsync();
            await db.RuleAssignments.Where(x => x.RuleId == revision.Rules[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.ConstantValueJson, "999"));
            await uow.CompleteAsync();
        }
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            ServiceProvider.GetRequiredService<IRuleExecutionAppService>().ExecuteAsync(Request("A")));
        Assert.Equal(NRuleEngineErrorCodes.PublishedContentCorrupted, error.Code);
    }

    private async Task<(Guid ModelId, RuleRevisionDto Revision)> PrepareDraftAsync(string code,
        string productKind, int priority, int outputWidth)
    {
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>()
            .GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var rules = ServiceProvider.GetRequiredService<IRuleAuthoringAppService>();
        var bindings = ServiceProvider.GetRequiredService<IRuleBindingAppService>();
        var release = ServiceProvider.GetRequiredService<IRuleReleaseAppService>();
        var model = await models.CreateAsync(new CreateRuleModelDto(
            "TEST-ODS", "size-expansion", "1.0", code, code, null));
        var revision = await models.GetRevisionAsync(Assert.Single(await models.GetRevisionsAsync(model.Id)).Id);
        revision = await rules.AddRuleAsync(revision.Id, new AddRuleDto
        {
            Name = "width", Priority = 10, SortOrder = 1,
            ConditionAstJson = JsonSerializer.Serialize(new
            {
                version = 1, root = new { op = "constant", kind = RuleValueKind.Boolean, value = true }
            }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await rules.AddAssignmentAsync(revision.Id, revision.Rules[0].Id, new AddAssignmentDto
        {
            TargetVariableStableKey = revision.Variables.Single(x => x.Code == "targetWidth").StableKey,
            ValueSourceKind = RuleValueSourceKind.Constant,
            ConstantValueJson = outputWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SortOrder = 1, ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await bindings.AddBindingAsync(revision.Id, new AddBindingDto
        {
            Priority = priority,
            Criteria = [new BindingCriterionDto
            {
                DimensionCode = "productKind", Operator = RuleBindingOperator.Equals,
                TypedValueJson = JsonSerializer.Serialize(productKind)
            }],
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        revision = await release.AddGateCaseAsync(revision.Id, new AddReleaseGateCaseDto
        {
            Name = "runtime gate", ContextJson = JsonSerializer.Serialize(new { productKind }),
            InputsJson = "{\"inputWidth\":120}",
            ExpectedOutputsJson = JsonSerializer.Serialize(new { targetWidth = outputWidth }),
            ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
        });
        await uow.CompleteAsync();
        return (model.Id, revision);
    }

    private async Task PublishAsync(Guid modelId, RuleRevisionDto revision)
    {
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        await ServiceProvider.GetRequiredService<IRuleReleaseAppService>().PublishAsync(revision.Id,
            new PublishRuleDto
            {
                ExpectedModelConcurrencyStamp = (await models.GetAsync(modelId)).ConcurrencyStamp,
                ExpectedRevisionConcurrencyStamp = revision.ConcurrencyStamp
            });
    }

    private static ExecuteRuleDto Request(string productKind)
    {
        return new ExecuteRuleDto
        {
            AppCode = "TEST-ODS", ScenarioCode = "size-expansion",
            Context = new() { ["productKind"] = JsonSerializer.SerializeToElement(productKind) },
            Inputs = new() { ["inputWidth"] = JsonSerializer.SerializeToElement(120) }
        };
    }
}
