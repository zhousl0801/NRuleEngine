namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>接收条件判定场景的版本化契约。</summary>
public interface IConditionScenarioRegistry
{
    /// <summary>注册固定输入契约的一个场景版本。</summary>
    void Add(ConditionScenarioDescriptor descriptor);
}
