using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.RuleModels;
using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Validation;

/// <summary>检查草稿规则、写入、依赖和修订内的绑定歧义。</summary>
public static class RuleRevisionValidator
{
    /// <summary>对整份修订检查必需出口、表达式、依赖、写入冲突及本修订绑定歧义。</summary>
    /// <remarks>核心逻辑：综合检查出口覆盖、AST 类型、循环依赖和本修订绑定歧义；跨模型冲突由发布服务检查。调用时机：草稿试算及发布校验前。</remarks>
    public static IReadOnlyList<RuleValidationIssue> Validate(RuleModelRevision revision,
        RuleScenarioDescriptor contract, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<RuleValidationIssue>();
        if (revision.Rules.Count == 0 || !revision.Rules.Any(r => r.IsEnabled))
        {
            issues.Add(new RuleValidationIssue("NoRule", "rules", "至少需要一条已启用的规则。"));
        }
        if (revision.Bindings.Count == 0)
        {
            issues.Add(new RuleValidationIssue("NoBinding", "bindings", "至少需要一条场景绑定。"));
        }

        var requiredKeys = revision.Variables.Where(v => v.Direction == RuleVariableDirection.Output)
            .Select(v => v.StableKey).ToHashSet();
        var graph = new Dictionary<Guid, HashSet<Guid>>();
        var enabled = revision.Rules.Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority).ThenBy(r => r.SortOrder).ToArray();
        foreach (var rule in revision.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = $"rules/{rule.Id}";
            if (rule.Assignments.Count == 0)
            {
                issues.Add(new RuleValidationIssue("NoAssignment", path, "规则没有赋值定义。"));
            }
            if (revision.ExecutionMode == RuleExecutionMode.FirstMatch && rule.IsEnabled &&
                !requiredKeys.IsSubsetOf(rule.Assignments.Select(a => a.TargetVariableStableKey)))
            {
                issues.Add(new RuleValidationIssue("MissingOutput", path, "规则未对全部必需的输出变量赋值。"));
            }
            try
            {
                if (RuleExpressionEngine.Validate(rule.ConditionAstJson, revision.Variables, out _) != RuleValueKind.Boolean)
                {
                    throw new ArgumentException("条件表达式必须返回布尔值。");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                var location = ex as RuleExpressionException;
                issues.Add(new RuleValidationIssue("InvalidCondition", $"{path}/condition", RuleErrorText.Describe(ex),
                    location?.Start, location?.Length));
            }
            foreach (var assignment in rule.Assignments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = revision.Variables.SingleOrDefault(v => v.StableKey == assignment.TargetVariableStableKey);
                var assignmentPath = $"{path}/assignments/{assignment.Id}";
                if (target is null || target.Direction == RuleVariableDirection.Input)
                {
                    issues.Add(new RuleValidationIssue("InvalidTarget", assignmentPath, "赋值目标不存在或是输入变量。"));
                    continue;
                }
                if (assignment.ValueSourceKind == RuleValueSourceKind.Expression)
                {
                    try
                    {
                        var kind = RuleExpressionEngine.Validate(assignment.ExpressionAstJson!, revision.Variables,
                            out var references);
                        if (kind != target.ValueKind && !(kind is null && target.IsNullable))
                        {
                            throw new ArgumentException("赋值类型与目标变量类型不一致。");
                        }
                        graph.TryAdd(target.StableKey, []);
                        graph[target.StableKey].UnionWith(references.Where(k => revision.Variables
                            .Any(v => v.StableKey == k && v.Direction != RuleVariableDirection.Input)));
                    }
                    catch (Exception ex) when (ex is ArgumentException or JsonException)
                    {
                        var location = ex as RuleExpressionException;
                        issues.Add(new RuleValidationIssue("InvalidExpression", assignmentPath, RuleErrorText.Describe(ex),
                            location?.Start, location?.Length));
                    }
                }
                else
                {
                    try
                    {
                        RuleExpressionEngine.CheckValue(target,
                            RuleExpressionEngine.ReadValue(target.ValueKind, assignment.ConstantValueJson!));
                    }
                    catch (Exception ex) when (ex is ArgumentException or JsonException)
                    {
                        issues.Add(new RuleValidationIssue("InvalidConstant", assignmentPath, RuleErrorText.Describe(ex)));
                    }
                }
            }
        }

        if (revision.ExecutionMode == RuleExecutionMode.Sequential &&
            !requiredKeys.IsSubsetOf(enabled.SelectMany(r => r.Assignments).Select(a => a.TargetVariableStableKey)))
        {
            issues.Add(new RuleValidationIssue("MissingOutput", "rules", "已启用规则无法生成全部必需的输出变量。"));
        }
        if (revision.ExecutionMode == RuleExecutionMode.Sequential && HasCycle(graph))
        {
            issues.Add(new RuleValidationIssue("CyclicDependency", "rules", "赋值引用形成循环依赖。"));
        }
        if (revision.ExecutionMode == RuleExecutionMode.Sequential &&
            revision.MultipleWritePolicy == MultipleWritePolicy.ErrorOnMultipleWrite &&
            enabled.SelectMany(r => r.Assignments).GroupBy(a => a.TargetVariableStableKey).Any(g => g.Count() > 1))
        {
            issues.Add(new RuleValidationIssue("MultipleWrite", "rules", "已启用规则可能重复写入同一变量。"));
        }
        foreach (var binding in revision.Bindings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (binding.AppCode != contract.AppCode || binding.ScenarioCode != contract.ScenarioCode ||
                binding.ContractVersion != contract.ContractVersion)
            {
                issues.Add(new RuleValidationIssue("InvalidBinding", $"bindings/{binding.Id}", "绑定契约与修订不一致。"));
            }
        }
        var bindings = revision.Bindings.ToArray();
        for (var i = 0; i < bindings.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var j = i + 1; j < bindings.Length; j++)
            {
                if (bindings[i].Priority == bindings[j].Priority && BindingsMayOverlap(bindings[i], bindings[j], contract))
                {
                    issues.Add(new RuleValidationIssue("AmbiguousBinding", "bindings",
                        $"绑定 {bindings[i].Id} 与 {bindings[j].Id} 可能在相同优先级下同时命中。"));
                }
            }
        }
        return issues;
    }

    /// <summary>用深度优先遍历检测 Sequential 赋值依赖环。</summary>
    private static bool HasCycle(Dictionary<Guid, HashSet<Guid>> graph)
    {
        var visited = new HashSet<Guid>();
        var active = new HashSet<Guid>();
        bool Visit(Guid key)
        {
            if (active.Contains(key))
            {
                return true;
            }
            if (!visited.Add(key))
            {
                return false;
            }
            active.Add(key);
            foreach (var next in graph.GetValueOrDefault(key) ?? [])
            {
                if (Visit(next))
                {
                    return true;
                }
            }
            active.Remove(key);
            return false;
        }
        return graph.Keys.Any(Visit);
    }

    /// <summary>判断同一契约下的两条绑定是否可能同时命中；无法证明互斥时返回真。</summary>
    /// <remarks>核心逻辑：逐维寻找可证明互斥的条件，任一维度互斥即判定不重叠；调用时机：校验修订内或跨模型绑定冲突时。</remarks>
    public static bool BindingsMayOverlap(ScenarioBinding left, ScenarioBinding right, RuleScenarioDescriptor contract)
    {
        foreach (var dimension in contract.BindingDimensions)
        {
            var a = left.Criteria.SingleOrDefault(c => c.DimensionCode == dimension.Code);
            var b = right.Criteria.SingleOrDefault(c => c.DimensionCode == dimension.Code);
            if (a is null || b is null)
            {
                continue;
            }
            if (!ConstraintsIntersect(a, b, dimension.ValueKind))
            {
                return false;
            }
        }
        return true;
    }

    private static IEnumerable<object?> Values(RuleBindingCriterion criterion, RuleValueKind kind)
    {
        using var document = JsonDocument.Parse(criterion.TypedValueJson);
        if (criterion.Operator == RuleBindingOperator.In)
        {
            return document.RootElement.EnumerateArray()
                .Select(element => RuleExpressionEngine.ReadConstant(kind, element)).ToArray();
        }
        return [RuleExpressionEngine.ReadConstant(kind, document.RootElement)];
    }

    /// <summary>判定有限候选集合或上下界之间是否存在共同取值。</summary>
    private static bool ConstraintsIntersect(RuleBindingCriterion first, RuleBindingCriterion second, RuleValueKind kind)
    {
        var firstFinite = first.Operator is RuleBindingOperator.Equals or RuleBindingOperator.In;
        var secondFinite = second.Operator is RuleBindingOperator.Equals or RuleBindingOperator.In;
        if (firstFinite)
        {
            var candidates = Values(first, kind);
            return candidates.Any(value => Matches(second, kind, value));
        }
        if (secondFinite)
        {
            var candidates = Values(second, kind);
            return candidates.Any(value => Matches(first, kind, value));
        }
        var firstLower = first.Operator is RuleBindingOperator.GreaterThan or RuleBindingOperator.GreaterThanOrEqual;
        var secondLower = second.Operator is RuleBindingOperator.GreaterThan or RuleBindingOperator.GreaterThanOrEqual;
        if (firstLower == secondLower)
        {
            return true;
        }
        var lower = firstLower ? first : second;
        var upper = firstLower ? second : first;
        var low = Values(lower, kind).Single();
        var high = Values(upper, kind).Single();
        var comparison = Compare(low, high);
        return comparison < 0 || comparison == 0 &&
            lower.Operator == RuleBindingOperator.GreaterThanOrEqual &&
            upper.Operator == RuleBindingOperator.LessThanOrEqual;
    }

    private static bool Matches(RuleBindingCriterion criterion, RuleValueKind kind, object? actual)
    {
        if (criterion.Operator is RuleBindingOperator.Equals or RuleBindingOperator.In)
        {
            return Values(criterion, kind).Any(value => Equals(value, actual));
        }
        var expected = Values(criterion, kind).Single();
        var comparison = Compare(actual, expected);
        return criterion.Operator switch
        {
            RuleBindingOperator.GreaterThan => comparison > 0,
            RuleBindingOperator.GreaterThanOrEqual => comparison >= 0,
            RuleBindingOperator.LessThan => comparison < 0,
            RuleBindingOperator.LessThanOrEqual => comparison <= 0,
            _ => throw new ArgumentException("未知的绑定操作符。")
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
        throw new ArgumentException("绑定范围值必须是小数或字符串。");
    }
}
