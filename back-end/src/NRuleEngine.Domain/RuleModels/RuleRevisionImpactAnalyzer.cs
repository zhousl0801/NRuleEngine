using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.RuleModels;

/// <summary>按稳定键分析草稿中的直接引用和下游变量影响。</summary>
public static class RuleRevisionImpactAnalyzer
{
    /// <summary>读取一个变量的直接引用、下游变量和相关门禁用例路径。</summary>
    /// <remarks>核心逻辑：沿规则表达式和门禁用例收集直接及间接引用；调用时机：维护者查看变量引用或尝试删除时。</remarks>
    public static IReadOnlyList<RuleImpactReference> GetVariableReferences(RuleModelRevision revision, Guid stableKey)
    {
        _ = revision.GetVariable(stableKey);
        var references = new List<RuleImpactReference>();
        var edges = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var rule in revision.Rules)
        {
            var conditionPath = $"rules/{rule.Id}/condition";
            var conditionKeys = ReadKeys(rule.ConditionAstJson, conditionPath, references);
            if (conditionKeys.Contains(stableKey))
            {
                references.Add(new RuleImpactReference("Condition", conditionPath, true));
            }

            foreach (var assignment in rule.Assignments)
            {
                var path = $"rules/{rule.Id}/assignments/{assignment.Id}";
                if (assignment.TargetVariableStableKey == stableKey)
                {
                    references.Add(new RuleImpactReference("AssignmentTarget", $"{path}/target", true));
                }

                var expressionKeys = assignment.ValueSourceKind == RuleValueSourceKind.Expression
                    ? ReadKeys(assignment.ExpressionAstJson!, $"{path}/expression", references) : [];
                if (expressionKeys.Contains(stableKey))
                {
                    references.Add(new RuleImpactReference("AssignmentExpression", $"{path}/expression", true));
                }

                foreach (var source in conditionKeys.Concat(expressionKeys))
                {
                    edges.TryAdd(source, []);
                    edges[source].Add(assignment.TargetVariableStableKey);
                }
            }
        }

        var affected = new HashSet<Guid> { stableKey };
        var pending = new Queue<Guid>();
        pending.Enqueue(stableKey);
        while (pending.TryDequeue(out var key))
        {
            foreach (var target in edges.GetValueOrDefault(key) ?? [])
            {
                if (affected.Add(target))
                {
                    pending.Enqueue(target);
                    references.Add(new RuleImpactReference("DependentVariable", $"variables/{target}", false));
                }
            }
        }
        foreach (var gateCase in revision.GateCases)
        {
            foreach (var affectedVariable in revision.Variables.Where(v => affected.Contains(v.StableKey)))
            {
                if (affectedVariable.Direction == RuleVariableDirection.Input &&
                    ContainsProperty(gateCase.InputsJson, affectedVariable.Code))
                {
                    references.Add(new RuleImpactReference("GateCaseInput", $"gate-cases/{gateCase.Id}/inputs", true));
                }

                if (affectedVariable.Direction == RuleVariableDirection.Output &&
                    ContainsProperty(gateCase.ExpectedOutputsJson, affectedVariable.Code))
                {
                    references.Add(new RuleImpactReference("GateCaseOutput", $"gate-cases/{gateCase.Id}/outputs", affectedVariable.StableKey == stableKey));
                }
            }
        }
        return references.GroupBy(x => (x.Kind, x.Path, x.BlocksVariableDeletion)).Select(x => x.First()).OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
    }

    /// <summary>预览删除规则或赋值后可能受影响的目标变量及下游对象。</summary>
    /// <remarks>核心逻辑：追踪赋值目标的下游变量依赖；调用时机：维护者预览删除赋值时。</remarks>
    public static IReadOnlyList<RuleImpactReference> GetAssignmentRemovalImpact(RuleModelRevision revision, Guid ruleId, Guid assignmentId)
    {
        var assignment = revision.Rules.Single(r => r.Id == ruleId).Assignments.Single(a => a.Id == assignmentId);
        return [new RuleImpactReference("AffectedVariable", $"variables/{assignment.TargetVariableStableKey}", false),
            ..GetVariableReferences(revision, assignment.TargetVariableStableKey)];
    }

    /// <summary>预览删除规则后各赋值目标可能受影响的对象。</summary>
    /// <remarks>核心逻辑：汇总整条规则各赋值的下游影响；调用时机：维护者预览删除规则时。</remarks>
    public static IReadOnlyList<RuleImpactReference> GetRuleRemovalImpact(RuleModelRevision revision, Guid ruleId)
    {
        var rule = revision.Rules.Single(r => r.Id == ruleId);
        return rule.Assignments.SelectMany(a => GetAssignmentRemovalImpact(revision, ruleId, a.Id)).GroupBy(x => (x.Kind, x.Path, x.BlocksVariableDeletion)).Select(x => x.First()).OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
    }

    private static HashSet<Guid> ReadKeys(string json, string path, List<RuleImpactReference> references)
    {
        try
        {
            var keys = new HashSet<Guid>();
            Collect(RuleExpression.Parse(json).Root, keys);
            return keys;
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            // 内容不可解析时无法安全证明变量未被引用。
            references.Add(new RuleImpactReference("UnresolvedExpression", path, true));
            return [];
        }
    }

    private static void Collect(RuleExpressionNode node, HashSet<Guid> keys)
    {
        if (node.Op == "variable" && node.StableKey is Guid stableKey)
        {
            keys.Add(stableKey);
        }

        foreach (var child in node.Args ?? [])
        {
            Collect(child, keys);
        }
    }

    private static bool ContainsProperty(string json, string code)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(code, out _);
    }
}
