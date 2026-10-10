using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>从指定修订复制为独立的新规则模型及首个草稿。</summary>
[Serializable]
public class CopyRuleModelDto
{
    /// <summary>源修订标识。</summary>
    [Required]
    public Guid SourceRevisionId { get; set; }

    /// <summary>新模型编码。</summary>
    [Required]
    [StringLength(128)]
    public string Code { get; set; } = string.Empty;

    /// <summary>新模型名称。</summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>新模型说明。</summary>
    [StringLength(2048)]
    public string? Description { get; set; }

    /// <summary>复制前读取的源修订并发戳。</summary>
    [Required]
    public string ExpectedSourceRevisionConcurrencyStamp { get; set; } = string.Empty;
}
