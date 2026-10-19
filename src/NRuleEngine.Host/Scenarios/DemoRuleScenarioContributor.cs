using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Host.Scenarios;

/// <summary>注册可用于编制与执行的赋值型示例场景。</summary>
public sealed class DemoRuleScenarioContributor : IRuleScenarioContributor
{
    public void Configure(IRuleScenarioRegistry registry)
    {
        registry.Add(new RuleScenarioDescriptor("DEMO", "size-expansion", "1.0", [new VariableContract("inputWidth", "入口宽度", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))], [new VariableContract("targetWidth", "目标宽度", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal))], [new BindingDimensionContract("productKind", "产品类型", RuleValueKind.Enum, [RuleBindingOperator.Equals])]));
        registry.Add(new RuleScenarioDescriptor("DEMO", "size-candidates", "1.0",
            [new VariableContract("inputWidth", "入口宽度", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new VariableContract("widthRange", "宽度范围", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.NumericRange)),
                new VariableContract("candidateWidths", "候选宽度", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.List)),
                new VariableContract("candidateCodes", "候选编码", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.String, Shape: RuleValueShape.List))],
            [new BindingDimensionContract("productKind", "产品类型", RuleValueKind.Enum, [RuleBindingOperator.Equals])]));
        registry.Add(new RuleScenarioDescriptor("DEMO", "matched-results", "1.0",
            [new VariableContract("width", "宽度", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new VariableContract("targetWidth", "目标宽度", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal)),
                new VariableContract("tolerance", "公差", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.NumericRange))],
            [], RuleResultCardinality.Many));
    }
}
