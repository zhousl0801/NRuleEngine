using NRuleEngine.Domain.Bindings;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Validation;
using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Simulation;

/// <summary>对指定修订执行内存规则计算，不修改修订和发布状态。</summary>
public static class RuleSimulator
{
    /// <summary>校验并试算指定修订，不写数据库，也不改变活动修订。</summary>
    /// <remarks>核心逻辑：先选唯一绑定，再校验输入，按执行语义求值并检查必需出口；调用时机：维护者试算草稿或运行已发布修订时。</remarks>
    public static RuleSimulationOutcome Simulate(RuleModelRevision revision, RuleScenarioDescriptor contract, IReadOnlyDictionary<string, string> contextJson, IReadOnlyDictionary<string, string> inputJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var conditions = new List<RuleConditionOutcome>();
        var assignments = new List<RuleAssignmentOutcome>();

        RuleSimulationOutcome Failure(string code, string message, Guid? bindingId = null, string? path = null, int? start = null, int? length = null, string? variableCode = null)
        {
            return new RuleSimulationOutcome(revision.Id, bindingId, false, code, message, conditions, [])
            {
                ErrorPath = path, Start = start, Length = length, Assignments = assignments,
                ErrorVariableCode = variableCode
            };
        }

        var issues = RuleRevisionValidator.Validate(revision, contract, cancellationToken);
        if (issues.Count > 0)
        {
            return Failure("InvalidRevision", issues[0].Message);
        }

        // 试算只在本修订内裁决绑定；生产运行服务先在全部活动模型间裁决。
        var matches = revision.Bindings.Where(b => MatchesBinding(b, contract, contextJson, cancellationToken)).OrderByDescending(b => b.Priority).ToArray();

        if (matches.Length == 0)
        {
            return Failure("NoMatchingBinding", "没有绑定匹配给定场景上下文。");
        }

        if (matches.Length > 1 && matches[0].Priority == matches[1].Priority)
        {
            return Failure("AmbiguousBinding", "最高优先级存在多条命中的绑定。");
        }

        var bindingId = matches[0].Id;
        var inputs = revision.Variables.Where(v => v.Direction == RuleVariableDirection.Input).ToArray();
        var unknownInput = inputJson.Keys.Except(inputs.Select(v => v.Code), StringComparer.Ordinal).FirstOrDefault();
        if (unknownInput is not null)
        {
            return Failure("UnknownInput", "输入包含未声明的变量。", bindingId, variableCode: unknownInput);
        }

        var missingInput = inputs.FirstOrDefault(v => !inputJson.ContainsKey(v.Code) && v.DefaultValueJson is null);
        if (missingInput is not null)
        {
            return Failure("MissingInput", "缺少必需的输入变量。", bindingId, variableCode: missingInput.Code);
        }

        var values = new Dictionary<Guid, object?>();
        foreach (var variable in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? value;
            try
            {
                var json = inputJson.TryGetValue(variable.Code, out var supplied)
                    ? supplied
                    : variable.DefaultValueJson!;
                value = RuleExpressionEngine.ReadValue(variable.ValueKind, json);
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                return Failure("InputTypeMismatch", RuleErrorText.Describe(ex), bindingId, variableCode: variable.Code);
            }

            try
            {
                RuleExpressionEngine.CheckValue(variable, value);
            }
            catch (ArgumentException ex)
            {
                return Failure("InputConstraintViolated", RuleErrorText.Describe(ex), bindingId, variableCode: variable.Code);
            }

            values.Add(variable.StableKey, value);
        }

        // 默认值提供初始取值，不计入规则赋值次数。
        foreach (var variable in revision.Variables.Where(v => v.Direction == RuleVariableDirection.Intermediate &&
                                                               v.DefaultValueJson is not null))
        {
            values.Add(variable.StableKey, RuleExpressionEngine.ReadValue(variable.ValueKind, variable.DefaultValueJson!));
        }

        var initialValues = new Dictionary<Guid, object?>(values);
        var results = new List<RuleResultRow>();
        var sourceRuleIds = new List<Guid>();

        RuleSimulationOutcome? AppendResult(IReadOnlyList<Guid> sources)
        {
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var variable in revision.Variables.Where(v => v.Direction == RuleVariableDirection.Output))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!values.TryGetValue(variable.StableKey, out var value) || value is null)
                {
                    return Failure("MissingOutput", $"必需的输出变量 {variable.Code} 没有值。", bindingId, variableCode: variable.Code);
                }
                outputs.Add(variable.Code, JsonSerializer.Serialize(value));
            }
            results.Add(new RuleResultRow(sources.ToArray(), outputs));
            if (RuleResultRows.Serialize(results).Length > RuleResultRows.MaxJsonBytes)
            {
                return Failure("ResultLimitExceeded", "结果 JSON 超过 65536 字节限制。", bindingId);
            }
            return null;
        }

        // 收集模式每条规则使用同一份初始值；顺序模式才允许跨规则读取赋值。
        var written = new HashSet<Guid>();
        foreach (var rule in revision.Rules.Where(r => r.IsEnabled).OrderByDescending(r => r.Priority).ThenBy(r => r.SortOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (revision.ExecutionMode == RuleExecutionMode.CollectMatches)
            {
                values = new Dictionary<Guid, object?>(initialValues);
                written.Clear();
            }
            bool matched;
            try
            {
                matched = (bool)RuleExpressionEngine.Evaluate(rule.ConditionAstJson, values, cancellationToken)!;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or JsonException)
            {
                var location = ex as RuleExpressionException;
                return Failure("ConditionFailed", RuleErrorText.Describe(ex), bindingId, $"rules/{rule.Id}/condition", location?.Start, location?.Length);
            }

            conditions.Add(new RuleConditionOutcome(rule.Id, rule.Name, matched));
            if (!matched)
            {
                continue;
            }

            if (revision.ExecutionMode == RuleExecutionMode.CollectMatches && results.Count >= RuleResultRows.MaxRows)
            {
                return Failure("ResultLimitExceeded", "命中结果超过 100 行限制。", bindingId);
            }
            sourceRuleIds.Add(rule.Id);
            foreach (var assignment in rule.Assignments.OrderBy(a => a.SortOrder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (written.Contains(assignment.TargetVariableStableKey))
                {
                    if (revision.MultipleWritePolicy == MultipleWritePolicy.ErrorOnMultipleWrite)
                    {
                        return Failure("MultipleWrite", "同一变量被重复赋值。", bindingId);
                    }

                    if (revision.MultipleWritePolicy == MultipleWritePolicy.FirstWriteWins)
                    {
                        continue;
                    }
                }

                var target = revision.Variables.Single(v => v.StableKey == assignment.TargetVariableStableKey);
                try
                {
                    var value = assignment.ValueSourceKind == RuleValueSourceKind.Constant
                        ? RuleValueEngine.ReadValue(target, assignment.ConstantValueJson!)
                        : RuleValueEngine.EvaluateExpression(target, assignment.ExpressionAstJson!, values, cancellationToken);
                    values[target.StableKey] = value;
                    written.Add(target.StableKey);
                    assignments.Add(new RuleAssignmentOutcome(rule.Id, assignment.Id, target.StableKey, JsonSerializer.Serialize(value)));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or JsonException)
                {
                    var location = ex as RuleExpressionException;
                    return Failure("AssignmentFailed", RuleErrorText.Describe(ex), bindingId, $"rules/{rule.Id}/assignments/{assignment.Id}", location?.Start, location?.Length);
                }
            }

            if (revision.ExecutionMode == RuleExecutionMode.CollectMatches)
            {
                var failure = AppendResult([rule.Id]);
                if (failure is not null)
                {
                    return failure;
                }
            }
            if (revision.ExecutionMode == RuleExecutionMode.FirstMatch)
            {
                break;
            }
        }

        if (sourceRuleIds.Count == 0)
        {
            return Failure("NoMatchingRule", "当前模型没有规则命中。", bindingId);
        }
        if (revision.ExecutionMode != RuleExecutionMode.CollectMatches)
        {
            var failure = AppendResult(sourceRuleIds);
            if (failure is not null)
            {
                return failure;
            }
        }
        return new RuleSimulationOutcome(revision.Id, bindingId, true, null, null, conditions, results.ToArray())
        {
            Assignments = assignments
        };
    }

    /// <summary>按锁定契约解码上下文，并要求绑定中的所有维度条件成立。</summary>
    /// <remarks>核心逻辑：按注册契约逐项比较场景上下文与绑定条件；调用时机：试算或运行筛选候选绑定时。</remarks>
    public static bool MatchesBinding(ScenarioBinding binding, RuleScenarioDescriptor contract, IReadOnlyDictionary<string, string> contextJson, CancellationToken cancellationToken = default)
    {
        foreach (var criterion in binding.Criteria)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!contextJson.TryGetValue(criterion.DimensionCode, out var actualJson))
            {
                return false;
            }

            var dimension = contract.BindingDimensions.Single(d => d.Code == criterion.DimensionCode);
            object? actual;
            try
            {
                actual = RuleExpressionEngine.ReadValue(dimension.ValueKind, actualJson);
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                return false;
            }

            if (actual is null)
            {
                return false;
            }

            using var document = JsonDocument.Parse(criterion.TypedValueJson);
            if (criterion.Operator == RuleBindingOperator.In)
            {
                if (!document.RootElement.EnumerateArray().Any(v => Equals(actual, RuleExpressionEngine.ReadConstant(dimension.ValueKind, v))))
                {
                    return false;
                }

                continue;
            }

            var expected = RuleExpressionEngine.ReadConstant(dimension.ValueKind, document.RootElement);
            var comparison = actual is decimal a && expected is decimal b ? a.CompareTo(b)
                : actual is string x && expected is string y ? string.CompareOrdinal(x, y) : 0;
            var matched = criterion.Operator switch
            {
                RuleBindingOperator.Equals => Equals(actual, expected),
                RuleBindingOperator.GreaterThan => comparison > 0,
                RuleBindingOperator.GreaterThanOrEqual => comparison >= 0,
                RuleBindingOperator.LessThan => comparison < 0,
                RuleBindingOperator.LessThanOrEqual => comparison <= 0,
                _ => false
            };
            if (!matched)
            {
                return false;
            }
        }

        return true;
    }
}
