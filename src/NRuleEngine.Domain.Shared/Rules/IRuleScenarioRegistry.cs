namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 接收规则场景的代码注册。
/// </summary>
public interface IRuleScenarioRegistry
{
    /// <summary>按场景编码和契约版本注册一份场景契约。</summary>
    void Add(RuleScenarioDescriptor descriptor);
}
