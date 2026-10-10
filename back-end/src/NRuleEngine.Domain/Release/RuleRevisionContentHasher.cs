using System.Security.Cryptography;
using System.Text.Json;
using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.RuleModels;

namespace NRuleEngine.Domain.Release;

/// <summary>从不含数据库实体 ID 和审计信息的规范化语义快照生成内容哈希。</summary>
public static class RuleRevisionContentHasher
{
    /// <summary>计算带算法及快照版本前缀的 SHA-256 内容哈希。</summary>
    /// <remarks>核心逻辑：规范化修订内容后计算与实体标识无关的哈希；调用时机：发布时记录快照或运行时校验完整性时。</remarks>
    public static string Compute(string appCode, RuleModelRevision revision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = JsonSerializer.SerializeToElement(new
        {
            appCode,
            revision.ScenarioCode,
            revision.ContractVersion,
            revision.ExecutionMode,
            revision.MultipleWritePolicy,
            variables = revision.Variables.OrderBy(x => x.StableKey).Select(x => new
            {
                x.StableKey, x.Code, x.Direction, x.Origin, x.ValueKind, x.IsNullable, x.Precision, x.Scale,
                x.EnumCode, defaultValue = Parse(x.DefaultValueJson), x.Unit
            }),
            rules = revision.Rules.OrderByDescending(x => x.Priority).ThenBy(x => x.SortOrder).Select(x => new
            {
                x.Priority, x.SortOrder, x.IsEnabled, condition = Parse(x.ConditionAstJson),
                assignments = x.Assignments.OrderBy(a => a.SortOrder).Select(a => new
                {
                    a.TargetVariableStableKey, a.SortOrder, a.ValueSourceKind,
                    constant = Parse(a.ConstantValueJson), expression = Parse(a.ExpressionAstJson)
                })
            }),
            bindings = revision.Bindings.OrderByDescending(x => x.Priority)
                .ThenBy(BindingSortKey, StringComparer.Ordinal).Select(x => new
                {
                    x.Priority,
                    criteria = x.Criteria.OrderBy(c => c.DimensionCode).Select(c => new
                    {
                        c.DimensionCode, c.Operator, value = Parse(c.TypedValueJson)
                    })
                }),
            gateCases = revision.GateCases.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => new
            {
                x.Name, context = Parse(x.ContextJson), inputs = Parse(x.InputsJson),
                expectedOutputs = Parse(x.ExpectedOutputsJson)
            })
        });
        cancellationToken.ThrowIfCancellationRequested();
        return "sha256:v1:" + Convert.ToHexString(SHA256.HashData(CanonicalBytes(snapshot, cancellationToken)))
            .ToLowerInvariant();
    }

    private static string BindingSortKey(ScenarioBinding binding)
    {
        var criteria = JsonSerializer.SerializeToElement(binding.Criteria.OrderBy(x => x.DimensionCode)
            .Select(x => new { x.DimensionCode, x.Operator, value = Parse(x.TypedValueJson) }));
        return Convert.ToHexString(CanonicalBytes(criteria));
    }

    private static byte[] CanonicalBytes(JsonElement value, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, value, cancellationToken);
        }
        return stream.ToArray();
    }

    private static JsonElement? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value, cancellationToken);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray())
            {
                WriteCanonical(writer, item, cancellationToken);
            }
            writer.WriteEndArray();
        }
        else
        {
            value.WriteTo(writer);
        }
    }
}
