using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>发布时验证的输入与预期布尔结果。</summary>
[Serializable]
public sealed class ConditionGateCaseDto
{
    /// <summary>按场景输入编码索引的原生 JSON 值。</summary>
    [Required]
    public Dictionary<string, JsonElement> Inputs { get; set; } = [];

    /// <summary>该输入应得到的布尔命中结果。</summary>
    public bool ExpectedMatch { get; set; }
}
