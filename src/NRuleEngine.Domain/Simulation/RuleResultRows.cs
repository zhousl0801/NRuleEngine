using System.Text.Json;

namespace NRuleEngine.Domain.Simulation;

/// <summary>统一结果行的资源边界和原生 JSON 序列化。</summary>
public static class RuleResultRows
{
    /// <summary>一次执行最多返回的完整结果行数。</summary>
    public const int MaxRows = 100;

    /// <summary>results 数组的 UTF-8 JSON 最大字节数，包含来源标识和字段名。</summary>
    public const int MaxJsonBytes = 65536;

    /// <summary>返回原生 JSON 数组；门禁比较时可排除每次复制都会变化的来源标识。</summary>
    public static byte[] Serialize(IReadOnlyList<RuleResultRow> rows, bool includeSources = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                if (includeSources)
                {
                    writer.WritePropertyName("sourceRuleIds");
                    JsonSerializer.Serialize(writer, row.SourceRuleIds);
                    writer.WritePropertyName("outputs");
                    writer.WriteStartObject();
                }
                foreach (var output in row.Outputs.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(output.Key);
                    using var value = JsonDocument.Parse(output.Value);
                    value.RootElement.WriteTo(writer);
                }
                if (includeSources)
                {
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return stream.ToArray();
    }
}
