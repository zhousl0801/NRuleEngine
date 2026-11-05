using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts.Conditions;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.Shared.Release;
using NRuleEngine.EntityFrameworkCore;
using NRuleEngine.TestBase;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class ReleaseHistoryAppServiceTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task MergesModelAndConditionSetReleases()
    {
        // 发布记录页需要跨对象合并，并直接给出对象名称与修订号。
        using var uow = ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin();
        var db = await ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>().GetDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var models = ServiceProvider.GetRequiredService<IRuleModelAppService>();
        var sets = ServiceProvider.GetRequiredService<IConditionSetAppService>();

        var model = await models.CreateAsync(new CreateRuleModelDto("TEST-ODS", "size-expansion", "1.0", "release-model", "Release model", null));
        var set = await sets.CreateAsync(new CreateConditionSetDto
        {
            AppCode = "TEST-ODS",
            ScenarioCode = "route-machine-param-eligibility",
            ContractVersion = "1.0",
            Code = "RELEASE-SET",
            Name = "Release set"
        });
        await uow.SaveChangesAsync();

        await ServiceProvider.GetRequiredService<IReleaseRecordRepository>().InsertAsync(new ReleaseRecord(
            Guid.NewGuid(), model.Id, model.DraftRevisionId!.Value, null, 1, ReleaseKind.Publish,
            "hash-model", "ok", "model note"));
        await ServiceProvider.GetRequiredService<IRepository<ConditionSetReleaseRecord, Guid>>().InsertAsync(
            new ConditionSetReleaseRecord(Guid.NewGuid(), set.Id, set.DraftRevisionId!.Value, null, 1,
                ReleaseKind.Publish, "hash-set", "set note"));
        await uow.SaveChangesAsync();

        var service = ServiceProvider.GetRequiredService<IReleaseHistoryAppService>();
        var history = await service.GetListAsync("TEST-ODS");

        Assert.Equal(2, history.Count);
        Assert.Contains(history, x => x.TargetType == ReleaseTargetType.RuleModel &&
            x.TargetName == "Release model" && x.RevisionNo == 1 && x.ChangeSummary == "model note");
        Assert.Contains(history, x => x.TargetType == ReleaseTargetType.ConditionSet &&
            x.TargetName == "Release set" && x.RevisionNo == 1 && x.ChangeSummary == "set note");
        Assert.Single(await service.GetListAsync("TEST-ODS", null, ReleaseTargetType.ConditionSet));
        await uow.CompleteAsync();
    }
}
