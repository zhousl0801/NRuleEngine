using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NRuleEngine.Application.Contracts.Runtime;

/// <summary>按应用、场景和业务上下文执行当前发布规则。</summary>
[Serializable]
public class ExecuteRuleDto
{
    /// <summary>代码注册的接入应用编码。</summary>
    [Required]
    public string AppCode { get; set; } = string.Empty;
    /// <summary>应用内的场景编码。</summary>
    [Required]
    public string ScenarioCode { get; set; } = string.Empty;
    /// <summary>可选的精确契约版本；指定时只考虑该版本的活动修订。</summary>
    public string? ContractVersion { get; set; }
    /// <summary>按绑定维度编码索引的原生 JSON 值。</summary>
    [Required]
    public Dictionary<string, JsonElement> Context { get; set; } = [];
    /// <summary>按入口变量编码索引的原生 JSON 值。</summary>
    [Required]
    public Dictionary<string, JsonElement> Inputs { get; set; } = [];
}
