namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>由接入应用在代码中注册条件判定场景。</summary>
public interface IConditionScenarioContributor
{
    /// <summary>在目录初始化阶段注册宿主提供的条件场景。</summary>
    void Configure(IConditionScenarioRegistry registry);
}
