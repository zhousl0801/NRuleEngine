using System.Text.Json;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Expressions;

/// <summary>
/// 表达式节点。Op 是白名单操作名；常量使用 Kind 和 JSON Value。
/// </summary>
public sealed class RuleExpressionNode
{
    /// <summary>白名单中的节点操作名。</summary>
    public string Op { get; set; } = string.Empty;

    /// <summary>变量节点引用的逻辑稳定标识。</summary>
    public Guid? StableKey { get; set; }

    /// <summary>常量节点声明的基础类型。</summary>
    public RuleValueKind? Kind { get; set; }

    /// <summary>常量节点的 JSON 值，含显式 null。</summary>
    public JsonElement? Value { get; set; }

    /// <summary>函数节点调用的白名单函数名。</summary>
    public string? Function { get; set; }

    /// <summary>按操作符签名排列的子表达式。</summary>
    public List<RuleExpressionNode> Args { get; set; } = [];

    /// <summary>可选的源表达式起始字符位置，用于错误定位。</summary>
    public int? Start { get; set; }

    /// <summary>可选的源表达式字符跨度，用于错误定位。</summary>
    public int? Length { get; set; }
}
