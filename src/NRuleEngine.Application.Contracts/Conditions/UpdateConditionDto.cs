using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>修改草稿中的唯一布尔条件和选择优先级。</summary>
[Serializable]
public sealed class UpdateConditionDto
{
    /// <summary>仅供展示的原始条件文本。</summary>
    [StringLength(8192)]
    public string? ConditionSource { get; set; }

    /// <summary>返回 Boolean 的版本化 AST JSON。</summary>
    [Required]
    public string ConditionAstJson { get; set; } = string.Empty;

    /// <summary>候选命中时的选择优先级。</summary>
    public int Priority { get; set; }

    /// <summary>草稿当前的乐观并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
