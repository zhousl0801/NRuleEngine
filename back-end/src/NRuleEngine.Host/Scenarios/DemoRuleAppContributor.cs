using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Host.Scenarios;

/// <summary>注册本地联调应用。</summary>
public sealed class DemoRuleAppContributor : IRuleAppContributor
{
    public void Configure(IRuleAppRegistry registry)
    {
        registry.Add(new RuleAppDescriptor("DEMO", "联调示例", "NRuleEngine.Host"));
    }
}
