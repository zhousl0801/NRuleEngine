namespace NRuleEngine.Application.Contracts.Simulation;

/// <summary>指定草稿、场景上下文和入口值进行一次试算。值均为 JSON 文本。</summary>
[Serializable]
public class SimulateRuleDto
{
    /// <summary>本次要试算的草稿修订标识。</summary>
    public Guid RevisionId { get; set; }
    /// <summary>按绑定维度编码索引的 JSON 值文本。</summary>
    public Dictionary<string, string> ContextJson { get; set; } = [];
    /// <summary>按入口变量编码索引的 JSON 值文本。</summary>
    public Dictionary<string, string> InputsJson { get; set; } = [];
}
