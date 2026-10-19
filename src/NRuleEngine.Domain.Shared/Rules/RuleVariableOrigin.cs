namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 变量定义的来源。
/// </summary>
public enum RuleVariableOrigin
{
    /// <summary>由宿主代码注册的场景契约定义。</summary>
    Contract,
    /// <summary>由规则维护者在草稿中创建。</summary>
    UserDefined
}
