using NRuleEngine.Domain.Shared.Runtime;

namespace NRuleEngine.Domain.Simulation;

/// <summary>不持久化的规则求值结果，由试算和生产运行共同使用。</summary>
/// <param name="RevisionId">本次试算使用的修订。</param>
/// <param name="BindingId">本次命中的绑定；失败早于绑定选择时为空。</param>
/// <param name="Success">是否得到全部必需出口。</param>
/// <param name="ErrorCode">失败原因的稳定编码。</param>
/// <param name="ErrorMessage">供维护者排查的说明。</param>
/// <param name="Conditions">已评估规则的命中情况。</param>
/// <param name="Results">按执行顺序排列的完整结果行；失败时为空。</param>
public sealed record RuleSimulationOutcome(Guid RevisionId, Guid? BindingId, bool Success, string? ErrorCode,
    string? ErrorMessage, IReadOnlyList<RuleConditionOutcome> Conditions,
    IReadOnlyList<RuleResultRow> Results)
{
    /// <summary>试算和门禁使用的统一状态，无命中与求值失败分开。</summary>
    public RuleExecutionStatus Status => Success ? RuleExecutionStatus.Succeeded : ErrorCode switch
    {
        "NoMatchingRule" or "NoMatchingBinding" => RuleExecutionStatus.NotMatched,
        "InvalidRevision" or "AmbiguousBinding" or "UnknownInput" or "MissingInput" or
            "InputTypeMismatch" or "InputConstraintViolated" => RuleExecutionStatus.Rejected,
        _ => RuleExecutionStatus.Failed
    };

    /// <summary>执行失败所对应的规则或赋值路径。</summary>
    public string? ErrorPath { get; init; }

    /// <summary>可选的表达式错误起始字符位置。</summary>
    public int? Start { get; init; }

    /// <summary>可选的表达式错误字符跨度。</summary>
    public int? Length { get; init; }

    /// <summary>本次试算中实际执行的赋值及其结果。</summary>
    public IReadOnlyList<RuleAssignmentOutcome> Assignments { get; init; } = [];

    /// <summary>与输入或必需出口错误相关的变量编码。</summary>
    public string? ErrorVariableCode { get; init; }
}
