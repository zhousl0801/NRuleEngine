using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>配置草稿契约入口的默认 JSON 值；null 清除默认值。</summary>
[Serializable]
public class SetInputDefaultDto
{
    /// <summary>默认 JSON 值，显式 JSON null 使用字符串 "null"。</summary>
    [StringLength(4096)]
    public string? DefaultValueJson { get; set; }

    /// <summary>期望的修订并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
