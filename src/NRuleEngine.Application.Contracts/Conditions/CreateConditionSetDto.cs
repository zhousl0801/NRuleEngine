using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>按已注册条件场景建立条件集及首个草稿。</summary>
[Serializable]
public sealed class CreateConditionSetDto
{
    /// <summary>已注册的接入应用编码。</summary>
    [Required]
    [StringLength(64)]
    public string AppCode { get; set; } = string.Empty;

    /// <summary>已注册的条件判定场景编码。</summary>
    [Required]
    [StringLength(128)]
    public string ScenarioCode { get; set; } = string.Empty;

    /// <summary>要锁定的精确场景契约版本。</summary>
    [Required]
    [StringLength(64)]
    public string ContractVersion { get; set; } = string.Empty;

    /// <summary>应用内唯一、跨修订稳定的业务编码。</summary>
    [Required]
    [StringLength(128)]
    public string Code { get; set; } = string.Empty;

    /// <summary>条件集的展示名称。</summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>供维护者阅读的可选说明。</summary>
    [StringLength(2048)]
    public string? Description { get; set; }
}
