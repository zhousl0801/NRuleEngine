using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Host.Scenarios;

/// <summary>注册可用于编制与执行的赋值型示例场景。</summary>
public sealed class DemoRuleScenarioContributor : IRuleScenarioContributor
{
    public void Configure(IRuleScenarioRegistry registry)
    {
        registry.Add(new RuleScenarioDescriptor("DEMO", "size-expansion", "1.0",
            [new VariableContract("inputWidth", "入口宽度", RuleVariableDirection.Input,
                new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new VariableContract("targetWidth", "目标宽度", RuleVariableDirection.Output,
                new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new BindingDimensionContract("productKind", "产品类型", RuleValueKind.Enum,
                [RuleBindingOperator.Equals])]));
    }
}
