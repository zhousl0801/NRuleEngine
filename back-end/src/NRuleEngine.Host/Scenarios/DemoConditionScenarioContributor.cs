using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Host.Scenarios;

/// <summary>注册可用于条件集编制与判定的示例场景。</summary>
public sealed class DemoConditionScenarioContributor : IConditionScenarioContributor
{
    public void Configure(IConditionScenarioRegistry registry)
    {
        registry.Add(new ConditionScenarioDescriptor("DEMO", "thickness-eligibility", "1.0",
            [new VariableContract("thickness", "厚度", RuleVariableDirection.Input,
                new RuleTypeDescriptor(RuleValueKind.Decimal))]));
    }
}
