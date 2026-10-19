using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Tests;

/// <summary>
/// 在应用测试中注册模拟应用。
/// </summary>
internal sealed class SimulatedRuleAppContributor : IRuleAppContributor
{
    public void Configure(IRuleAppRegistry registry)
    {
        registry.Add(new RuleAppDescriptor("TEST-ODS", "Simulated order design", "Application.Tests"));
    }
}
