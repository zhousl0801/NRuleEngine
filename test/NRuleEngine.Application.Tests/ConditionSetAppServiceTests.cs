using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts.Conditions;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class ConditionSetAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task PublishesMatchesCopiesAndRollsBackWithAuditedHistory()
    {
        // 贯穿创建、发布、判定、复制和回滚，核对活动修订与审计历史。
        using (var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            await uow.CompleteAsync();
        }
        var service = ServiceProvider.GetRequiredService<IConditionSetAppService>();
        var set = await service.CreateAsync(new CreateConditionSetDto
        {
            AppCode = "TEST-ODS",
            ScenarioCode = "route-machine-param-eligibility",
            ContractVersion = "1.0",
            Code = "RM-01",
            Name = "轧机参数资格"
        });
        var first = await service.GetRevisionAsync(Assert.Single(await service.GetRevisionsAsync(set.Id)).Id);
        var key = Assert.Single(first.Inputs).StableKey;
        first = await service.UpdateConditionAsync(first.Id, new UpdateConditionDto
        {
            ConditionAstJson = Between(key, 0m, 5m),
            Priority = 10,
            ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp
        });
        var initialSetStamp = (await service.GetAsync(set.Id)).ConcurrencyStamp;
        var failed = await Assert.ThrowsAsync<BusinessException>(() => service.PublishAsync(first.Id,
            new PublishConditionSetDto
            {
                ExpectedSetConcurrencyStamp = initialSetStamp,
                ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp,
                GateCases = [Gate(4m, false), Gate(9m, false)]
            }));
        Assert.Equal(NRuleEngineErrorCodes.ConditionGateFailed, failed.Code);
        Assert.Null((await service.GetAsync(set.Id)).ActiveRevisionId);

        await service.PublishAsync(first.Id, new PublishConditionSetDto
        {
            ExpectedSetConcurrencyStamp = (await service.GetAsync(set.Id)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = first.ConcurrencyStamp,
            GateCases = [Gate(4m, true), Gate(9m, false)]
        });
        var matching = await service.EvaluateAsync(Request(4m));
        Assert.True(matching.Succeeded, matching.ErrorMessage);
        Assert.Equal("RM-01", Assert.Single(matching.Matches).Code);
        var none = await service.EvaluateAsync(Request(9m));
        Assert.True(none.Succeeded);
        Assert.False(none.Matched);
        Assert.Empty(none.Matches);
        var bad = await service.EvaluateAsync(new EvaluateConditionSetsDto
        {
            AppCode = "TEST-ODS",
            ScenarioCode = "route-machine-param-eligibility",
            ContractVersion = "1.0",
            CandidateCodes = ["unknown"],
            Inputs = new() { ["THICKNESS"] = JsonSerializer.SerializeToElement(4m) }
        });
        Assert.Equal("InvalidCandidate", bad.ErrorCode);

        var copy = await service.CreateDraftAsync(set.Id, new CreateConditionDraftDto
        {
            ExpectedSetConcurrencyStamp = (await service.GetAsync(set.Id)).ConcurrencyStamp
        });
        Assert.Equal(key, Assert.Single(copy.Inputs).StableKey);
        copy = await service.UpdateConditionAsync(copy.Id, new UpdateConditionDto
        {
            ConditionAstJson = Between(key, 5m, 10m),
            Priority = 20,
            ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp
        });
        await service.PublishAsync(copy.Id, new PublishConditionSetDto
        {
            ExpectedSetConcurrencyStamp = (await service.GetAsync(set.Id)).ConcurrencyStamp,
            ExpectedRevisionConcurrencyStamp = copy.ConcurrencyStamp,
            GateCases = [Gate(8m, true), Gate(4m, false)]
        });
        Assert.False((await service.EvaluateAsync(Request(4m))).Matched);
        await service.RollbackAsync(set.Id, new RollbackConditionSetDto
        {
            TargetRevisionId = first.Id,
            ExpectedSetConcurrencyStamp = (await service.GetAsync(set.Id)).ConcurrencyStamp
        });
        Assert.True((await service.EvaluateAsync(Request(4m))).Matched);
        Assert.Equal([3, 2, 1], (await service.GetHistoryAsync(set.Id)).Select(x => x.ReleaseNo));
        Assert.Equal(RuleRevisionStatus.Published, (await service.GetRevisionAsync(copy.Id)).Status);
    }

    private static EvaluateConditionSetsDto Request(decimal thickness)
    {
        return new EvaluateConditionSetsDto
        {
            AppCode = "TEST-ODS",
            ScenarioCode = "route-machine-param-eligibility",
            ContractVersion = "1.0",
            CandidateCodes = ["RM-01"],
            Inputs = new() { ["THICKNESS"] = JsonSerializer.SerializeToElement(thickness) }
        };
    }

    private static ConditionGateCaseDto Gate(decimal thickness, bool expected)
    {
        return new ConditionGateCaseDto
        {
            Inputs = new() { ["THICKNESS"] = JsonSerializer.SerializeToElement(thickness) },
            ExpectedMatch = expected
        };
    }

    private static string Between(Guid key, decimal minimum, decimal maximum)
    {
        RuleExpressionNode Compare(string op, decimal value)
        {
            return new RuleExpressionNode
            {
                Op = op,
                Args =
                [
                    new RuleExpressionNode { Op = "variable", StableKey = key },
                    new RuleExpressionNode
                    {
                        Op = "constant", Kind = RuleValueKind.Decimal,
                        Value = JsonSerializer.SerializeToElement(value)
                    }
                ]
            };
        }
        return JsonSerializer.Serialize(new RuleExpression
        {
            Version = 1,
            Root = new RuleExpressionNode { Op = "and", Args = [Compare("gte", minimum), Compare("lte", maximum)] }
        });
    }
}
