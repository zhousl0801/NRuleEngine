using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Tests;

/// <summary>
/// 在领域测试中注册模拟应用。
/// </summary>
public sealed class SimulatedRuleAppContributor : IRuleAppContributor
{
    public void Configure(IRuleAppRegistry registry)
    {
        registry.Add(SimulatedSizeExpansionScenario.App);
    }
}
