namespace NRuleEngine.Application.Runtime;

/// <summary>生产规则执行的宿主可配置时间预算。</summary>
public class RuleExecutionOptions
{
    /// <summary>包括数据库读取和内存求值的最长执行时间，默认两秒。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
}
