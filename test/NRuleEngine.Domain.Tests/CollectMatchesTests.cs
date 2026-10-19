using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.Domain.Simulation;
using NRuleEngine.Domain.Validation;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class CollectMatchesTests
{
    [Fact]
    public void IsolatesConditionsAndAssignmentsAndKeepsDuplicateValuesInDistinctRows()
    {
        var (revision, contract) = Create(2);
        var result = Run(revision, contract, 100);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Conditions, condition => Assert.True(condition.Matched));
        Assert.All(result.Results, row =>
        {
            Assert.Equal("101", row.Outputs["width"]);
            using var tolerance = JsonDocument.Parse(row.Outputs["tolerance"]);
            Assert.Equal(-1, tolerance.RootElement.GetProperty("min").GetDecimal());
            Assert.Equal(2, tolerance.RootElement.GetProperty("max").GetDecimal());
        });
        Assert.Equal(revision.Rules.OrderByDescending(x => x.Priority).ThenBy(x => x.SortOrder).Select(x => x.Id),
            result.Results.Select(row => Assert.Single(row.SourceRuleIds)));
    }

    [Fact]
    public void EvaluatesEveryMatchInPriorityThenSortOrderAndKeepsTheWholeTuple()
    {
        var (revision, contract) = Create(0);
        foreach (var (priority, order, value) in new[] { (1, 2, 200), (2, 1, 300), (1, 1, 100) })
        {
            var rule = revision.AddRule(Guid.NewGuid(), $"{value}", priority, order, true, null, Ast(Constant(true)));
            Assign(revision, rule.Id, "width", value.ToString(), 1);
            Assign(revision, rule.Id, "tolerance", $"{{\"min\":{-value},\"max\":{value}}}", 2);
        }
        var result = Run(revision, contract, 1);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { "300", "100", "200" }, result.Results.Select(x => x.Outputs["width"]));
        foreach (var row in result.Results)
        {
            using var tolerance = JsonDocument.Parse(row.Outputs["tolerance"]);
            Assert.Equal(decimal.Parse(row.Outputs["width"]), tolerance.RootElement.GetProperty("max").GetDecimal());
        }
        var reversed = System.Text.Encoding.UTF8.GetString(RuleResultRows.Serialize(result.Results.Reverse().ToArray(), includeSources: false));
        revision.AddGateCase(Guid.NewGuid(), "错误顺序", "{}", "{\"x\":1}", reversed);
        Assert.NotEmpty(ReleaseGateEvaluator.Evaluate(revision, contract));
    }

    [Fact]
    public void NoMatchAndLateEvaluationFailureNeverReturnPartialRows()
    {
        var (revision, contract) = Create(2);
        var none = Run(revision, contract, 0);
        Assert.Equal("NoMatchingRule", none.ErrorCode);
        Assert.Empty(none.Results);

        var last = revision.Rules.OrderByDescending(x => x.Priority).Last();
        var width = revision.Variables.Single(x => x.Code == "width");
        var assignment = last.Assignments.Single(x => x.TargetVariableStableKey == width.StableKey);
        revision.UpdateAssignment(last.Id, assignment.Id, width.StableKey, RuleValueSourceKind.Expression, null, null,
            Ast(new { op = "div", args = new[] { Constant(1), Constant(0) } }), assignment.SortOrder);
        var failed = Run(revision, contract, 1);
        Assert.Equal("AssignmentFailed", failed.ErrorCode);
        Assert.Contains(last.Id.ToString(), failed.ErrorPath);
        Assert.Empty(failed.Results);
        Assert.NotEmpty(failed.Assignments);
    }

    [Theory]
    [InlineData(RuleExecutionMode.FirstMatch)]
    [InlineData(RuleExecutionMode.Sequential)]
    public void SingleResultModesAlsoDistinguishNoMatchFromMissingOutputs(RuleExecutionMode mode)
    {
        var (revision, contract) = Create(1, RuleResultCardinality.Single);
        revision.SetExecutionSemantics(mode, MultipleWritePolicy.ErrorOnMultipleWrite);
        var result = Run(revision, contract, 0);
        Assert.Equal(RuleExecutionStatus.NotMatched, result.Status);
        Assert.Equal("NoMatchingRule", result.ErrorCode);
        Assert.Empty(result.Results);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void EnforcesMatchCountWithoutTruncation(int count, bool success)
    {
        var (revision, contract) = Create(count);
        var result = Run(revision, contract, 1);
        Assert.Equal(success, result.Success);
        if (success)
        {
            Assert.Equal(count, result.Results.Count);
        }
        else
        {
            Assert.Equal("ResultLimitExceeded", result.ErrorCode);
            Assert.Empty(result.Results);
        }
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void BoundsTheSerializedResultsIncludingTextAndSourceMetadata(int count, bool success)
    {
        var contract = new RuleScenarioDescriptor("TEST", "text-rows", "1", [],
            [new VariableContract("text", "文本", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.String))], [], RuleResultCardinality.Many);
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        for (var i = 0; i < count; i++)
        {
            var rule = revision.AddRule(Guid.NewGuid(), $"{i}", 0, i, true, null, Ast(Constant(true)));
            Assign(revision, rule.Id, "text", "\"" + new string('中', 3000) + "\"", 1);
        }
        revision.AddBinding(Guid.NewGuid(), contract, 1, []);
        var result = RuleSimulator.Simulate(revision, contract, new Dictionary<string, string>(), new Dictionary<string, string>());
        Assert.Equal(success, result.Success);
        if (success)
        {
            Assert.InRange(RuleResultRows.Serialize(result.Results).Length, 1, RuleResultRows.MaxJsonBytes);
        }
        else
        {
            Assert.Equal("ResultLimitExceeded", result.ErrorCode);
            Assert.Empty(result.Results);
        }
    }

    [Fact]
    public void RejectsCrossRuleReferencesMissingOutputsAndIncompatibleModes()
    {
        var (revision, contract) = Create(2);
        Assert.Throws<ArgumentException>(() => revision.SetExecutionSemantics(RuleExecutionMode.FirstMatch, MultipleWritePolicy.ErrorOnMultipleWrite));
        Assert.Throws<ArgumentException>(() => revision.SetExecutionSemantics(RuleExecutionMode.CollectMatches, MultipleWritePolicy.LastWriteWins));
        var single = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract with { ResultCardinality = RuleResultCardinality.Single });
        Assert.Throws<ArgumentException>(() => single.SetExecutionSemantics(RuleExecutionMode.CollectMatches, MultipleWritePolicy.ErrorOnMultipleWrite));
        Assert.Contains(RuleRevisionValidator.Validate(revision, contract with { ResultCardinality = RuleResultCardinality.Single }), x => x.Code == "InvalidExecutionMode");

        var middle = revision.Variables.Single(x => x.Code == "middle");
        revision.UpdateIntermediateProperties(middle.StableKey, null, null, null);
        Assert.Contains(RuleRevisionValidator.Validate(revision, contract), x => x.Code == "UninitializedVariable");
        var last = revision.Rules.Last();
        var width = revision.Variables.Single(x => x.Code == "width");
        revision.RemoveAssignment(last.Id, last.Assignments.Single(x => x.TargetVariableStableKey == width.StableKey).Id);
        Assert.Contains(RuleRevisionValidator.Validate(revision, contract), x => x.Code == "MissingOutput");
    }

    [Fact]
    public void PublishesOrderedRowExpectationsAndNoMatchWithoutPinningCopiedRuleIds()
    {
        var (revision, contract) = Create(2);
        const string row = "{\"width\":101.0,\"tolerance\":{\"max\":2,\"min\":-1}}";
        revision.AddGateCase(Guid.NewGuid(), "命中两行", "{}", "{\"x\":100}", $"[{row},{row}]");
        revision.AddGateCase(Guid.NewGuid(), "未命中", "{}", "{\"x\":0}", "[]", RuleExecutionStatus.NotMatched);
        Assert.Empty(ReleaseGateEvaluator.Evaluate(revision, contract));
        var copy = RuleModelRevision.CopyDraft(Guid.NewGuid(), revision.RuleModelId, 2, revision);
        Assert.Equal(RuleResultCardinality.Many, copy.ResultCardinality);
        Assert.Equal(RuleExecutionMode.CollectMatches, copy.ExecutionMode);
        Assert.NotEqual(revision.Rules.First().Id, copy.Rules.First().Id);
        Assert.Empty(ReleaseGateEvaluator.Evaluate(copy, contract));
        Assert.Equal(RuleRevisionContentHasher.Compute("TEST", revision), RuleRevisionContentHasher.Compute("TEST", copy));
        copy.AddGateCase(Guid.NewGuid(), "错误数量", "{}", "{\"x\":100}", $"[{row}]");
        Assert.Contains(ReleaseGateEvaluator.Evaluate(copy, contract), x => x.StartsWith("错误数量"));
    }

    [Fact]
    public void GateRejectsAnExpectedValueThatWouldUnderflowToTheActualZero()
    {
        var (revision, contract) = Create(0);
        var rule = revision.AddRule(Guid.NewGuid(), "零值", 1, 1, true, null, Ast(Constant(true)));
        Assign(revision, rule.Id, "width", "0", 1);
        Assign(revision, rule.Id, "tolerance", "{\"min\":-1,\"max\":2}", 2);
        revision.AddGateCase(Guid.NewGuid(), "不得下溢", "{}", "{\"x\":1}",
            "[{\"width\":1e-29,\"tolerance\":{\"min\":-1,\"max\":2}}]");
        Assert.True(Run(revision, contract, 1).Success);
        Assert.Contains(ReleaseGateEvaluator.Evaluate(revision, contract), failure => failure.StartsWith("不得下溢"));
    }

    [Fact]
    public void GatesRejectInvalidShapesDuplicateFieldsAndFailureOnlyExpectations()
    {
        var (revision, contract) = Create(1);
        Assert.Throws<ArgumentException>(() => revision.AddGateCase(Guid.NewGuid(), "旧形态", "{}", "{}", "{}"));
        Assert.Throws<ArgumentException>(() => revision.AddGateCase(Guid.NewGuid(), "重复", "{}", "{}", "[{\"width\":1,\"width\":2}]"));
        Assert.Throws<ArgumentException>(() => revision.AddGateCase(Guid.NewGuid(), "空成功", "{}", "{}", "[]"));
        Assert.Throws<ArgumentException>(() => revision.AddGateCase(Guid.NewGuid(), "失败", "{}", "{}", "[]", RuleExecutionStatus.Failed));
        revision.AddGateCase(Guid.NewGuid(), "仅未命中", "{}", "{\"x\":0}", "[]", RuleExecutionStatus.NotMatched);
        Assert.NotEmpty(ReleaseGateEvaluator.Evaluate(revision, contract));
    }

    [Fact]
    public void SequentialStillSharesValuesAndReturnsOneRowWithAllSources()
    {
        var (revision, contract) = Create(0, RuleResultCardinality.Single);
        revision.SetExecutionSemantics(RuleExecutionMode.Sequential, MultipleWritePolicy.ErrorOnMultipleWrite);
        var middle = revision.Variables.Single(x => x.Code == "middle");
        var first = revision.AddRule(Guid.NewGuid(), "准备", 2, 1, true, null, Ast(Constant(true)));
        Assign(revision, first.Id, "middle", "120", 1);
        var second = revision.AddRule(Guid.NewGuid(), "计算", 1, 1, true, null, Ast(new { op = "eq", args = new[] { Variable(middle.StableKey), Constant(120) } }));
        revision.AddAssignment(second.Id, Guid.NewGuid(), revision.Variables.Single(x => x.Code == "width").StableKey,
            RuleValueSourceKind.Expression, null, null, Ast(Variable(middle.StableKey)), 1);
        Assign(revision, second.Id, "tolerance", "{\"min\":0,\"max\":1}", 2);
        var result = Run(revision, contract, 1);
        Assert.True(result.Success, result.ErrorMessage);
        var row = Assert.Single(result.Results);
        Assert.Equal("120", row.Outputs["width"]);
        Assert.Equal(new[] { first.Id, second.Id }, row.SourceRuleIds);
    }

    private static (RuleModelRevision Revision, RuleScenarioDescriptor Contract) Create(int count, RuleResultCardinality cardinality = RuleResultCardinality.Many)
    {
        var contract = new RuleScenarioDescriptor("TEST", "matched-rows", "1",
            [new VariableContract("x", "输入", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new VariableContract("width", "宽度", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal)),
                new VariableContract("tolerance", "公差", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.NumericRange))], [], cardinality);
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var middle = revision.AddIntermediate(Guid.NewGuid(), "middle", "行内变量", new RuleTypeDescriptor(RuleValueKind.Decimal));
        revision.UpdateIntermediateProperties(middle.StableKey, "10", null, null);
        var input = revision.Variables.Single(x => x.Code == "x");
        for (var i = 0; i < count; i++)
        {
            var rule = revision.AddRule(Guid.NewGuid(), $"候选{i}", i, 1, true, null, Ast(new
            {
                op = "and", args = new object[]
                {
                    new { op = "gt", args = new[] { Variable(input.StableKey), Constant(0) } },
                    new { op = "eq", args = new[] { Variable(middle.StableKey), Constant(10) } }
                }
            }));
            revision.AddAssignment(rule.Id, Guid.NewGuid(), middle.StableKey, RuleValueSourceKind.Expression, null, null,
                Ast(new { op = "add", args = new[] { Variable(input.StableKey), Constant(1) } }), 1);
            revision.AddAssignment(rule.Id, Guid.NewGuid(), revision.Variables.Single(x => x.Code == "width").StableKey,
                RuleValueSourceKind.Expression, null, null, Ast(Variable(middle.StableKey)), 2);
            Assign(revision, rule.Id, "tolerance", "{\"min\":-1,\"max\":2}", 3);
        }
        revision.AddBinding(Guid.NewGuid(), contract, 1, []);
        return (revision, contract);
    }

    private static void Assign(RuleModelRevision revision, Guid ruleId, string code, string json, int order)
    {
        revision.AddAssignment(ruleId, Guid.NewGuid(), revision.Variables.Single(x => x.Code == code).StableKey,
            RuleValueSourceKind.Constant, json, null, null, order);
    }

    private static RuleSimulationOutcome Run(RuleModelRevision revision, RuleScenarioDescriptor contract, decimal input)
    {
        return RuleSimulator.Simulate(revision, contract, new Dictionary<string, string>(), new Dictionary<string, string> { ["x"] = JsonSerializer.Serialize(input) });
    }

    private static object Constant(object value)
    {
        return new { op = "constant", kind = value is bool ? RuleValueKind.Boolean : RuleValueKind.Decimal, value };
    }

    private static object Variable(Guid key)
    {
        return new { op = "variable", stableKey = key };
    }

    private static string Ast(object root)
    {
        return JsonSerializer.Serialize(new { version = 1, root });
    }
}
