using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Domain.Shared.Runtime;

namespace NRuleEngine.Application.Contracts.Simulation;

/// <summary>不落库的草稿试算结果。</summary>
[Serializable]
public class SimulationResultDto
{
    /// <summary>本次试算使用的草稿修订标识。</summary>
    public Guid RevisionId { get; set; }
    /// <summary>唯一命中的最高优先级绑定；选择前失败时为空。</summary>
    public Guid? BindingId { get; set; }
    /// <summary>成功、正常未匹配、拒绝或求值失败。</summary>
    public RuleExecutionStatus Status { get; set; }
    /// <summary>是否得到所有必需出口的有效值。</summary>
    public bool Success { get; set; }
    /// <summary>试算失败的稳定错误编码；成功时为空。</summary>
    public string? ErrorCode { get; set; }
    /// <summary>供维护者排查的错误说明；成功时为空。</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>发生错误的规则或赋值路径。</summary>
    public string? ErrorPath { get; set; }
    /// <summary>表达式错误的可选起始字符位置。</summary>
    public int? Start { get; set; }
    /// <summary>表达式错误的可选字符跨度。</summary>
    public int? Length { get; set; }
    /// <summary>已评估规则的条件结果，按实际执行顺序排列。</summary>
    public IReadOnlyList<RuleConditionResultDto> Conditions { get; set; } = [];
    /// <summary>本次实际发生的赋值及其结果。</summary>
    public IReadOnlyList<RuleAssignmentResultDto> Assignments { get; set; } = [];
    /// <summary>有序完整结果行，和运行接口使用相同的原生 JSON 结构。</summary>
    public IReadOnlyList<RuleResultRowDto> Results { get; set; } = [];
    /// <summary>输入或出口错误对应的变量编码。</summary>
    public string? ErrorVariableCode { get; set; }
}
