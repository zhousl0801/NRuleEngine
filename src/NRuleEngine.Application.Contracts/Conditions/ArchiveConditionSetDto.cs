using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>携带并发戳归档条件集。</summary>
[Serializable]
public sealed class ArchiveConditionSetDto
{
    /// <summary>条件集当前的乐观并发戳。</summary>
    [Required]
    public string ExpectedSetConcurrencyStamp { get; set; } = string.Empty;
}
