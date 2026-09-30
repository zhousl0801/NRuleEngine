namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 同一输出被多次写入时的处理方式。
/// </summary>
public enum MultipleWritePolicy
{
    /// <summary>同一变量第二次写入时报告冲突。</summary>
    ErrorOnMultipleWrite,
    /// <summary>保留第一次写入结果，忽略后续写入。</summary>
    FirstWriteWins,
    /// <summary>以后一次写入覆盖此前结果。</summary>
    LastWriteWins
}
