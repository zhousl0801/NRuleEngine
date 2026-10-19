namespace NRuleEngine.Application.Contracts.Simulation;

/// <summary>试算中一条规则的命中情况。</summary>
[Serializable]
public class RuleConditionResultDto
{
    /// <summary>被评估的规则标识。</summary>
    public Guid RuleId { get; set; }
    /// <summary>规则在试算时的展示名称。</summary>
    public string RuleName { get; set; } = string.Empty;
    /// <summary>该规则的条件是否命中。</summary>
    public bool Matched { get; set; }
}
