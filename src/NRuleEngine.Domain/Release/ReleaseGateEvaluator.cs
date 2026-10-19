using System.Text.Json;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Simulation;

namespace NRuleEngine.Domain.Release;

/// <summary>用实际规则试算结果检验修订的发布回归用例。</summary>
public static class ReleaseGateEvaluator
{
    /// <summary>返回未通过用例名称及原因；空集合表示所有用例通过。</summary>
    /// <remarks>核心逻辑：逐例试算并比较出口 JSON 的语义值；调用时机：发布规则修订的门禁阶段。</remarks>
    public static IReadOnlyList<string> Evaluate(RuleModelRevision revision, RuleScenarioDescriptor contract)
    {
        var failures = new List<string>();
        if (!revision.GateCases.Any(x => x.ExpectedStatus == RuleExecutionStatus.Succeeded))
        {
            failures.Add("至少需要一条预期成功的发布门禁用例。");
            return failures;
        }

        foreach (var gateCase in revision.GateCases)
        {
            var context = ReadValues(gateCase.ContextJson);
            var inputs = ReadValues(gateCase.InputsJson);
            var outcome = RuleSimulator.Simulate(revision, contract, context, inputs);
            var status = outcome.Status;
            if (status != gateCase.ExpectedStatus)
            {
                failures.Add($"{gateCase.Name}: 实际状态 {status} 与预期 {gateCase.ExpectedStatus} 不一致；{outcome.ErrorCode}: {outcome.ErrorMessage}");
                continue;
            }
            using var expected = JsonDocument.Parse(gateCase.ExpectedResultsJson);
            using var actual = JsonDocument.Parse(RuleResultRows.Serialize(outcome.Results, includeSources: false));
            if (!RuleJsonComparer.Equals(expected.RootElement, actual.RootElement))
            {
                failures.Add($"{gateCase.Name}：结果行数量、顺序或出口与预期不一致。");
            }
        }
        return failures;
    }

    private static Dictionary<string, string> ReadValues(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText(), StringComparer.Ordinal);
    }

}
