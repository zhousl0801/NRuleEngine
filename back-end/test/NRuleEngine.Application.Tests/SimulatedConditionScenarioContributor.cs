using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Tests;

public sealed class SimulatedConditionScenarioContributor : IConditionScenarioContributor
{
    public void Configure(IConditionScenarioRegistry registry)
    {
        registry.Add(new ConditionScenarioDescriptor("TEST-ODS", "route-machine-param-eligibility", "1.0", [new VariableContract("THICKNESS", "Thickness", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))]));
    }
}
