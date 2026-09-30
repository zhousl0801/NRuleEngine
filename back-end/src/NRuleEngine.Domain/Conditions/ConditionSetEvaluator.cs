using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Domain.Conditions;

/// <summary>只判定调用方给出的候选条件集，不查询全部规则或写入业务属性。</summary>
public static class ConditionSetEvaluator
{
    /// <summary>验证指定候选与输入后求值；无命中返回成功的空集合。</summary>
    public static ConditionEvaluationResult Evaluate(
        IReadOnlyCollection<ConditionCandidate> candidates,
        ConditionScenarioDescriptor contract,
        IReadOnlyDictionary<string, string> inputsJson,
        ConditionMatchMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            return Error("InvalidMode", "Unsupported match mode.");
        }

        if (candidates.Count == 0)
        {
            return Error("EmptyCandidates", "At least one candidate is required.");
        }

        if (candidates.Select(x => x.Set.Code).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            return Error("DuplicateCandidate", "Candidate codes must be unique.");
        }

        foreach (var candidate in candidates)
        {
            var set = candidate.Set;
            var revision = candidate.Revision;
            try
            {
                if (set.AppCode != contract.AppCode || set.ScenarioCode != contract.ScenarioCode ||
                    revision.ScenarioCode != contract.ScenarioCode || revision.ContractVersion != contract.ContractVersion ||
                    set.ActiveRevisionId != revision.Id || revision.ConditionSetId != set.Id ||
                    set.Status != RuleModelStatus.Active || revision.Status != RuleRevisionStatus.Published ||
                    revision.ConditionAstJson is null || revision.ContentHash != ConditionSetContentHasher.Compute(revision) ||
                    !SameInputs(revision, contract))
                {
                    return Error("InvalidCandidate", $"Candidate {set.Code} is not an active published revision of this contract.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            {
                return Error("InvalidCandidate", $"Candidate {set.Code}: {ex.Message}");
            }
        }

        var contractCodes = contract.Inputs.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        if (!contractCodes.SetEquals(inputsJson.Keys))
        {
            return Error("InvalidInput", "Input codes do not match the condition scenario contract.");
        }

        if (inputsJson.Values.Any(x => x is null || x.Length > 4096))
        {
            return Error("InvalidInput", "An input JSON value exceeds 4096 characters.");
        }

        var typedInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            foreach (var input in contract.Inputs)
            {
                var value = RuleExpressionEngine.ReadValue(input.Type.Kind, inputsJson[input.Code]);
                var candidateVariable = candidates.First().Revision.Inputs.Single(x => x.Code == input.Code);
                RuleExpressionEngine.CheckValue(candidateVariable, value);
                typedInputs.Add(input.Code, value);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Error("InvalidInput", ex.Message);
        }

        var matches = new List<ConditionMatch>();
        foreach (var candidate in candidates.OrderByDescending(x => x.Revision.Priority).ThenBy(x => x.Set.Code, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mode == ConditionMatchMode.FirstMatch && matches.Count > 0 &&
                candidate.Revision.Priority < matches[0].Priority)
            {
                break;
            }

            var set = candidate.Set;
            var revision = candidate.Revision;
            try
            {
                var values = revision.Inputs.ToDictionary(x => x.StableKey, x => typedInputs[x.Code]);
                if (RuleExpressionEngine.Evaluate(revision.ConditionAstJson!, values, cancellationToken) is not bool matched)
                {
                    return Error("EvaluationError", $"Candidate {set.Code} did not return Boolean.");
                }

                if (matched)
                {
                    matches.Add(new ConditionMatch(set.Code, set.Id, revision.Id, revision.Priority));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException)
            {
                return Error("EvaluationError", $"Candidate {set.Code}: {ex.Message}");
            }
        }

        if (mode == ConditionMatchMode.FirstMatch && matches.Count > 1 && matches[0].Priority == matches[1].Priority)
        {
            return Error("AmbiguousMatch", "Multiple candidates matched at the highest priority.");
        }

        return new ConditionEvaluationResult(mode == ConditionMatchMode.FirstMatch ? matches.Take(1).ToArray() : matches);
    }

    private static bool SameInputs(ConditionSetRevision revision, ConditionScenarioDescriptor contract)
    {
        return revision.Inputs.Count == contract.Inputs.Count && contract.Inputs.All(expected =>
            revision.Inputs.Any(actual => actual.Code == expected.Code && actual.ValueKind == expected.Type.Kind &&
                actual.IsNullable == expected.Type.IsNullable && actual.Precision == expected.Type.Precision &&
                actual.Scale == expected.Type.Scale && actual.EnumCode == expected.Type.EnumCode));
    }

    private static ConditionEvaluationResult Error(string code, string message)
    {
        return new ConditionEvaluationResult([], code, message);
    }
}
