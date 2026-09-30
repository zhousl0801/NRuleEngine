namespace NRuleEngine.Application.Contracts.Bindings;

/// <summary>草稿绑定详情。</summary>
[Serializable]
public class ScenarioBindingDto
{
    /// <summary>场景绑定的唯一标识。</summary>
    public Guid Id { get; set; }
    /// <summary>绑定优先级；数值越大越优先。</summary>
    public int Priority { get; set; }
    /// <summary>绑定内按 AND 组合的维度条件。</summary>
    public IReadOnlyList<BindingCriterionDto> Criteria { get; set; } = [];
}
