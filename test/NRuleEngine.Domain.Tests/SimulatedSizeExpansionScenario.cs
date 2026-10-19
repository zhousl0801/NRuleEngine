using NRuleEngine.Domain.Shared.Rules;
namespace NRuleEngine.Domain.Tests;

/// <summary>
/// 测试专用的尺寸展开场景；字段只用于模拟，不代表真实生产契约。
/// </summary>
public static class SimulatedSizeExpansionScenario
{
    public static RuleAppDescriptor App
    {
        get
        {
            return new RuleAppDescriptor("TEST-ODS", "Simulated order design", "Domain.Tests");
        }
    }

    public static RuleScenarioDescriptor Contract
    {
        get
        {
            return new RuleScenarioDescriptor("TEST-ODS", "size-expansion", "1.0",
                [
                    new VariableContract("inputWidth", "Input width", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal, Precision: 28, Scale: 10)),
                    new VariableContract("productKind", "Product kind", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Enum, EnumCode: "test-product-kind"))
                ],
                [
                    new VariableContract("targetWidth", "Target width", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Precision: 28, Scale: 10))
                ],
                [
                    new BindingDimensionContract("productKind", "Product kind", RuleValueKind.Enum, [RuleBindingOperator.Equals, RuleBindingOperator.In])
                ]);
        }
    }
}
