namespace NRuleEngine.Domain.Simulation;

/// <summary>试算中实际发生的一次赋值。</summary>
/// <param name="RuleId">执行该赋值的规则标识。</param>
/// <param name="AssignmentId">赋值定义的标识。</param>
/// <param name="TargetVariableStableKey">被写入变量的稳定标识。</param>
/// <param name="ValueJson">求值后的 JSON 值。</param>
public sealed record RuleAssignmentOutcome(Guid RuleId, Guid AssignmentId, Guid TargetVariableStableKey, string ValueJson);
