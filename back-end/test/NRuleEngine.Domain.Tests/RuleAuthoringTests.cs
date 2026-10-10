using NRuleEngine.Domain.Simulation;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.Validation;
using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class RuleAuthoringTests
{
    [Fact]
    public void SimulatesDraftAndKeepsRulesIndependentAcrossCopies()
    {
        // 用高低两档输入分别命中主规则与兜底规则，再确认复制后实体标识独立、变量稳定键不变。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var input = revision.Variables.Single(v => v.Code == "inputWidth");
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        var condition = Ast(new { op = "gt", args = new object[]
        {
            new { op = "variable", stableKey = input.StableKey },
            new { op = "constant", kind = RuleValueKind.Decimal, value = 100m }
        } });
        var assigned = Ast(new { op = "add", args = new object[]
        {
            new { op = "variable", stableKey = input.StableKey },
            new { op = "constant", kind = RuleValueKind.Decimal, value = 5m }
        } });
        var rule = revision.AddRule(Guid.NewGuid(), "wide", 10, 1, true, null, condition);
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Expression,
            null, null, assigned, 1);
        var fallback = revision.AddRule(Guid.NewGuid(), "default", 0, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        revision.AddAssignment(fallback.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "100", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1,
            [("productKind", RuleBindingOperator.Equals, "\"A\"")]);

        Assert.Empty(RuleRevisionValidator.Validate(revision, contract));
        var high = RuleSimulator.Simulate(revision, contract,
            new Dictionary<string, string> { ["productKind"] = "\"A\"" },
            new Dictionary<string, string> { ["inputWidth"] = "120", ["productKind"] = "\"A\"" });
        Assert.True(high.Success);
        Assert.Equal("125", high.Outputs["targetWidth"]);
        Assert.Single(high.Conditions);
        var low = RuleSimulator.Simulate(revision, contract,
            new Dictionary<string, string> { ["productKind"] = "\"A\"" },
            new Dictionary<string, string> { ["inputWidth"] = "80", ["productKind"] = "\"A\"" });
        Assert.True(low.Success);
        Assert.Equal("100", low.Outputs["targetWidth"]);
        Assert.Equal([false, true], low.Conditions.Select(c => c.Matched));

        var copy = RuleModelRevision.CopyDraft(Guid.NewGuid(), revision.RuleModelId, 2, revision);
        Assert.Equal(revision.Variables.Select(v => v.StableKey), copy.Variables.Select(v => v.StableKey));
        Assert.NotEqual(revision.Rules.First().Id, copy.Rules.First().Id);
        Assert.NotEqual(revision.Bindings.First().Id, copy.Bindings.First().Id);
        Assert.Empty(RuleRevisionValidator.Validate(copy, contract));
    }

    [Fact]
    public void RejectsInvalidExpressionAndAmbiguousBinding()
    {
        // 先验证未知变量和赋值类型会被拒绝，再构造同优先级重叠绑定检查歧义诊断。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        Assert.Throws<RuleExpressionException>(() => revision.AddRule(Guid.NewGuid(), "bad", 1, 1, true, null,
            Ast(new { op = "variable", stableKey = Guid.NewGuid() })));
        var rule = revision.AddRule(Guid.NewGuid(), "valid", 1, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        Assert.Throws<ArgumentException>(() => revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey,
            RuleValueSourceKind.Expression, null, null,
            Ast(new { op = "constant", kind = RuleValueKind.String, value = "wrong" }), 1));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "100", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1,
            [("productKind", RuleBindingOperator.In, "[\"A\",\"B\"]")]);
        revision.AddBinding(Guid.NewGuid(), contract, 1,
            [("productKind", RuleBindingOperator.Equals, "\"B\"")]);
        Assert.Contains(RuleRevisionValidator.Validate(revision, contract), i => i.Code == "AmbiguousBinding");
    }

    [Fact]
    public void RejectsUnknownInputAndUnsupportedAstVersion()
    {
        // 分别从 AST 版本和试算输入两条入口验证契约限制。
        var malformed = Assert.Throws<JsonException>(() => RuleExpression.Parse("{"));
        Assert.Contains("表达式 JSON 格式无效", malformed.Message);
        Assert.Throws<ArgumentException>(() => RuleExpression.Parse("{\"version\":2,\"root\":{\"op\":\"constant\"}}"));
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, SimulatedSizeExpansionScenario.Contract);
        var rule = revision.AddRule(Guid.NewGuid(), "all", 1, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "100", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), SimulatedSizeExpansionScenario.Contract, 1, []);
        var result = RuleSimulator.Simulate(revision, SimulatedSizeExpansionScenario.Contract,
            new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["inputWidth"] = "20", ["productKind"] = "\"A\"", ["unknown"] = "1"
            });
        Assert.False(result.Success);
        Assert.Equal("UnknownInput", result.ErrorCode);
    }

    [Fact]
    public void DetectsSequentialCycleAndRepeatedWrites()
    {
        // 构造顺序执行依赖和重复写入，确认校验器拦截不安全的规则组合。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        revision.SetExecutionSemantics(RuleExecutionMode.Sequential, MultipleWritePolicy.ErrorOnMultipleWrite);
        var a = revision.AddIntermediate(Guid.NewGuid(), "a", "A", new RuleTypeDescriptor(RuleValueKind.Decimal));
        var b = revision.AddIntermediate(Guid.NewGuid(), "b", "B", new RuleTypeDescriptor(RuleValueKind.Decimal));
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        var rule = revision.AddRule(Guid.NewGuid(), "first", 2, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), a.StableKey, RuleValueSourceKind.Expression,
            null, null, Ast(new { op = "variable", stableKey = b.StableKey }), 1);
        revision.AddAssignment(rule.Id, Guid.NewGuid(), b.StableKey, RuleValueSourceKind.Expression,
            null, null, Ast(new { op = "variable", stableKey = a.StableKey }), 2);
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "10", null, null, 3);
        var second = revision.AddRule(Guid.NewGuid(), "second", 1, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        revision.AddAssignment(second.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "20", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1, []);
        var issues = RuleRevisionValidator.Validate(revision, contract);
        Assert.Contains(issues, i => i.Code == "CyclicDependency");
        Assert.Contains(issues, i => i.Code == "MultipleWrite");
    }

    [Fact]
    public void ValidatesFunctionsTypesAndSourceLocation()
    {
        // 以错误函数参数触发表达式诊断，并核对错误位置随异常保留。
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1,
            SimulatedSizeExpansionScenario.Contract);
        var width = revision.Variables.Single(v => v.Code == "inputWidth");
        var rounded = Ast(new { op = "function", function = "Round", args = new object[]
        {
            new { op = "variable", stableKey = width.StableKey },
            new { op = "constant", kind = RuleValueKind.Decimal, value = 0m }
        } });
        Assert.Equal(RuleValueKind.Decimal, RuleExpressionEngine.Validate(rounded, revision.Variables, out _));
        Assert.Equal(3m, RuleExpressionEngine.Evaluate(rounded,
            new Dictionary<Guid, object?> { [width.StableKey] = 2.5m }));
        var invalid = Ast(new { op = "add", start = 8, length = 3, args = new object[]
        {
            new { op = "constant", kind = RuleValueKind.String, value = "x" },
            new { op = "constant", kind = RuleValueKind.Decimal, value = 1m }
        } });
        var error = Assert.Throws<RuleExpressionException>(() =>
            RuleExpressionEngine.Validate(invalid, revision.Variables, out _));
        Assert.Equal(8, error.Start);
        Assert.Equal(3, error.Length);
        Assert.Equal(RuleValueKind.Decimal, RuleExpressionEngine.Validate(
            "{\"version\":1,\"root\":{\"op\":\"constant\",\"kind\":\"Decimal\",\"value\":3}}",
            revision.Variables, out _));
    }

    [Fact]
    public void DisjointNumericRangesAreNotReportedAsAmbiguous()
    {
        // 用不相交的数值区间证明同优先级绑定可安全共存。
        var contract = SimulatedSizeExpansionScenario.Contract with
        {
            BindingDimensions = [new BindingDimensionContract("inputWidth", "Input width", RuleValueKind.Decimal,
                [RuleBindingOperator.GreaterThan, RuleBindingOperator.LessThan])]
        };
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        var rule = revision.AddRule(Guid.NewGuid(), "all", 1, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "10", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1,
            [("inputWidth", RuleBindingOperator.GreaterThan, "10")]);
        revision.AddBinding(Guid.NewGuid(), contract, 1,
            [("inputWidth", RuleBindingOperator.LessThan, "5")]);
        Assert.Empty(RuleRevisionValidator.Validate(revision, contract));
        var result = RuleSimulator.Simulate(revision, contract,
            new Dictionary<string, string> { ["inputWidth"] = "12" },
            new Dictionary<string, string> { ["inputWidth"] = "12", ["productKind"] = "\"A\"" });
        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void ContentHashIgnoresEntityIdsAndJsonPropertyOrderAcrossDraftCopies()
    {
        // 比较复制前后及 JSON 字段顺序变化后的哈希，确认只由业务内容决定。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var output = revision.Variables.Single(x => x.Code == "targetWidth");
        var rule = revision.AddRule(Guid.NewGuid(), "width", 1, 1, true, null,
            Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant,
            "125", null, null, 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1,
            [("productKind", RuleBindingOperator.Equals, "\"A\"")]);
        revision.AddGateCase(Guid.NewGuid(), "default", "{\"productKind\":\"A\"}",
            "{\"inputWidth\":120,\"productKind\":\"A\"}", "{\"targetWidth\":125}");
        Assert.Empty(ReleaseGateEvaluator.Evaluate(revision, contract));
        var hash = RuleRevisionContentHasher.Compute(contract.AppCode, revision);
        var copy = RuleModelRevision.CopyDraft(Guid.NewGuid(), revision.RuleModelId, 2, revision);
        Assert.NotEqual(revision.GateCases.Single().Id, copy.GateCases.Single().Id);
        copy.RemoveGateCase(copy.GateCases.Single().Id);
        copy.AddGateCase(Guid.NewGuid(), "default", "{\"productKind\":\"A\"}",
            "{\"productKind\":\"A\",\"inputWidth\":120}", "{\"targetWidth\":125}");
        Assert.Equal(hash, RuleRevisionContentHasher.Compute(contract.AppCode, copy));
    }

    private static string Ast(object root)
    {
        return JsonSerializer.Serialize(new { version = 1, root });
    }
}
