using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Bindings;

/// <summary>给草稿增加场景绑定；所有条件按 AND 组合。</summary>
[Serializable]
public class AddBindingDto
{
    /// <summary>跨同一场景的活动模型比较的绑定优先级。</summary>
    public int Priority { get; set; }
    /// <summary>按 AND 关系组合的场景维度条件。</summary>
    public List<BindingCriterionDto> Criteria { get; set; } = [];
    /// <summary>调用方读取草稿时获得的并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
