using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NRuleEngine.Domain.Expressions;

namespace NRuleEngine.Domain.Conditions;

/// <summary>计算发布内容摘要，覆盖条件 AST、优先级和固定输入契约。</summary>
public static class ConditionSetContentHasher
{
    /// <summary>基于确定顺序的语义快照生成版本化 SHA-256 摘要。</summary>
    public static string Compute(ConditionSetRevision revision)
    {
        if (revision.ConditionAstJson is null)
        {
            throw new InvalidOperationException("Condition is required.");
        }

        var content = new
        {
            revision.ScenarioCode,
            revision.ContractVersion,
            revision.Priority,
            Inputs = revision.Inputs.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => new
            {
                x.StableKey,
                x.Code,
                x.ValueKind,
                x.IsNullable,
                x.Precision,
                x.Scale,
                x.EnumCode
            }),
            Ast = RuleExpression.Parse(revision.ConditionAstJson)
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content));
        return "sha256:v1:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
