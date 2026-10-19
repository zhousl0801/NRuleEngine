using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>把活动指针回滚到先前发布的修订。</summary>
[Serializable]
public sealed class RollbackConditionSetDto
{
    /// <summary>要重新激活的历史发布修订标识。</summary>
    public Guid TargetRevisionId { get; set; }

    /// <summary>条件集当前的乐观并发戳。</summary>
    [Required]
    public string ExpectedSetConcurrencyStamp { get; set; } = string.Empty;

    /// <summary>本次回滚的维护说明。</summary>
    [StringLength(2048)]
    public string? ChangeSummary { get; set; }
}
