using System.ComponentModel.DataAnnotations;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 新增中间变量的输入参数。
/// </summary>
[Serializable]
public class AddIntermediateVariableDto
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
    /// 变量类型。
    /// </summary>
    [Required]
    public RuleTypeDescriptor Type { get; set; } = null!;

    /// <summary>
    /// 期望的修订并发戳。
    /// </summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;

    public AddIntermediateVariableDto()
    {
    }

    public AddIntermediateVariableDto(
        string code, string name, RuleTypeDescriptor type,
        string expectedRevisionConcurrencyStamp)
    {
        Code = code;
        Name = name;
        Type = type;
        ExpectedRevisionConcurrencyStamp = expectedRevisionConcurrencyStamp;
    }
}
