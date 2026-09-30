namespace NRuleEngine.Domain.Shared.Runtime;

/// <summary>一次生产运行的确定性结果状态。</summary>
public enum RuleExecutionStatus
{
    /// <summary>选中唯一发布修订并产出全部必需出口。</summary>
    Succeeded,
    /// <summary>有活动发布模型，但上下文未命中任何绑定。</summary>
    NotMatched,
    /// <summary>请求、契约或发布配置不满足执行前提。</summary>
    Rejected,
    /// <summary>已进入规则计算，但赋值或输出产生失败。</summary>
    Failed
}
