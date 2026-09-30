namespace NRuleEngine.Application.Contracts.Release;

/// <summary>发布门禁回归用例及其输入、预期输出。</summary>
[Serializable]
public class ReleaseGateCaseDto
{
    /// <summary>本修订内的用例标识。</summary>
    public Guid Id { get; set; }
    /// <summary>用例名称。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>场景上下文 JSON 对象。</summary>
    public string ContextJson { get; set; } = "{}";
    /// <summary>入口值 JSON 对象。</summary>
    public string InputsJson { get; set; } = "{}";
    /// <summary>预期出口 JSON 对象。</summary>
    public string ExpectedOutputsJson { get; set; } = "{}";
}
