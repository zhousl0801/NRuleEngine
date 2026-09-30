using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>提交草稿发布所需的双并发戳和变更说明。</summary>
[Serializable]
public class PublishRuleDto
{
    /// <summary>调用方读取模型时获得的并发戳。</summary>
    [Required]
    public string ExpectedModelConcurrencyStamp { get; set; } = string.Empty;
    /// <summary>调用方读取草稿时获得的并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
    /// <summary>审阅者可读的本次修订变更说明。</summary>
    [StringLength(2048)]
    public string? ChangeSummary { get; set; }
}
