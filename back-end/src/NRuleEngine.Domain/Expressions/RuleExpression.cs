using System.Text.Json;
using System.Text.Json.Serialization;

namespace NRuleEngine.Domain.Expressions;

/// <summary>
/// 首版结构化表达式。Version 固定为 1，变量引用只使用 StableKey。
/// </summary>
public sealed class RuleExpression
{
    /// <summary>AST 格式版本；当前只接受 1。</summary>
    public int Version { get; set; }
    /// <summary>本次表达式的根节点。</summary>
    public RuleExpressionNode Root { get; set; } = new();

    /// <summary>读取 JSON 并拒绝空内容、过大输入及未知版本。</summary>
    public static RuleExpression Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 65536)
        {
            throw new ArgumentException("Expression JSON is empty or exceeds the size limit.");
        }
        var expression = JsonSerializer.Deserialize<RuleExpression>(json, JsonOptions)
            ?? throw new ArgumentException("Expression is required.");
        if (expression.Version != 1 || expression.Root is null)
        {
            throw new ArgumentException("Unsupported expression version or missing root.");
        }
        return expression;
    }

    internal static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
