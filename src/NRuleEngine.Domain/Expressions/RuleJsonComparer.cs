using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace NRuleEngine.Domain.Expressions;

/// <summary>.NET 8 可用的 JSON 语义比较：对象忽略字段顺序，数组保留顺序，数值精确比较。</summary>
public static class RuleJsonComparer
{
    /// <summary>比较合法的规则结果 JSON，不把 1 与 1.0 视为不同数值。</summary>
    public static bool Equals(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var left = expected.EnumerateObject().ToArray();
                var right = actual.EnumerateObject().ToArray();
                return left.Length == right.Length && left.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == left.Length &&
                    right.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == right.Length && left.All(x => actual.TryGetProperty(x.Name, out var value) && Equals(x.Value, value));
            case JsonValueKind.Array:
                return expected.GetArrayLength() == actual.GetArrayLength() &&
                    expected.EnumerateArray().Zip(actual.EnumerateArray()).All(pair => Equals(pair.First, pair.Second));
            case JsonValueKind.Number:
                return NormalizeNumber(expected.GetRawText()) == NormalizeNumber(actual.GetRawText());
            case JsonValueKind.String:
                return expected.GetString() == actual.GetString();
            default:
                return expected.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
        }
    }

    /// <summary>把已通过 JSON 语法校验的数值归一为有效数字和十进制指数，避免 decimal 转换舍入或下溢。</summary>
    private static (string Digits, BigInteger Exponent, bool Negative) NormalizeNumber(string number)
    {
        var exponentIndex = number.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? number : number[..exponentIndex];
        var exponent = exponentIndex < 0 ? BigInteger.Zero
            : BigInteger.Parse(number[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var negative = mantissa[0] == '-';
        if (negative)
        {
            mantissa = mantissa[1..];
        }
        var point = mantissa.IndexOf('.');
        var fractionLength = point < 0 ? 0 : mantissa.Length - point - 1;
        var digits = mantissa.Replace(".", string.Empty).TrimStart('0');
        if (digits.Length == 0)
        {
            return ("0", BigInteger.Zero, false);
        }
        var significant = digits.TrimEnd('0');
        return (significant, exponent - fractionLength + digits.Length - significant.Length, negative);
    }
}
