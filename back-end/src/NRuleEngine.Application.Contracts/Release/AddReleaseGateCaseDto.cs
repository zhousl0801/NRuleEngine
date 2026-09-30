using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>向草稿加入一条发布前必须通过的回归用例。</summary>
[Serializable]
public class AddReleaseGateCaseDto
{
    /// <summary>修订内唯一的用例名称。</summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;
    /// <summary>绑定维度编码到 JSON 值的对象，例如 {"productKind":"A"}。</summary>
    [Required]
    public string ContextJson { get; set; } = "{}";
    /// <summary>入口变量编码到 JSON 值的对象。</summary>
    [Required]
    public string InputsJson { get; set; } = "{}";
    /// <summary>全部必需出口编码到预期 JSON 值的对象。</summary>
    [Required]
    public string ExpectedOutputsJson { get; set; } = "{}";
    /// <summary>调用方读取草稿时获得的并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
