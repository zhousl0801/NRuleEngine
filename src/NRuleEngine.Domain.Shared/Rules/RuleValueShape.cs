namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>变量值的结构；复合结构仅供场景出口使用。</summary>
public enum RuleValueShape
{
    /// <summary>一个基础类型值。</summary>
    Scalar,
    /// <summary>包含最小值与最大值的有限闭区间，仅支持 Decimal。</summary>
    NumericRange,
    /// <summary>按维护顺序返回的同类型候选值列表。</summary>
    List
}
