using System.Text.Json;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Simulation;
using Volo.Abp;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class RuleMaintenanceTests
{
    [Fact]
    public void DefaultsApplyOnlyWhenInputIsAbsentOrIntermediateHasNotBeenAssigned()
    {
        // 分别覆盖缺省输入、显式 null 和中间变量赋值，确认默认值只在缺失时生效。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var input = revision.Variables.Single(v => v.Code == "inputWidth");
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        var middle = revision.AddIntermediate(Guid.NewGuid(), "extra", "Extra", new RuleTypeDescriptor(RuleValueKind.Decimal));
        var rule = revision.AddRule(Guid.NewGuid(), "calculate", 1, 1, true, null, Ast(new { op = "constant", kind = RuleValueKind.Boolean, value = true }));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Expression,
            null, null, Ast(new { op = "add", args = new object[]
            {
                new { op = "variable", stableKey = input.StableKey },
                new { op = "variable", stableKey = middle.StableKey }
            } }), 1);
        revision.AddBinding(Guid.NewGuid(), contract, 1, [("productKind", RuleBindingOperator.Equals, "\"A\"")]);
        var context = new Dictionary<string, string> { ["productKind"] = "\"A\"" };
        var supplied = new Dictionary<string, string> { ["productKind"] = "\"A\"" };
        Assert.Equal("MissingInput", RuleSimulator.Simulate(revision, contract, context, supplied).ErrorCode);

        revision.SetInputDefault(input.StableKey, "120");
        Assert.Equal("AssignmentFailed", RuleSimulator.Simulate(revision, contract, context, supplied).ErrorCode);
        revision.UpdateIntermediateProperties(middle.StableKey, "5", "mm", 28);
        Assert.Equal("125", RuleSimulator.Simulate(revision, contract, context, supplied).Results.Single().Outputs["targetWidth"]);
        supplied["inputWidth"] = "null";
        Assert.Equal("InputConstraintViolated", RuleSimulator.Simulate(revision, contract, context, supplied).ErrorCode);
        Assert.Throws<ArgumentException>(() => revision.UpdateIntermediateProperties(input.StableKey, "1", null, 28));
        Assert.Throws<ArgumentException>(() => revision.UpdateIntermediateProperties(middle.StableKey, "5", null, 1));

        var copy = RuleModelRevision.CopyDraft(Guid.NewGuid(), Guid.NewGuid(), 1, revision);
        Assert.Equal("120", copy.Variables.Single(v => v.Code == "inputWidth").DefaultValueJson);
        Assert.Equal("mm", copy.Variables.Single(v => v.Code == "extra").Unit);
        Assert.Equal(middle.StableKey, copy.Variables.Single(v => v.Code == "extra").StableKey);
    }

    [Fact]
    public void VariableReferencesBlockItsRemovalWhileRuleRemovalRemainsAvailable()
    {
        // 先保留表达式引用验证变量删除被阻止，再检查删除规则的影响预览。
        var contract = SimulatedSizeExpansionScenario.Contract;
        var revision = new RuleModelRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        var middle = revision.AddIntermediate(Guid.NewGuid(), "extra", "Extra", new RuleTypeDescriptor(RuleValueKind.Decimal));
        var output = revision.Variables.Single(v => v.Code == "targetWidth");
        var rule = revision.AddRule(Guid.NewGuid(), "uses extra", 1, 1, true, null,
            Ast(new { op = "gt", args = new object[]
            {
                new { op = "variable", stableKey = middle.StableKey },
                new { op = "constant", kind = RuleValueKind.Decimal, value = 0 }
            } }));
        revision.AddAssignment(rule.Id, Guid.NewGuid(), output.StableKey, RuleValueSourceKind.Constant, "125", null, null, 1);
        revision.AddGateCase(Guid.NewGuid(), "check", "{}", "{}", "[{\"targetWidth\":125}]");

        var references = RuleRevisionImpactAnalyzer.GetVariableReferences(revision, middle.StableKey);
        Assert.Contains(references, x => x.Kind == "Condition" && x.BlocksVariableDeletion);
        Assert.Contains(references, x => x.Kind == "DependentVariable");
        Assert.Contains(references, x => x.Kind == "GateCaseOutput");
        var error = Assert.Throws<BusinessException>(() => revision.RemoveIntermediate(middle.StableKey));
        Assert.Equal(NRuleEngineErrorCodes.VariableReferenced, error.Code);
        Assert.NotEmpty(RuleRevisionImpactAnalyzer.GetRuleRemovalImpact(revision, rule.Id));
        revision.RemoveRule(rule.Id);
        revision.RemoveIntermediate(middle.StableKey);
        Assert.DoesNotContain(revision.Variables, v => v.StableKey == middle.StableKey);
        var recreated = revision.AddIntermediate(Guid.NewGuid(), "extra", "Extra", new RuleTypeDescriptor(RuleValueKind.Decimal));
        Assert.NotEqual(middle.StableKey, recreated.StableKey);
    }

    private static string Ast(object root)
    {
        return JsonSerializer.Serialize(new { version = 1, root });
    }
}
