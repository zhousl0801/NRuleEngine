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
    /// <remarks>核心逻辑：反序列化首版 AST 并把格式错误保留为中文 JSON 异常；调用时机：校验或求值表达式前。</remarks>
    public static RuleExpression Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 65536)
        {
            throw new ArgumentException("表达式 JSON 为空或超过长度限制。");
        }

        RuleExpression expression;
        try
        {
            expression = JsonSerializer.Deserialize<RuleExpression>(json, JsonOptions)
                ?? throw new ArgumentException("必须提供表达式。");
        }
        catch (JsonException ex)
        {
            throw new JsonException("表达式 JSON 格式无效。", ex);
        }
        if (expression.Version != 1 || expression.Root is null)
        {
            throw new ArgumentException("不支持的表达式版本或缺少根节点。");
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
