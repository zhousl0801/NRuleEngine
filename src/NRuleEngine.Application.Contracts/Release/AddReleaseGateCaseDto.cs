using System.ComponentModel.DataAnnotations;

using NRuleEngine.Domain.Shared.Runtime;

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
    /// <summary>有序出口对象数组，例如 [{"targetWidth":125}]，不包含来源规则标识。</summary>
    [Required]
    public string ExpectedResultsJson { get; set; } = "[]";

    /// <summary>预期成功或正常未匹配；未匹配时预期结果必须为空数组。</summary>
    public RuleExecutionStatus ExpectedStatus { get; set; } = RuleExecutionStatus.Succeeded;
    /// <summary>调用方读取草稿时获得的并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
