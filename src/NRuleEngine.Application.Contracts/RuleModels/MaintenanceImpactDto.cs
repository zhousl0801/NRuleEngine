namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>删除或归档前的只读影响摘要；BlocksOperation 只反映引用阻塞。</summary>
[Serializable]
public class MaintenanceImpactDto
{
    /// <summary>Model、Variable、Rule 或 Assignment。</summary>
    public string TargetKind { get; set; } = string.Empty;
    /// <summary>本次预览的对象标识。</summary>
    public Guid TargetId { get; set; }
    /// <summary>执行后续写操作时应提交的当前并发戳。</summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;
    /// <summary>引用是否阻止目标操作；归档、规则和赋值删除均为 false。</summary>
    public bool BlocksOperation { get; set; }
    /// <summary>直接引用及下游影响路径。</summary>
    public IReadOnlyList<ImpactReferenceDto> References { get; set; } = [];
}
