using System.Text.Json;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Expressions;

/// <summary>
/// 对首版 AST 做静态类型检查并确定性求值；不调用外部代码。
/// </summary>
public static class RuleExpressionEngine
{
    /// <summary>检查 AST 节点类型和变量引用，返回表达式结果类型与引用集合。</summary>
    /// <remarks>核心逻辑：递归检查变量引用、函数参数和表达式静态类型；调用时机：保存条件或赋值 AST 时。</remarks>
    public static RuleValueKind? Validate(string json, IReadOnlyCollection<IExpressionVariable> variables, out IReadOnlySet<Guid> references)
    {
        return ValidateScalarNode(RuleExpression.Parse(json).Root, variables, out references);
    }

    /// <summary>检查复合赋值中的单值子表达式，并收集稳定键引用。</summary>
    internal static RuleValueKind? ValidateScalarNode(RuleExpressionNode node, IReadOnlyCollection<IExpressionVariable> variables, out IReadOnlySet<Guid> references)
    {
        var keys = new HashSet<Guid>();
        var result = TypeOf(node, variables.ToDictionary(v => v.StableKey), keys, 0);
        references = keys;
        return result;
    }

    /// <summary>使用已校验的 AST 和按 StableKey 索引的值计算表达式。</summary>
    /// <remarks>核心逻辑：根据已赋值变量求值并将失败定位到 AST 节点；调用时机：试算或运行规则时。</remarks>
    public static object? Evaluate(string json, IReadOnlyDictionary<Guid, object?> values, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EvaluateNode(RuleExpression.Parse(json).Root, values, 0, cancellationToken);
    }

    /// <summary>复用单值求值语义计算区间端点或候选项。</summary>
    internal static object? EvaluateScalarNode(RuleExpressionNode node, IReadOnlyDictionary<Guid, object?> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EvaluateNode(node, values, 0, cancellationToken);
    }

    /// <summary>为最深层失败节点保留源码位置，再交给上层报告。</summary>
    private static RuleValueKind? TypeOf(RuleExpressionNode node, IReadOnlyDictionary<Guid, IExpressionVariable> variables, HashSet<Guid> references, int depth)
    {
        CheckDepth(depth);
        try
        {
            return TypeOfCore(node, variables, references, depth);
        }
        catch (ArgumentException ex) when (ex is not RuleExpressionException)
        {
            throw new RuleExpressionException(RuleErrorText.Describe(ex), node.Start, node.Length, ex);
        }
    }

    /// <summary>只允许确定的节点及操作数类型，不解释任意方法调用。</summary>
    private static RuleValueKind? TypeOfCore(RuleExpressionNode node, IReadOnlyDictionary<Guid, IExpressionVariable> variables, HashSet<Guid> references, int depth)
    {
        var op = node.Op;
        if (op == "constant")
        {
            if (node.Kind is null || node.Value is null)
            {
                throw new ArgumentException("常量必须包含类型和值。");
            }

            ReadConstant(node.Kind.Value, node.Value.Value);
            return node.Value.Value.ValueKind == JsonValueKind.Null ? null : node.Kind;
        }

        if (op == "variable")
        {
            if (node.StableKey is null || !variables.TryGetValue(node.StableKey.Value, out var variable))
            {
                throw new ArgumentException("表达式引用了未知变量。");
            }

            if (variable.Shape != RuleValueShape.Scalar)
            {
                throw new ArgumentException("单值表达式不能引用区间或候选列表变量。");
            }

            references.Add(variable.StableKey);
            return variable.ValueKind;
        }

        var args = node.Args ?? throw new ArgumentException("必须提供表达式参数。");
        var types = args.Select(a => TypeOf(a, variables, references, depth + 1)).ToArray();
        switch (op)
        {
            case "and":
            case "or":
                RequireArity(types, 2);
                RequireAll(types, RuleValueKind.Boolean);
                return RuleValueKind.Boolean;
            case "not":
                RequireArity(types, 1);
                RequireAll(types, RuleValueKind.Boolean);
                return RuleValueKind.Boolean;
            case "eq":
            case "ne":
                RequireArity(types, 2);
                RequireSame(types);
                return RuleValueKind.Boolean;
            case "lt":
            case "lte":
            case "gt":
            case "gte":
                RequireArity(types, 2);
                RequireSame(types);
                if (types[0] is not (RuleValueKind.Decimal or RuleValueKind.String))
                {
                    throw new ArgumentException("顺序比较需要小数或字符串操作数。");
                }
                return RuleValueKind.Boolean;
            case "add":
            case "sub":
            case "mul":
            case "div":
                RequireArity(types, 2);
                RequireAll(types, RuleValueKind.Decimal);
                return RuleValueKind.Decimal;
            case "isNull":
                RequireArity(types, 1);
                return RuleValueKind.Boolean;
            case "in":
                if (types.Length < 2)
                {
                    throw new ArgumentException("In 运算需要一个值和至少一个候选值。");
                }
                RequireSame(types);
                return RuleValueKind.Boolean;
            case "function":
                return FunctionType(node.Function, types);
            default:
                throw new ArgumentException($"不支持的表达式操作：{op}。");
        }
    }

    /// <summary>按白名单函数签名推断结果类型。</summary>
    private static RuleValueKind? FunctionType(string? name, RuleValueKind?[] types)
    {
        switch (name)
        {
            case "Abs":
                RequireArity(types, 1);
                RequireAll(types, RuleValueKind.Decimal);
                return RuleValueKind.Decimal;
            case "Min":
            case "Max":
                RequireArity(types, 2);
                RequireAll(types, RuleValueKind.Decimal);
                return RuleValueKind.Decimal;
            case "Round":
                RequireArity(types, 2);
                RequireAll(types, RuleValueKind.Decimal);
                return RuleValueKind.Decimal;
            case "Coalesce":
                RequireArity(types, 2);
                RequireSame(types);
                return types[0] ?? types[1];
            default:
                throw new ArgumentException($"不支持的函数：{name}。");
        }
    }

    private static void RequireArity(RuleValueKind?[] types, int count)
    {
        if (types.Length != count)
        {
            throw new ArgumentException($"表达式需要 {count} 个参数。");
        }
    }

    private static void RequireAll(RuleValueKind?[] types, RuleValueKind kind)
    {
        if (types.Any(t => t != kind))
        {
            throw new ArgumentException($"表达式参数必须为 {kind} 类型。");
        }
    }

    private static void RequireSame(RuleValueKind?[] types)
    {
        var nonNull = types.Where(t => t is not null).Distinct().ToArray();
        if (nonNull.Length > 1)
        {
            throw new ArgumentException("表达式参数类型不兼容。");
        }
    }

    /// <summary>把 JSON 常量解码为 decimal、string、bool 或空值。</summary>
    public static object? ReadConstant(RuleValueKind kind, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return kind switch
        {
            RuleValueKind.Decimal when value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) => number,
            RuleValueKind.String or RuleValueKind.Enum when value.ValueKind == JsonValueKind.String => value.GetString(),
            RuleValueKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            _ => throw new ArgumentException("常量值与声明的类型不一致。")
        };
    }

    /// <summary>按声明类型读取独立 JSON 值，供输入、绑定和常量赋值复用。</summary>
    public static object? ReadValue(RuleValueKind kind, string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadConstant(kind, document.RootElement);
    }

    /// <summary>检查变量可空性、基础类型及 Decimal 精度和小数位。</summary>
    /// <remarks>核心逻辑：核对可空性、值类型及小数精度；调用时机：接收输入或维护默认值时。</remarks>
    public static void CheckValue(IExpressionVariable variable, object? value)
    {
        if (value is null)
        {
            if (!variable.IsNullable)
            {
                throw new ArgumentException($"变量 {variable.Code} 不能为 null。");
            }

            return;
        }

        if (variable.ValueKind switch
        {
            RuleValueKind.Decimal => value is decimal,
            RuleValueKind.String or RuleValueKind.Enum => value is string,
            RuleValueKind.Boolean => value is bool,
            _ => false
        } == false)
        {
            throw new ArgumentException($"变量 {variable.Code} 的值类型无效。");
        }

        if (value is decimal number)
        {
            var precision = variable.Precision ?? 28;
            var scale = variable.Scale ?? 10;
            var normalized = number.ToString(System.Globalization.CultureInfo.InvariantCulture).TrimStart('-');
            var parts = normalized.Split('.');
            var fraction = parts.Length == 2 ? parts[1].TrimEnd('0').Length : 0;
            var integer = parts[0].TrimStart('0').Length;
            if (fraction > scale || integer + fraction > precision || integer > precision - scale)
            {
                throw new ArgumentException($"变量 {variable.Code} 超过小数精度或小数位数限制。");
            }
        }
    }

    /// <summary>保留求值失败的节点位置，避免调用方解析异常文本。</summary>
    private static object? EvaluateNode(RuleExpressionNode node, IReadOnlyDictionary<Guid, object?> values, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckDepth(depth);
        try
        {
            return EvaluateNodeCore(node, values, depth, cancellationToken);
        }
        catch (Exception ex) when (ex is not RuleExpressionException &&
                                   ex is ArgumentException or InvalidOperationException or ArithmeticException or
                                       InvalidCastException or NullReferenceException)
        {
            throw new RuleExpressionException(RuleErrorText.Describe(ex), node.Start, node.Length, ex);
        }
    }

    /// <summary>按节点语义求值；逻辑运算与 Coalesce 保持短路。</summary>
    private static object? EvaluateNodeCore(RuleExpressionNode node, IReadOnlyDictionary<Guid, object?> values, int depth, CancellationToken cancellationToken)
    {
        if (node.Op == "constant")
        {
            return ReadConstant(node.Kind!.Value, node.Value!.Value);
        }

        if (node.Op == "variable")
        {
            return values.TryGetValue(node.StableKey!.Value, out var value) ? value
                : throw new InvalidOperationException("引用的变量没有值。");
        }

        if (node.Op == "and")
        {
            return (bool)EvaluateNode(node.Args[0], values, depth + 1, cancellationToken)! &&
                (bool)EvaluateNode(node.Args[1], values, depth + 1, cancellationToken)!;
        }

        if (node.Op == "or")
        {
            return (bool)EvaluateNode(node.Args[0], values, depth + 1, cancellationToken)! ||
                (bool)EvaluateNode(node.Args[1], values, depth + 1, cancellationToken)!;
        }

        if (node.Op == "function" && node.Function == "Coalesce")
        {
            return EvaluateNode(node.Args[0], values, depth + 1, cancellationToken) ??
                EvaluateNode(node.Args[1], values, depth + 1, cancellationToken);
        }

        var args = node.Args.Select(a => EvaluateNode(a, values, depth + 1, cancellationToken)).ToArray();
        return node.Op switch
        {
            "not" => !(bool)args[0]!,
            "eq" => Equals(args[0], args[1]),
            "ne" => !Equals(args[0], args[1]),
            "lt" => Compare(args[0], args[1]) < 0,
            "lte" => Compare(args[0], args[1]) <= 0,
            "gt" => Compare(args[0], args[1]) > 0,
            "gte" => Compare(args[0], args[1]) >= 0,
            "add" => (decimal)args[0]! + (decimal)args[1]!,
            "sub" => (decimal)args[0]! - (decimal)args[1]!,
            "mul" => (decimal)args[0]! * (decimal)args[1]!,
            "div" => (decimal)args[0]! / (decimal)args[1]!,
            "isNull" => args[0] is null,
            "in" => args.Skip(1).Any(a => Equals(a, args[0])),
            "function" => EvaluateFunction(node.Function!, args),
            _ => throw new InvalidOperationException("不支持的表达式操作。")
        };
    }

    private static int Compare(object? left, object? right)
    {
        if (left is decimal a && right is decimal b)
        {
            return a.CompareTo(b);
        }

        if (left is string x && right is string y)
        {
            return string.CompareOrdinal(x, y);
        }

        throw new InvalidOperationException("不能比较 null 或类型不兼容的值。");
    }

    /// <summary>仅计算已声明的纯函数，不执行反射或外部 I/O。</summary>
    private static object? EvaluateFunction(string name, object?[] args)
    {
        return name switch
        {
            "Abs" => Math.Abs((decimal)args[0]!),
            "Min" => Math.Min((decimal)args[0]!, (decimal)args[1]!),
            "Max" => Math.Max((decimal)args[0]!, (decimal)args[1]!),
            "Round" => Round((decimal)args[0]!, (decimal)args[1]!),
            "Coalesce" => args[0] ?? args[1],
            _ => throw new InvalidOperationException("不支持的函数。")
        };
    }

    /// <summary>限制递归深度，避免异常表达式耗尽调用栈。</summary>
    private static void CheckDepth(int depth)
    {
        if (depth > 64)
        {
            throw new ArgumentException("表达式嵌套层数超过限制。");
        }
    }

    /// <summary>要求 Round 位数是 0 到 28 的整数，避免隐式截断。</summary>
    private static decimal Round(decimal value, decimal digits)
    {
        if (digits != decimal.Truncate(digits) || digits is < 0 or > 28)
        {
            throw new ArgumentException("Round 的小数位数必须是 0 到 28 之间的整数。");
        }

        return Math.Round(value, (int)digits, MidpointRounding.AwayFromZero);
    }
}
