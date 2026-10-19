namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 场景可用于绑定的维度契约。
/// </summary>
/// <param name="Code">维度的稳定业务编码。</param>
/// <param name="Name">展示给规则维护者的维度名称。</param>
/// <param name="ValueKind">绑定值需要满足的基础类型。</param>
/// <param name="AllowedOperators">该维度允许使用的比较运算符。</param>
public sealed record BindingDimensionContract(
    string Code,
    string Name,
    RuleValueKind ValueKind,
    IReadOnlyList<RuleBindingOperator> AllowedOperators);
