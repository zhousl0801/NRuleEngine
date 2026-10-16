using System.Text.Json;

namespace NRuleEngine.Application.Contracts.Runtime;

/// <summary>试算和运行共同使用的一行完整出口。</summary>
[Serializable]
public class RuleResultRowDto
{
    /// <summary>本次修订中产生此行的规则，按执行顺序排列。</summary>
    public IReadOnlyList<Guid> SourceRuleIds { get; set; } = [];

    /// <summary>出口编码到原生 JSON 值的字典，区间和列表保持结构。</summary>
    public Dictionary<string, JsonElement> Outputs { get; set; } = [];
}
