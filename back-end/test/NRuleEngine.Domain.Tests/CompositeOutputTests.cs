using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Scenarios;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Simulation;
using NRuleEngine.Domain.Validation;
using Volo.Abp;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class CompositeOutputTests
{
    [Fact]
    public void ComputesRangeAndOrderedCandidatesWithScalarExpressions()
    {
        var contract = Contract();
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var input = revision.Variables.Single(v => v.Code == "width");
        var factor = revision.AddIntermediate(Guid.NewGuid(), "factor", "系数", new RuleTypeDescriptor(RuleValueKind.Decimal));
        revision.UpdateIntermediateProperties(factor.StableKey, "1.05", null, null);
        var rule = revision.AddRule(Guid.NewGuid(), "区间及候选", 1, 1, true, null, Ast(Constant(true, RuleValueKind.Boolean)));
        AddExpression(revision, rule.Id, "range", new
        {
            op = "range", args = new object[] { Variable(input.StableKey), new { op = "mul", args = new[] { Variable(input.StableKey), Variable(factor.StableKey) } } }
        }, 1);
        AddExpression(revision, rule.Id, "numbers", new { op = "list", args = new[] { Constant(140), Variable(input.StableKey), Constant(130) } }, 2);
        AddConstant(revision, rule.Id, "codes", "[\"B\",\"A\"]", 3);
        revision.AddBinding(Guid.NewGuid(), contract, 1, []);
        revision.AddGateCase(Guid.NewGuid(), "区间与顺序", "{}", "{\"width\":120}",
            "{\"range\":{\"min\":120,\"max\":126},\"numbers\":[140,120,130],\"codes\":[\"B\",\"A\"]}");

        Assert.Empty(RuleRevisionValidator.Validate(revision, contract));
        var result = RuleSimulator.Simulate(revision, contract, new Dictionary<string, string>(), new Dictionary<string, string> { ["width"] = "120" });
        Assert.True(result.Success, result.ErrorMessage);
        using var range = JsonDocument.Parse(result.Outputs["range"]);
        Assert.Equal(120, range.RootElement.GetProperty("min").GetDecimal());
        Assert.Equal(126, range.RootElement.GetProperty("max").GetDecimal());
        Assert.Equal("[140,120,130]", result.Outputs["numbers"]);
        Assert.Equal("[\"B\",\"A\"]", result.Outputs["codes"]);
        Assert.Empty(ReleaseGateEvaluator.Evaluate(revision, contract));
        Assert.Contains(RuleRevisionImpactAnalyzer.GetVariableReferences(revision, factor.StableKey), reference => reference.Kind == "AssignmentExpression");
        Assert.Throws<BusinessException>(() => revision.RemoveIntermediate(factor.StableKey));
        var copy = RuleModelRevision.CopyDraft(Guid.NewGuid(), revision.RuleModelId, 2, revision);
        Assert.Equal(RuleValueShape.NumericRange, copy.Variables.Single(v => v.Code == "range").Shape);
        Assert.Equal(RuleRevisionContentHasher.Compute("TEST", revision), RuleRevisionContentHasher.Compute("TEST", copy));
        var codes = copy.Rules.Single().Assignments.Single(a => a.TargetVariableStableKey == copy.Variables.Single(v => v.Code == "codes").StableKey);
        copy.UpdateAssignment(copy.Rules.Single().Id, codes.Id, codes.TargetVariableStableKey, RuleValueSourceKind.Constant, "[\"A\",\"B\"]", null, null, 3);
        Assert.NotEqual(RuleRevisionContentHasher.Compute("TEST", revision), RuleRevisionContentHasher.Compute("TEST", copy));
        Assert.NotEmpty(ReleaseGateEvaluator.Evaluate(copy, contract));
    }

    [Theory]
    [InlineData("range", "{\"min\":2,\"max\":1}")]
    [InlineData("range", "{\"min\":null,\"max\":1}")]
    [InlineData("range", "{\"min\":1,\"max\":2,\"other\":3}")]
    [InlineData("range", "{\"min\":1,\"min\":1}")]
    [InlineData("range", "{\"min\":\"1\",\"max\":2}")]
    [InlineData("range", "{\"min\":1.001,\"max\":2}")]
    [InlineData("numbers", "[]")]
    [InlineData("numbers", "[1,\"2\"]")]
    [InlineData("numbers", "[1,null]")]
    [InlineData("numbers", "[1,1.0]")]
    [InlineData("numbers", "[1,2.001]")]
    [InlineData("codes", "[\"A\",\" \"]")]
    [InlineData("codes", "[\"A\",\"A\"]")]
    public void RejectsInvalidConstantsAtSave(string outputCode, string value)
    {
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, Contract());
        var rule = revision.AddRule(Guid.NewGuid(), "无条件", 1, 1, true, null, Ast(Constant(true, RuleValueKind.Boolean)));
        Assert.Throws<ArgumentException>(() => AddConstant(revision, rule.Id, outputCode, value, 1));
    }

    [Fact]
    public void EnforcesBoundariesAndScalarReferences()
    {
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, Contract());
        var range = revision.Variables.Single(v => v.Code == "range");
        var list = revision.Variables.Single(v => v.Code == "numbers");
        Assert.Equal(new RuleNumericRange(-1, -1), RuleValueEngine.ReadValue(range, "{\"min\":-1,\"max\":-1}"));
        Assert.NotNull(RuleValueEngine.ReadValue(list, JsonSerializer.Serialize(Enumerable.Range(1, 100))));
        Assert.Throws<ArgumentException>(() => RuleValueEngine.ReadValue(list, JsonSerializer.Serialize(Enumerable.Range(1, 101))));
        Assert.Throws<RuleExpressionException>(() => RuleValueEngine.ValidateExpression(range, Ast(Constant(1)), revision.Variables, out _));
        Assert.Throws<RuleExpressionException>(() => RuleValueEngine.ValidateExpression(list, Ast(new { op = "list", args = new[] { Constant(1), Constant("A", RuleValueKind.String) } }), revision.Variables, out _));
        Assert.Throws<RuleExpressionException>(() => RuleValueEngine.ValidateExpression(list, Ast(new { op = "list", args = new[] { Constant((object?)null) } }), revision.Variables, out _));
        Assert.Throws<RuleExpressionException>(() => RuleExpressionEngine.Validate(Ast(Variable(range.StableKey)), revision.Variables, out _));
    }

    [Theory]
    [InlineData("range", "range")]
    [InlineData("numbers", "list")]
    public void RejectsInvalidComputedValuesAtRuntime(string outputCode, string op)
    {
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, Contract());
        var input = revision.Variables.Single(v => v.Code == "width");
        var target = revision.Variables.Single(v => v.Code == outputCode);
        var expression = Ast(new { op, start = 2, length = 10, args = new[] { Variable(input.StableKey), Constant(1) } });
        RuleValueEngine.ValidateExpression(target, expression, revision.Variables, out _);
        var error = Assert.Throws<RuleExpressionException>(() => RuleValueEngine.EvaluateExpression(target, expression, new Dictionary<Guid, object?> { [input.StableKey] = op == "range" ? 2m : 1m }));
        Assert.Equal(2, error.Start);
    }

    [Fact]
    public void OnlyAllowsNonNullableCompositeOutputsInContracts()
    {
        var catalog = new RuleCatalog();
        catalog.Add(new RuleAppDescriptor("TEST", "测试", "test"));
        catalog.Add(Contract());
        Assert.Throws<ArgumentException>(() => catalog.Add(Contract() with
        {
            ScenarioCode = "invalid-input",
            Inputs = [new VariableContract("width", "宽度", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.List))]
        }));
        foreach (var type in new[] { new RuleTypeDescriptor(RuleValueKind.String, Shape: RuleValueShape.NumericRange), new RuleTypeDescriptor(RuleValueKind.Decimal, true, Shape: RuleValueShape.List) })
        {
            Assert.Throws<ArgumentException>(() => catalog.Add(Contract() with
            {
                ScenarioCode = "invalid-output",
                RequiredOutputs = [new VariableContract("result", "结果", RuleVariableDirection.Output, type)]
            }));
        }
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, Contract());
        Assert.Throws<ArgumentException>(() => revision.AddIntermediate(Guid.NewGuid(), "list", "中间列表", new RuleTypeDescriptor(RuleValueKind.Decimal, Shape: RuleValueShape.List)));
    }

    private static RuleScenarioDescriptor Contract()
    {
        return new RuleScenarioDescriptor("TEST", "composite", "1.0",
            [new VariableContract("width", "宽度", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))],
            [new VariableContract("range", "区间", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Precision: 8, Scale: 2, Shape: RuleValueShape.NumericRange)),
                new VariableContract("numbers", "候选数值", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Decimal, Precision: 8, Scale: 2, Shape: RuleValueShape.List)),
                new VariableContract("codes", "候选编码", RuleVariableDirection.Output, new RuleTypeDescriptor(RuleValueKind.Enum, EnumCode: "codes", Shape: RuleValueShape.List))], []);
    }

    private static void AddExpression(RuleModelRevision revision, Guid ruleId, string code, object root, int order)
    {
        revision.AddAssignment(ruleId, Guid.NewGuid(), revision.Variables.Single(v => v.Code == code).StableKey, RuleValueSourceKind.Expression, null, null, Ast(root), order);
    }

    private static void AddConstant(RuleModelRevision revision, Guid ruleId, string code, string value, int order)
    {
        revision.AddAssignment(ruleId, Guid.NewGuid(), revision.Variables.Single(v => v.Code == code).StableKey, RuleValueSourceKind.Constant, value, null, null, order);
    }

    private static object Variable(Guid key)
    {
        return new { op = "variable", stableKey = key };
    }

    private static object Constant(object? value, RuleValueKind kind = RuleValueKind.Decimal)
    {
        return new { op = "constant", kind, value };
    }

    private static string Ast(object root)
    {
        return JsonSerializer.Serialize(new { version = 1, root });
    }
}
