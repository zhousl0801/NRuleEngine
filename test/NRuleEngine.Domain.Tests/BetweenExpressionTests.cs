using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Simulation;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Validation;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class BetweenExpressionTests
{
    [Fact]
    public void EvaluatesInclusiveBoundaries()
    {
        // between 是闭区间：端点命中，区间外不命中。
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, SimulatedSizeExpansionScenario.Contract);
        var input = revision.Variables.Single(v => v.Code == "inputWidth");
        var expression = Ast(Between(input.StableKey, 100m, 120m));

        Assert.Equal(RuleValueKind.Boolean, RuleExpressionEngine.Validate(expression, revision.Variables, out var references));
        Assert.Contains(input.StableKey, references);

        foreach (var (value, expected) in new[]
                 {
                     (99m, false),
                     (100m, true),
                     (110m, true),
                     (120m, true),
                     (121m, false)
                 })
        {
            Assert.Equal(expected, RuleExpressionEngine.Evaluate(
                expression,
                new Dictionary<Guid, object?> { [input.StableKey] = value }));
        }
    }

    [Fact]
    public void RejectsWrongArityAndNonDecimalOperands()
    {
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, SimulatedSizeExpansionScenario.Contract);
        var input = revision.Variables.Single(v => v.Code == "inputWidth");
        var productKind = revision.Variables.Single(v => v.Code == "productKind");

        var missingMaximum = Ast(new
        {
            op = "between",
            args = new[] { Variable(input.StableKey), Number(100m) }
        });
        Assert.Contains("参数", Assert.Throws<RuleExpressionException>(
            () => RuleExpressionEngine.Validate(missingMaximum, revision.Variables, out _)).Message);

        var wrongType = Ast(new
        {
            op = "between",
            args = new[] { Variable(productKind.StableKey), Number(100m), Number(120m) }
        });
        Assert.Throws<RuleExpressionException>(
            () => RuleExpressionEngine.Validate(wrongType, revision.Variables, out _));
    }

    [Fact]
    public void RuleConditionUsesBetweenForHitDecision()
    {
        // 规则命中与不命中都通过 between 条件判定，覆盖端点和区间外。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var input = revision.Variables.Single(v => v.Code == "inputWidth");
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        var rule = revision.AddRule(Guid.NewGuid(), "width-band", 10, 1, true, null,
            Ast(Between(input.StableKey, 100m, 120m)));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant, "115", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1, [("productKind", RuleBindingOperator.Equals, "\"A\"")]);

        Assert.Empty(RuleRevisionValidator.Validate(revision, contract));

        var matched = RuleSimulator.Simulate(revision, contract,
            new Dictionary<string, string> { ["productKind"] = "\"A\"" },
            new Dictionary<string, string> { ["inputWidth"] = "100", ["productKind"] = "\"A\"" });
        Assert.True(matched.Success);
        Assert.Single(matched.Results);

        var missed = RuleSimulator.Simulate(revision, contract,
            new Dictionary<string, string> { ["productKind"] = "\"A\"" },
            new Dictionary<string, string> { ["inputWidth"] = "121", ["productKind"] = "\"A\"" });
        Assert.False(missed.Success);
        Assert.Equal("NoMatchingRule", missed.ErrorCode);
    }

    private static object Between(Guid stableKey, decimal minimum, decimal maximum)
    {
        return new
        {
            op = "between",
            args = new[] { Variable(stableKey), Number(minimum), Number(maximum) }
        };
    }

    private static object Variable(Guid stableKey)
    {
        return new { op = "variable", stableKey };
    }

    private static object Number(decimal value)
    {
        return new { op = "constant", kind = RuleValueKind.Decimal, value };
    }

    private static string Ast(object root)
    {
        return JsonSerializer.Serialize(new { version = 1, root });
    }
}
