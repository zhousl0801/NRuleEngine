using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 创建规则模型的输入参数。
/// </summary>
[Serializable]
public class CreateRuleModelDto
{
    /// <summary>
    /// 所属应用编码。
    /// </summary>
    [Required]
    [StringLength(64)]
    public string AppCode { get; set; } = string.Empty;

    /// <summary>
    /// 场景编码。
    /// </summary>
    [Required]
    [StringLength(128)]
    public string ScenarioCode { get; set; } = string.Empty;

    /// <summary>
    /// 场景契约版本。
    /// </summary>
    [Required]
    [StringLength(64)]
    public string ContractVersion { get; set; } = string.Empty;

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
    /// 说明。
    /// </summary>
    [StringLength(2048)]
    public string? Description { get; set; }

    public CreateRuleModelDto()
    {
    }

    public CreateRuleModelDto(
        string appCode, string scenarioCode, string contractVersion,
        string code, string name, string? description)
    {
        AppCode = appCode;
        ScenarioCode = scenarioCode;
        ContractVersion = contractVersion;
        Code = code;
        Name = name;
        Description = description;
    }
}
