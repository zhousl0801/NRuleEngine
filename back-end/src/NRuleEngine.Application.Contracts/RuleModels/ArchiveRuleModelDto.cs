using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 归档规则模型的输入参数。
/// </summary>
[Serializable]
public class ArchiveRuleModelDto
{
    /// <summary>
    /// 期望的模型并发戳。
    /// </summary>
    [Required]
    public string ExpectedModelConcurrencyStamp { get; set; } = string.Empty;

    public ArchiveRuleModelDto()
    {
    }

    public ArchiveRuleModelDto(string expectedModelConcurrencyStamp)
    {
        ExpectedModelConcurrencyStamp = expectedModelConcurrencyStamp;
    }
}
