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
        registry.Add(new RuleScenarioDescriptor("TEST-ODS", "composite-output", "1.0",
            [new VariableContract("width", "宽度", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new VariableContract("range", "区间", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.NumericRange)),
                new VariableContract("numbers", "候选数值", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.List)),
                new VariableContract("codes", "候选编码", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Enum, EnumCode: "codes", Shape: RuleValueShape.List))], []));
    }
}
