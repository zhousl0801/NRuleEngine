namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 变量在规则计算中的方向。
/// </summary>
public enum RuleVariableDirection
{
    /// <summary>由调用方输入，不允许规则赋值。</summary>
    Input,
    /// <summary>供规则内部计算使用。</summary>
    Intermediate,
    /// <summary>场景契约要求规则最终生成的输出。</summary>
    Output
}
