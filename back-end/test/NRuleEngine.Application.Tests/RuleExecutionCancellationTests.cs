using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Application.Runtime;
using NRuleEngine.TestBase;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace NRuleEngine.Application.Tests;

/// <summary>验证执行入口在异步计算期间保留调用方取消及其优先级。</summary>
public sealed class RuleExecutionCancellationTests : NRuleEngineTestBase<NRuleEngineApplicationTestModule>
{
    [Fact]
    public async Task PropagatesCallerCancellationWhileExecutionIsAwaiting()
    {
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken executionToken = default;
        var execution = CreateExecution(async token =>
        {
            executionToken = token;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new RuleExecutionResultDto();
        }, TimeSpan.FromSeconds(30));

        var running = execution.ExecuteAsync(new ExecuteRuleDto(), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        caller.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(executionToken, error.CancellationToken);
        Assert.True(executionToken.IsCancellationRequested);
    }

    [Fact]
    public async Task CallerCancellationTakesPriorityWhenInternalBudgetAlsoExpires()
    {
        using var caller = new CancellationTokenSource();
        var execution = CreateExecution(async token =>
        {
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = token.Register(() =>
            {
                // 先耗尽内部预算，再令调用方同时取消，确保异常分类时两者均已取消。
                caller.Cancel();
                cancelled.SetResult();
            });
            await cancelled.Task;
            token.ThrowIfCancellationRequested();
            return new RuleExecutionResultDto();
        }, TimeSpan.FromMilliseconds(20));

        var running = execution.ExecuteAsync(new ExecuteRuleDto(), caller.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(caller.IsCancellationRequested);
    }

    private RuleExecutionAppService CreateExecution(Func<CancellationToken, Task<RuleExecutionResultDto>> operation, TimeSpan timeout)
    {
        return new ControlledRuleExecutionAppService(operation, timeout)
        {
            LazyServiceProvider = ServiceProvider.GetRequiredService<IAbpLazyServiceProvider>()
        };
    }

    [DisableConventionalRegistration]
    private sealed class ControlledRuleExecutionAppService : RuleExecutionAppService
    {
        private readonly Func<CancellationToken, Task<RuleExecutionResultDto>> _operation;

        public ControlledRuleExecutionAppService(Func<CancellationToken, Task<RuleExecutionResultDto>> operation, TimeSpan timeout)
            : base(null!, null!, null!, Options.Create(new RuleExecutionOptions { Timeout = timeout }), NullLogger<RuleExecutionAppService>.Instance)
        {
            _operation = operation;
        }

        protected override Task<RuleExecutionResultDto> ExecuteCoreAsync(Guid executionId, ExecuteRuleDto input, CancellationToken cancellationToken)
        {
            return _operation(cancellationToken);
        }
    }
}
