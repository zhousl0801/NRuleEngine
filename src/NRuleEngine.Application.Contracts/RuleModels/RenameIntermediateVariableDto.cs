using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 重命名中间变量的输入参数。
/// </summary>
[Serializable]
public class RenameIntermediateVariableDto
{
    /// <summary>
    /// 编码。
    /// </summary>
    [Required]
    [StringLength(128)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 名称。
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 期望的修订并发戳。
    /// </summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;

    public RenameIntermediateVariableDto()
    {
    }

    public RenameIntermediateVariableDto(string code, string name, string expectedRevisionConcurrencyStamp)
    {
        Code = code;
        Name = name;
        ExpectedRevisionConcurrencyStamp = expectedRevisionConcurrencyStamp;
    }
}
