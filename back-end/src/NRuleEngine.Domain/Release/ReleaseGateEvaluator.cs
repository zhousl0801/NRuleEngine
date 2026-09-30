using System.Text.Json;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Simulation;

namespace NRuleEngine.Domain.Release;

/// <summary>用实际规则试算结果检验修订的发布回归用例。</summary>
public static class ReleaseGateEvaluator
{
    /// <summary>返回未通过用例名称及原因；空集合表示所有用例通过。</summary>
    public static IReadOnlyList<string> Evaluate(RuleModelRevision revision, RuleScenarioDescriptor contract)
    {
        var failures = new List<string>();
        if (revision.GateCases.Count == 0)
        {
            failures.Add("At least one release gate case is required.");
            return failures;
        }

        foreach (var gateCase in revision.GateCases)
        {
            var context = ReadValues(gateCase.ContextJson);
            var inputs = ReadValues(gateCase.InputsJson);
            var expected = ReadValues(gateCase.ExpectedOutputsJson);
            var outcome = RuleSimulator.Simulate(revision, contract, context, inputs);
            if (!outcome.Success)
            {
                failures.Add($"{gateCase.Name}: {outcome.ErrorCode}: {outcome.ErrorMessage}");
                continue;
            }
            if (!expected.Keys.Order(StringComparer.Ordinal).SequenceEqual(
                    outcome.Outputs.Keys.Order(StringComparer.Ordinal)) ||
                expected.Any(item => !SameJson(item.Value, outcome.Outputs[item.Key])))
            {
                failures.Add($"{gateCase.Name}: outputs differ from expected values.");
            }
        }
        return failures;
    }

    private static Dictionary<string, string> ReadValues(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value.GetRawText(), StringComparer.Ordinal);
    }

    private static bool SameJson(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        return JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement);
    }
}
