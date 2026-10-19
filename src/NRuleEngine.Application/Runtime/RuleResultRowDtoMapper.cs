using System.Text.Json;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Domain.Simulation;

namespace NRuleEngine.Application.Runtime;

/// <summary>为试算和运行统一转换原生 JSON 结果行。</summary>
internal static class RuleResultRowDtoMapper
{
    internal static IReadOnlyList<RuleResultRowDto> Map(IReadOnlyList<RuleResultRow> rows)
    {
        return rows.Select(row => new RuleResultRowDto
        {
            SourceRuleIds = row.SourceRuleIds,
            Outputs = row.Outputs.ToDictionary(x => x.Key, x => JsonSerializer.Deserialize<JsonElement>(x.Value), StringComparer.Ordinal)
        }).ToArray();
    }
}
