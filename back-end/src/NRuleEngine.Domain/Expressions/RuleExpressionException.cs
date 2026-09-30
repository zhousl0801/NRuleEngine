namespace NRuleEngine.Domain.Expressions;

/// <summary>表达式节点校验或计算错误及其可选源码位置。</summary>
public sealed class RuleExpressionException : ArgumentException
{
    /// <summary>发生错误的节点在源文本中的起始位置。</summary>
    public int? Start { get; }
    /// <summary>发生错误的节点在源文本中的字符跨度。</summary>
    public int? Length { get; }

    /// <summary>保留原始异常及节点位置，供校验或试算结果定位。</summary>
    public RuleExpressionException(string message, int? start, int? length, Exception innerException)
        : base(message, innerException)
    {
        Start = start;
        Length = length;
    }
}
