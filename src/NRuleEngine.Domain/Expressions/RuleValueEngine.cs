using System.Text.Json;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Expressions;

/// <summary>按出口结构解码、校验和构造赋值结果；子表达式仍采用单值 AST。</summary>
public static class RuleValueEngine
{
    /// <summary>候选列表的最大元素数量，限制编制和运行成本。</summary>
    public const int MaxListItems = 100;

    /// <summary>读取类型化常量，拒绝结构不符、非法区间和候选列表。</summary>
    public static object? ReadValue(IExpressionVariable target, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4096)
        {
            throw new ArgumentException("常量 JSON 为空或超过 4096 个字符。");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        object? value;
        switch (target.Shape)
        {
            case RuleValueShape.Scalar:
                value = RuleExpressionEngine.ReadConstant(target.ValueKind, root);
                break;
            case RuleValueShape.NumericRange:
                if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                    !root.TryGetProperty("min", out var min) || !root.TryGetProperty("max", out var max) ||
                    RuleExpressionEngine.ReadConstant(RuleValueKind.Decimal, min) is not decimal minimum ||
                    RuleExpressionEngine.ReadConstant(RuleValueKind.Decimal, max) is not decimal maximum)
                {
                    throw new ArgumentException("数值区间必须且只能包含非空数值 min 和 max。");
                }
                value = new RuleNumericRange(minimum, maximum);
                break;
            case RuleValueShape.List:
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() is < 1 or > MaxListItems)
                {
                    throw new ArgumentException("候选列表必须包含 1 到 100 项。");
                }
                value = root.EnumerateArray().Select(item => RuleExpressionEngine.ReadConstant(target.ValueKind, item)).ToArray();
                break;
            default:
                throw new ArgumentException("不支持的值结构。");
        }
        CheckValue(target, value);
        return value;
    }

    /// <summary>校验赋值表达式结构及每个子表达式的基础类型，合并全部变量引用。</summary>
    public static void ValidateExpression(IExpressionVariable target, string json,
        IReadOnlyCollection<IExpressionVariable> variables, out IReadOnlySet<Guid> references)
    {
        var root = RuleExpression.Parse(json).Root;
        if (target.Shape == RuleValueShape.Scalar)
        {
            var kind = RuleExpressionEngine.ValidateScalarNode(root, variables, out references);
            if (kind != target.ValueKind && !(kind is null && target.IsNullable))
            {
                throw new ArgumentException("赋值表达式的类型与目标变量类型不一致。");
            }
            return;
        }

        var nodes = GetParts(target, root);
        var keys = new HashSet<Guid>();
        foreach (var node in nodes)
        {
            var kind = RuleExpressionEngine.ValidateScalarNode(node, variables, out var childReferences);
            if (kind != target.ValueKind)
            {
                throw new RuleExpressionException("区间端点或候选项必须为与目标一致的非空单值表达式。", node.Start, node.Length);
            }
            keys.UnionWith(childReferences);
        }
        references = keys;
    }

    /// <summary>逐项计算复合赋值，并在返回前验证端点、精度和列表约束。</summary>
    public static object? EvaluateExpression(IExpressionVariable target, string json,
        IReadOnlyDictionary<Guid, object?> values, CancellationToken cancellationToken = default)
    {
        var root = RuleExpression.Parse(json).Root;
        object? value;
        if (target.Shape == RuleValueShape.Scalar)
        {
            value = RuleExpressionEngine.EvaluateScalarNode(root, values, cancellationToken);
        }
        else
        {
            var nodes = GetParts(target, root);
            var parts = nodes.Select(node => RuleExpressionEngine.EvaluateScalarNode(node, values, cancellationToken)).ToArray();
            if (target.Shape == RuleValueShape.NumericRange)
            {
                if (parts[0] is not decimal min || parts[1] is not decimal max)
                {
                    throw new ArgumentException("区间端点必须为非空数值。");
                }
                value = new RuleNumericRange(min, max);
            }
            else
            {
                value = parts;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CheckValue(target, value);
        }
        catch (ArgumentException ex)
        {
            throw new RuleExpressionException(ex.Message, root.Start, root.Length, ex);
        }
        return value;
    }

    /// <summary>验证完整结果；候选项保留顺序，禁止 null、空文本和重复值。</summary>
    public static void CheckValue(IExpressionVariable target, object? value)
    {
        switch (target.Shape)
        {
            case RuleValueShape.Scalar:
                RuleExpressionEngine.CheckValue(target, value);
                return;
            case RuleValueShape.NumericRange:
                if (target.ValueKind != RuleValueKind.Decimal || value is not RuleNumericRange range || range.Min > range.Max)
                {
                    throw new ArgumentException("数值区间的最小值必须小于或等于最大值。");
                }
                RuleExpressionEngine.CheckValue(target, range.Min);
                RuleExpressionEngine.CheckValue(target, range.Max);
                return;
            case RuleValueShape.List:
                if (value is not IReadOnlyList<object?> items || items.Count is < 1 or > MaxListItems)
                {
                    throw new ArgumentException("候选列表必须包含 1 到 100 项。");
                }
                var seen = new HashSet<object>();
                for (var index = 0; index < items.Count; index++)
                {
                    var item = items[index];
                    if (item is null || item is string text && string.IsNullOrWhiteSpace(text))
                    {
                        throw new ArgumentException($"第 {index + 1} 个候选值不能为空。");
                    }
                    RuleExpressionEngine.CheckValue(target, item);
                    if (!seen.Add(item))
                    {
                        throw new ArgumentException($"第 {index + 1} 个候选值重复。");
                    }
                }
                return;
            default:
                throw new ArgumentException("不支持的值结构。");
        }
    }

    private static IReadOnlyList<RuleExpressionNode> GetParts(IExpressionVariable target, RuleExpressionNode root)
    {
        if (root.Args is null || root.Args.Any(node => node is null))
        {
            throw new RuleExpressionException("区间端点和候选项必须包含有效子表达式。", root.Start, root.Length);
        }
        if (target.Shape == RuleValueShape.NumericRange && target.ValueKind == RuleValueKind.Decimal &&
            root.Op == "range" && root.Args?.Count == 2)
        {
            return root.Args;
        }
        if (target.Shape == RuleValueShape.List && root.Op == "list" && root.Args?.Count is >= 1 and <= MaxListItems)
        {
            return root.Args;
        }
        throw new RuleExpressionException("区间赋值需要 range(min, max)，候选列表赋值需要 list(1 到 100 个同类型单值)。", root.Start, root.Length);
    }
}
