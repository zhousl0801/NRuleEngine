using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 规则修订及其变量的详情。
/// </summary>
[Serializable]
public class RuleRevisionDto
{
    /// <summary>
    /// 标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属规则模型标识。
    /// </summary>
    public Guid RuleModelId { get; set; }

    /// <summary>
    /// 修订号。
    /// </summary>
    public int RevisionNo { get; set; }

    /// <summary>
    /// 源修订标识。
    /// </summary>
    public Guid? BasedOnRevisionId { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public RuleRevisionStatus Status { get; set; }

    /// <summary>
    /// 场景编码。
    /// </summary>
    public string ScenarioCode { get; set; } = string.Empty;

    /// <summary>
    /// 场景契约版本。
    /// </summary>
    public string ContractVersion { get; set; } = string.Empty;

    /// <summary>
    /// 执行方式。
    /// </summary>
    public RuleExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// 多次写入策略。
    /// </summary>
    public MultipleWritePolicy MultipleWritePolicy { get; set; }

    /// <summary>
    /// 并发戳。
    /// </summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;

    /// <summary>
    /// 变量集合。
    /// </summary>
    public IReadOnlyList<RuleVariableDto> Variables { get; set; } = Array.Empty<RuleVariableDto>();

    /// <summary>草稿规则及其赋值。</summary>
    public IReadOnlyList<RuleDefinitionDto> Rules { get; set; } = [];

    /// <summary>草稿场景绑定。</summary>
    public IReadOnlyList<ScenarioBindingDto> Bindings { get; set; } = [];

    /// <summary>本修订的发布门禁回归用例。</summary>
    public IReadOnlyList<ReleaseGateCaseDto> GateCases { get; set; } = [];

    public RuleRevisionDto()
    {
    }

    public RuleRevisionDto(
        Guid id, Guid ruleModelId, int revisionNo,
        Guid? basedOnRevisionId, RuleRevisionStatus status, string scenarioCode,
        string contractVersion, RuleExecutionMode executionMode, MultipleWritePolicy multipleWritePolicy,
        string concurrencyStamp, IReadOnlyList<RuleVariableDto> variables)
    {
        Id = id;
        RuleModelId = ruleModelId;
        RevisionNo = revisionNo;
        BasedOnRevisionId = basedOnRevisionId;
        Status = status;
        ScenarioCode = scenarioCode;
        ContractVersion = contractVersion;
        ExecutionMode = executionMode;
        MultipleWritePolicy = multipleWritePolicy;
        ConcurrencyStamp = concurrencyStamp;
        Variables = variables;
    }
}
