using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Tests;

/// <summary>
/// 在应用测试中注册模拟尺寸展开场景。
/// </summary>
internal sealed class SimulatedSizeExpansionContributor : IRuleScenarioContributor
{
    public void Configure(IRuleScenarioRegistry registry)
    {
        registry.Add(new RuleScenarioDescriptor("TEST-ODS", "size-expansion", "1.0", [new VariableContract("inputWidth", "Input width", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))], [new VariableContract("targetWidth", "Target width", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal))], [new BindingDimensionContract("productKind", "Product kind", RuleValueKind.Enum, [RuleBindingOperator.Equals])]));
    }
}
