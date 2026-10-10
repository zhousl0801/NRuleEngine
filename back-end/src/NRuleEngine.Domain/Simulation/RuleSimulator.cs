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
    /// <remarks>先选最高优先级的唯一匹配绑定，再验证输入，最后按修订语义执行规则。</remarks>
    public static RuleSimulationOutcome Simulate(RuleModelRevision revision, RuleScenarioDescriptor contract,
        IReadOnlyDictionary<string, string> contextJson, IReadOnlyDictionary<string, string> inputJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var conditions = new List<RuleConditionOutcome>();
        var assignments = new List<RuleAssignmentOutcome>();
        RuleSimulationOutcome Failure(string code, string message, Guid? bindingId = null,
            string? path = null, int? start = null, int? length = null, string? variableCode = null)
        {
            return new RuleSimulationOutcome(revision.Id, bindingId, false, code, message, conditions,
                new Dictionary<string, string>())
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
        var matches = revision.Bindings.Where(b => MatchesBinding(b, contract, contextJson, cancellationToken))
            .OrderByDescending(b => b.Priority).ToArray();
        if (matches.Length == 0)
        {
            return Failure("NoMatchingBinding", "No binding matches the supplied context.");
        }
        if (matches.Length > 1 && matches[0].Priority == matches[1].Priority)
        {
            return Failure("AmbiguousBinding", "Multiple bindings match at the highest priority.");
        }
        var bindingId = matches[0].Id;
        var inputs = revision.Variables.Where(v => v.Direction == RuleVariableDirection.Input).ToArray();
        var unknownInput = inputJson.Keys.Except(inputs.Select(v => v.Code), StringComparer.Ordinal).FirstOrDefault();
        if (unknownInput is not null)
        {
            return Failure("UnknownInput", "Input contains an undeclared variable.", bindingId,
                variableCode: unknownInput);
        }
        var missingInput = inputs.FirstOrDefault(v => !inputJson.ContainsKey(v.Code) && v.DefaultValueJson is null);
        if (missingInput is not null)
        {
            return Failure("MissingInput", "A required input is missing.", bindingId,
                variableCode: missingInput.Code);
        }
        var values = new Dictionary<Guid, object?>();
        foreach (var variable in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? value;
            try
            {
                var json = inputJson.TryGetValue(variable.Code, out var supplied)
                    ? supplied : variable.DefaultValueJson!;
                value = RuleExpressionEngine.ReadValue(variable.ValueKind, json);
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                return Failure("InputTypeMismatch", ex.Message, bindingId, variableCode: variable.Code);
            }
            try
            {
                RuleExpressionEngine.CheckValue(variable, value);
            }
            catch (ArgumentException ex)
            {
                return Failure("InputConstraintViolated", ex.Message, bindingId, variableCode: variable.Code);
            }
            values.Add(variable.StableKey, value);
        }

        // 默认值提供初始取值，不计入规则赋值次数。
        foreach (var variable in revision.Variables.Where(v => v.Direction == RuleVariableDirection.Intermediate &&
                     v.DefaultValueJson is not null))
        {
            values.Add(variable.StableKey,
                RuleExpressionEngine.ReadValue(variable.ValueKind, variable.DefaultValueJson!));
        }

        // Sequential 会把赋值写回上下文，供后续规则读取；FirstMatch 命中后立即停止。
        var written = new HashSet<Guid>();
        foreach (var rule in revision.Rules.Where(r => r.IsEnabled)
                     .OrderByDescending(r => r.Priority).ThenBy(r => r.SortOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool matched;
            try
            {
                matched = (bool)RuleExpressionEngine.Evaluate(rule.ConditionAstJson, values, cancellationToken)!;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or JsonException)
            {
                var location = ex as RuleExpressionException;
                return Failure("ConditionFailed", ex.Message, bindingId, $"rules/{rule.Id}/condition",
                    location?.Start, location?.Length);
            }
            conditions.Add(new RuleConditionOutcome(rule.Id, rule.Name, matched));
            if (!matched)
            {
                continue;
            }
            foreach (var assignment in rule.Assignments.OrderBy(a => a.SortOrder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (written.Contains(assignment.TargetVariableStableKey))
                {
                    if (revision.MultipleWritePolicy == MultipleWritePolicy.ErrorOnMultipleWrite)
                    {
                        return Failure("MultipleWrite", "A variable was assigned more than once.", bindingId);
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
                        ? RuleExpressionEngine.ReadValue(target.ValueKind, assignment.ConstantValueJson!)
                        : RuleExpressionEngine.Evaluate(assignment.ExpressionAstJson!, values, cancellationToken);
                    RuleExpressionEngine.CheckValue(target, value);
                    values[target.StableKey] = value;
                    written.Add(target.StableKey);
                    assignments.Add(new RuleAssignmentOutcome(rule.Id, assignment.Id, target.StableKey,
                        JsonSerializer.Serialize(value)));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or JsonException)
                {
                    var location = ex as RuleExpressionException;
                    return Failure("AssignmentFailed", ex.Message, bindingId,
                        $"rules/{rule.Id}/assignments/{assignment.Id}", location?.Start, location?.Length);
                }
            }
            if (revision.ExecutionMode == RuleExecutionMode.FirstMatch)
            {
                break;
            }
        }
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in revision.Variables.Where(v => v.Direction == RuleVariableDirection.Output))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!values.TryGetValue(variable.StableKey, out var value) || value is null)
            {
                return Failure("MissingOutput", $"Required output {variable.Code} has no value.", bindingId,
                    variableCode: variable.Code);
            }
            outputs.Add(variable.Code, JsonSerializer.Serialize(value));
        }
        return new RuleSimulationOutcome(revision.Id, bindingId, true, null, null, conditions, outputs)
        {
            Assignments = assignments
        };
    }

    /// <summary>按锁定契约解码上下文，并要求绑定中的所有维度条件成立。</summary>
    public static bool MatchesBinding(ScenarioBinding binding, RuleScenarioDescriptor contract,
        IReadOnlyDictionary<string, string> contextJson, CancellationToken cancellationToken = default)
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
                if (!document.RootElement.EnumerateArray().Any(v => Equals(actual,
                        RuleExpressionEngine.ReadConstant(dimension.ValueKind, v))))
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
