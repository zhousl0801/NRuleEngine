using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>完整替换用户中间变量的默认值与单位，保留当前精度。</summary>
[Serializable]
public class UpdateIntermediatePropertiesDto
{
    /// <summary>默认 JSON 值；null 清除配置。</summary>
    [StringLength(4096)]
    public string? DefaultValueJson { get; set; }

    /// <summary>单位；null 清除配置。</summary>
    [StringLength(64)]
    public string? Unit { get; set; }

    /// <summary>期望的修订并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
