using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 创建草稿修订的输入参数。
/// </summary>
[Serializable]
public class CreateDraftDto
{
    /// <summary>
    /// 源修订标识。
    /// </summary>
    public Guid? BasedOnRevisionId { get; set; }

    /// <summary>
    /// 期望的模型并发戳。
    /// </summary>
    [Required]
    public string ExpectedModelConcurrencyStamp { get; set; } = string.Empty;

    public CreateDraftDto()
    {
    }

    public CreateDraftDto(Guid? basedOnRevisionId, string expectedModelConcurrencyStamp)
    {
        BasedOnRevisionId = basedOnRevisionId;
        ExpectedModelConcurrencyStamp = expectedModelConcurrencyStamp;
    }
}
