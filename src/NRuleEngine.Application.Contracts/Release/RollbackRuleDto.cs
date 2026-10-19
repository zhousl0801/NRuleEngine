using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>将模型重新指向已发布历史修订的请求。</summary>
[Serializable]
public class RollbackRuleDto
{
    /// <summary>需要重新激活的历史发布修订标识。</summary>
    public Guid TargetRevisionId { get; set; }
    /// <summary>调用方预期当前正在运行的修订标识。</summary>
    public Guid ExpectedActiveRevisionId { get; set; }
    /// <summary>调用方读取模型时获得的并发戳。</summary>
    [Required]
    public string ExpectedModelConcurrencyStamp { get; set; } = string.Empty;
    /// <summary>调用方读取历史发布修订时获得的并发戳。</summary>
    [Required]
    public string ExpectedTargetRevisionConcurrencyStamp { get; set; } = string.Empty;
}
