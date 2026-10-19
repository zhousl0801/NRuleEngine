using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>明确候选编码后进行条件匹配；不读取整个场景目录。</summary>
[Serializable]
public sealed class EvaluateConditionSetsDto
{
    /// <summary>代码注册的接入应用编码。</summary>
    [Required]
    public string AppCode { get; set; } = string.Empty;

    /// <summary>条件判定场景编码。</summary>
    [Required]
    public string ScenarioCode { get; set; } = string.Empty;

    /// <summary>要求匹配的精确输入契约版本。</summary>
    [Required]
    public string ContractVersion { get; set; } = string.Empty;

    /// <summary>业务方明确指定的 1 至 100 个条件集编码。</summary>
    [Required]
    public List<string> CandidateCodes { get; set; } = [];

    /// <summary>返回全部命中或裁决唯一最高优先级命中。</summary>
    public ConditionMatchMode Mode { get; set; } = ConditionMatchMode.AllMatches;

    /// <summary>按契约输入编码索引的原生 JSON 值。</summary>
    [Required]
    public Dictionary<string, JsonElement> Inputs { get; set; } = [];
}
