using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>发布条件修订，要求真、假两类回归样本。</summary>
[Serializable]
public sealed class PublishConditionSetDto
{
    /// <summary>条件集当前的乐观并发戳。</summary>
    [Required]
    public string ExpectedSetConcurrencyStamp { get; set; } = string.Empty;

    /// <summary>草稿当前的乐观并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;

    /// <summary>本次发布的维护说明。</summary>
    [StringLength(2048)]
    public string? ChangeSummary { get; set; }

    /// <summary>至少各含一条真、假预期的回归样本。</summary>
    [Required]
    public List<ConditionGateCaseDto> GateCases { get; set; } = [];
}
