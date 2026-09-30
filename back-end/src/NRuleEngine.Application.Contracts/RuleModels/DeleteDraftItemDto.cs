using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>删除草稿内容时的期望修订并发戳。</summary>
[Serializable]
public class DeleteDraftItemDto
{
    /// <summary>读取草稿时获得的并发戳，删除前用于阻止静默覆盖。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
