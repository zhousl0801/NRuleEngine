using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Tests;

/// <summary>
/// 在领域测试中注册模拟尺寸展开场景。
/// </summary>
public sealed class SimulatedSizeExpansionContributor : IRuleScenarioContributor
{
    public void Configure(IRuleScenarioRegistry registry)
    {
        registry.Add(SimulatedSizeExpansionScenario.Contract);
    }
}
