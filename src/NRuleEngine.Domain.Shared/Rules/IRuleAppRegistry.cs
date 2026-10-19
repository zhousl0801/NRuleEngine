namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 接收接入应用的代码注册。
/// </summary>
public interface IRuleAppRegistry
{
    /// <summary>注册一个具有唯一应用编码的接入应用。</summary>
    void Add(RuleAppDescriptor descriptor);
}
