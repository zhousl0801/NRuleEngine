namespace NRuleEngine.Domain.Simulation;

/// <summary>一组完整且相互对应的出口；来源标识只用于追踪，不参与门禁预期。</summary>
/// <param name="SourceRuleIds">按执行顺序排列的来源规则标识。</param>
/// <param name="Outputs">按出口编码索引的原生 JSON 文本。</param>
public sealed record RuleResultRow(IReadOnlyList<Guid> SourceRuleIds, IReadOnlyDictionary<string, string> Outputs);
