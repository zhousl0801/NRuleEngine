using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Rules;

/// <summary>在草稿中新增规则；表达式使用版本化 AST JSON。</summary>
[Serializable]
public class AddRuleDto
{
    /// <summary>便于维护者识别的规则名称。</summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;
    /// <summary>规则执行优先级；数值越大越先评估。</summary>
    public int Priority { get; set; }
    /// <summary>相同优先级下的唯一执行顺序值。</summary>
    public int SortOrder { get; set; }
    /// <summary>是否参与试算及后续发布执行。</summary>
    public bool IsEnabled { get; set; } = true;
    /// <summary>供编辑器展示的条件文本，不作为执行依据。</summary>
    public string? ConditionSource { get; set; }
    /// <summary>返回 Boolean 的版本化条件 AST JSON。</summary>
    [Required]
    public string ConditionAstJson { get; set; } = string.Empty;
    /// <summary>调用方读取草稿时获得的并发戳，用于阻止静默覆盖。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
