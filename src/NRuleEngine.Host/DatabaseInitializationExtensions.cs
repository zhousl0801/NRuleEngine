using NRuleEngine.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace NRuleEngine.Host;

public static class DatabaseInitializationExtensions
{
    /// <summary>为本地联调库创建当前 EF 模型对应的表。</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var unitOfWork = unitOfWorkManager.Begin();
        var provider = scope.ServiceProvider.GetRequiredService<IDbContextProvider<NRuleEngineDbContext>>();
        var dbContext = await provider.GetDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await unitOfWork.CompleteAsync();
    }
}
