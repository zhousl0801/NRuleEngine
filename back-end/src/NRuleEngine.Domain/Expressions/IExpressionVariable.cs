using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Expressions;

/// <summary>表达式类型检查所需的最小变量契约。</summary>
public interface IExpressionVariable
{
    /// <summary>表达式引用的逻辑稳定键。</summary>
    Guid StableKey { get; }

    /// <summary>变量在场景中的编码。</summary>
    string Code { get; }

    /// <summary>变量的基础值类型。</summary>
    RuleValueKind ValueKind { get; }

    /// <summary>是否接受空值。</summary>
    bool IsNullable { get; }

    /// <summary>Decimal 的总位数约束。</summary>
    int? Precision { get; }

    /// <summary>Decimal 的小数位约束。</summary>
    int? Scale { get; }
}
