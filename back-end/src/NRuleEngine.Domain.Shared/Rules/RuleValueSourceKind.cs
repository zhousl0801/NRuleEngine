namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>赋值来源。</summary>
public enum RuleValueSourceKind
{
    /// <summary>直接使用已校验的类型化 JSON 常量。</summary>
    Constant,
    /// <summary>使用版本化 AST 计算赋值。</summary>
    Expression
}
