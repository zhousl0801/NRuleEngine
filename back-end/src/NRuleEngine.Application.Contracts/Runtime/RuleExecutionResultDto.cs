using NRuleEngine.Domain.Shared.Runtime;

namespace NRuleEngine.Application.Contracts.Runtime;

/// <summary>生产运行的确定性结果；未预期系统故障通过异常返回。</summary>
[Serializable]
public class RuleExecutionResultDto
{
    /// <summary>本次运行的唯一关联标识。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>成功、未匹配、拒绝或计算失败。</summary>
    public RuleExecutionStatus Status { get; set; }
    /// <summary>选中的规则模型标识；选择前失败时为空。</summary>
    public Guid? RuleModelId { get; set; }
    /// <summary>选中的发布修订标识；选择前失败时为空。</summary>
    public Guid? RevisionId { get; set; }
    /// <summary>最终命中的场景绑定标识。</summary>
    public Guid? BindingId { get; set; }
    /// <summary>有序完整结果行；非成功状态为空。</summary>
    public IReadOnlyList<RuleResultRowDto> Results { get; set; } = [];
    /// <summary>非成功状态下的稳定错误和安全定位字段。</summary>
    public RuleExecutionErrorDto? Error { get; set; }
}
