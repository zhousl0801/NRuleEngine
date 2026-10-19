namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 向目录贡献场景契约。
/// </summary>
public interface IRuleScenarioContributor
{
    /// <summary>向注册器提交本模块支持的场景契约。</summary>
    void Configure(IRuleScenarioRegistry registry);
}
