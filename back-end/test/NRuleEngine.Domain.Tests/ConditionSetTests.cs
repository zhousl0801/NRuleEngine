using System.Text.Json;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class ConditionSetTests
{
    [Fact]
    public void RouteMachineCandidatesSupportAllFirstNoMatchAndAmbiguity()
    {
        // 合成重叠区间验证多候选语义。
        var contract = Contract("route-machine-param-eligibility", "THICKNESS");
        var narrow = Candidate(contract, "RM-01", 10, 0m, 5m);
        var wide = Candidate(contract, "RM-02", 5, 0m, 10m);
        var candidates = new[] { narrow, wide };

        var all = ConditionSetEvaluator.Evaluate(candidates, contract, Input(("THICKNESS", "4")), ConditionMatchMode.AllMatches);
        Assert.True(all.Succeeded);
        Assert.Equal(["RM-01", "RM-02"], all.Matches.Select(x => x.Code));
        var first = ConditionSetEvaluator.Evaluate(candidates, contract, Input(("THICKNESS", "4")), ConditionMatchMode.FirstMatch);
        Assert.Equal("RM-01", Assert.Single(first.Matches).Code);
        var none = ConditionSetEvaluator.Evaluate(candidates, contract, Input(("THICKNESS", "12")), ConditionMatchMode.AllMatches);
        Assert.True(none.Succeeded);
        Assert.False(none.Matched);
        Assert.Empty(none.Matches);
        var invalid = ConditionSetEvaluator.Evaluate(candidates, contract, Input(("THICKNESS", "\"bad\"")), ConditionMatchMode.AllMatches);
        Assert.False(invalid.Succeeded);
        Assert.Equal("InvalidInput", invalid.ErrorCode);

        var tie = Candidate(contract, "RM-03", 10, 0m, 5m);
        var ambiguous = ConditionSetEvaluator.Evaluate([narrow, tie], contract, Input(("THICKNESS", "4")), ConditionMatchMode.FirstMatch);
        Assert.Equal("AmbiguousMatch", ambiguous.ErrorCode);
    }

    [Fact]
    public void MicDesignConditionKeepsInputKeysAndPublishedRevisionImmutable()
    {
        // MIC FinishedProduct-ThicknessTol 的宽厚组合条件形状。
        var contract = Contract("mic-design", "THICKNESS", "WIDTH");
        var set = new ConditionSet(Guid.NewGuid(), contract.AppCode, contract.ScenarioCode,
            contract.ContractVersion, "MIC-ThicknessTol", "厚度公差", null);
        var revision = new ConditionSetRevision(Guid.NewGuid(), set.Id, set.AllocateRevisionNo(), contract);
        var thickness = revision.Inputs.Single(x => x.Code == "THICKNESS").StableKey;
        var width = revision.Inputs.Single(x => x.Code == "WIDTH").StableKey;
        var root = And(Between(thickness, 1.2m, 20m), Between(width, 400m, 1270m));
        revision.SetCondition("THICKNESS BETWEEN 1.2 AND 20 AND WIDTH BETWEEN 400 AND 1270",
            Ast(root), 30);
        revision.Publish(ConditionSetContentHasher.Compute(revision), "MIC sample");
        set.Activate(revision.Id);

        var matched = ConditionSetEvaluator.Evaluate([new(set, revision)], contract,
            Input(("THICKNESS", "5"), ("WIDTH", "1000")), ConditionMatchMode.AllMatches);
        Assert.Equal("MIC-ThicknessTol", Assert.Single(matched.Matches).Code);
        var unmatched = ConditionSetEvaluator.Evaluate([new(set, revision)], contract,
            Input(("THICKNESS", "5"), ("WIDTH", "1300")), ConditionMatchMode.AllMatches);
        Assert.True(unmatched.Succeeded);
        Assert.Empty(unmatched.Matches);
        Assert.Throws<InvalidOperationException>(() => revision.SetCondition(null, Ast(root), 1));

        var copy = ConditionSetRevision.CopyDraft(Guid.NewGuid(), set.Id, set.AllocateRevisionNo(), revision);
        Assert.Equal(revision.Inputs.Select(x => x.StableKey), copy.Inputs.Select(x => x.StableKey));
        Assert.Equal(revision.ConditionAstJson, copy.ConditionAstJson);
        Assert.NotEqual(revision.Inputs.Select(x => x.Id), copy.Inputs.Select(x => x.Id));
        Assert.Equal(RuleRevisionStatus.Draft, copy.Status);
    }

    [Fact]
    public void RejectsNonBooleanCondition()
    {
        var contract = Contract("route-machine-param-eligibility", "THICKNESS");
        var revision = new ConditionSetRevision(Guid.NewGuid(), Guid.NewGuid(), 1, contract);
        Assert.Throws<ArgumentException>(() => revision.SetCondition(null,
            Ast(new RuleExpressionNode { Op = "variable", StableKey = revision.Inputs.Single().StableKey }), 1));
    }

    [Fact]
    public void FirstMatchDoesNotEvaluateLowerPriorityAfterWinner()
    {
        var contract = Contract("route-machine-param-eligibility", "THICKNESS");
        var winner = Candidate(contract, "winner", 10, 0m, 5m);
        var set = new ConditionSet(Guid.NewGuid(), contract.AppCode, contract.ScenarioCode,
            contract.ContractVersion, "broken-lower", "broken-lower", null);
        var revision = new ConditionSetRevision(Guid.NewGuid(), set.Id, set.AllocateRevisionNo(), contract);
        revision.SetCondition(null, Ast(new RuleExpressionNode
        {
            Op = "gt",
            Args = [new RuleExpressionNode { Op = "div", Args = [Number(1m), Number(0m)] }, Number(0m)]
        }), 1);
        revision.Publish(ConditionSetContentHasher.Compute(revision), null);
        set.Activate(revision.Id);
        var candidates = new[] { winner, new ConditionCandidate(set, revision) };
        var first = ConditionSetEvaluator.Evaluate(candidates, contract, Input(("THICKNESS", "4")), ConditionMatchMode.FirstMatch);
        Assert.True(first.Succeeded);
        Assert.Equal("winner", Assert.Single(first.Matches).Code);
        var all = ConditionSetEvaluator.Evaluate(candidates, contract, Input(("THICKNESS", "4")), ConditionMatchMode.AllMatches);
        Assert.Equal("EvaluationError", all.ErrorCode);
    }

    private static ConditionCandidate Candidate(ConditionScenarioDescriptor contract, string code, int priority,
        decimal minimum, decimal maximum)
    {
        var set = new ConditionSet(Guid.NewGuid(), contract.AppCode, contract.ScenarioCode,
            contract.ContractVersion, code, code, null);
        var revision = new ConditionSetRevision(Guid.NewGuid(), set.Id, set.AllocateRevisionNo(), contract);
        revision.SetCondition(null, Ast(Between(revision.Inputs.Single().StableKey, minimum, maximum)), priority);
        revision.Publish(ConditionSetContentHasher.Compute(revision), null);
        set.Activate(revision.Id);
        return new ConditionCandidate(set, revision);
    }

    private static ConditionScenarioDescriptor Contract(string scenario, params string[] codes)
    {
        return new ConditionScenarioDescriptor("ZT-ODS", scenario, "1.0",
            codes.Select(code => new VariableContract(code, code, RuleVariableDirection.Input,
                new RuleTypeDescriptor(RuleValueKind.Decimal))).ToArray());
    }

    private static Dictionary<string, string> Input(params (string Code, string Json)[] values)
    {
        return values.ToDictionary(x => x.Code, x => x.Json);
    }

    private static RuleExpressionNode Between(Guid key, decimal minimum, decimal maximum)
    {
        return And(new RuleExpressionNode { Op = "gte", Args = [Variable(key), Number(minimum)] },
            new RuleExpressionNode { Op = "lte", Args = [Variable(key), Number(maximum)] });
    }

    private static RuleExpressionNode And(RuleExpressionNode a, RuleExpressionNode b)
    {
        return new RuleExpressionNode { Op = "and", Args = [a, b] };
    }

    private static RuleExpressionNode Variable(Guid key)
    {
        return new RuleExpressionNode { Op = "variable", StableKey = key };
    }

    private static RuleExpressionNode Number(decimal value)
    {
        return new RuleExpressionNode
        {
            Op = "constant",
            Kind = RuleValueKind.Decimal,
            Value = JsonSerializer.SerializeToElement(value)
        };
    }

    private static string Ast(RuleExpressionNode root)
    {
        return JsonSerializer.Serialize(new RuleExpression { Version = 1, Root = root });
    }
}
