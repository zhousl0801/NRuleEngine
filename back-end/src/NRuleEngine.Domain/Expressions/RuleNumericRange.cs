using System.Text.Json.Serialization;

namespace NRuleEngine.Domain.Expressions;

/// <summary>使用 decimal 表示且包含两端的有限数值区间。</summary>
public sealed record RuleNumericRange(
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("max")] decimal Max);
