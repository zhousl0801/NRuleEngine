namespace NRuleEngine.Application.Contracts.Simulation;

/// <summary>一次试算中发生的赋值和值。</summary>
[Serializable]
public class RuleAssignmentResultDto
{
    /// <summary>实际执行本次赋值的规则标识。</summary>
    public Guid RuleId { get; set; }
    /// <summary>赋值定义的唯一标识。</summary>
    public Guid AssignmentId { get; set; }
    /// <summary>本次被写入变量的 StableKey。</summary>
    public Guid TargetVariableStableKey { get; set; }
    /// <summary>本次赋值计算出的 JSON 值。</summary>
    public string ValueJson { get; set; } = string.Empty;
}
