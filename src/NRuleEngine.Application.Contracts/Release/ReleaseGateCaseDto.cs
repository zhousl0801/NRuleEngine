using NRuleEngine.Domain.Shared.Runtime;

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
    /// <summary>有序的预期出口对象数组，不包含来源规则标识。</summary>
    public string ExpectedResultsJson { get; set; } = "[]";

    /// <summary>预期成功或正常未匹配；未匹配时预期结果必须为空数组。</summary>
    public RuleExecutionStatus ExpectedStatus { get; set; } = RuleExecutionStatus.Succeeded;
}
