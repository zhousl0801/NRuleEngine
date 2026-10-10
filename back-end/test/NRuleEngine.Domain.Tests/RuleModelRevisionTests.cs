using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class RuleModelRevisionTests
{
    [Fact]
    public void DraftCopiesContractVariablesAndStableKeys()
    {
        // 比对契约变量与草稿复制后的稳定键，防止修订间引用失效。
        var model = new RuleModel(Guid.NewGuid(), "TEST-ODS", "size-expansion", "1.0", "width", "Width", null);
        var first = new RuleModelRevision(Guid.NewGuid(), model.Id, model.AllocateRevisionNo(), SimulatedSizeExpansionScenario.Contract);
        var middle = first.AddIntermediate(Guid.NewGuid(), "delta", "Adjustment", new RuleTypeDescriptor(RuleValueKind.Decimal));
        var copied = RuleModelRevision.CopyDraft(Guid.NewGuid(), model.Id, model.AllocateRevisionNo(), first);

        Assert.Equal(4, copied.Variables.Count);
        Assert.Equal(first.Variables.Select(v => v.StableKey), copied.Variables.Select(v => v.StableKey));
        Assert.All(copied.Variables, variable => Assert.Equal(copied.Id, variable.RevisionId));
        copied.RenameIntermediate(middle.StableKey, "widthDelta", "Width adjustment");
        Assert.Equal("delta", middle.Code);
        Assert.Equal("widthDelta", copied.Variables.Single(v => v.StableKey == middle.StableKey).Code);
        Assert.Throws<InvalidOperationException>(() => copied.AddIntermediate(Guid.NewGuid(),
            " widthDelta ", "Duplicate", new RuleTypeDescriptor(RuleValueKind.Decimal)));
    }

    [Fact]
    public void ContractVariablesCannotBeRenamedAndArchivedModelCannotCreateDraft()
    {
        // 分别验证契约变量身份和模型归档状态两个领域边界。
        var model = new RuleModel(Guid.NewGuid(), "TEST-ODS", "size-expansion", "1.0", "width", "Width", null);
        var draft = new RuleModelRevision(Guid.NewGuid(), model.Id, model.AllocateRevisionNo(), SimulatedSizeExpansionScenario.Contract);
        var input = draft.Variables.First(v => v.Direction == RuleVariableDirection.Input);

        Assert.Throws<InvalidOperationException>(() => draft.RenameIntermediate(input.StableKey, "changed", "Changed"));
        model.Archive();
        Assert.Throws<InvalidOperationException>(() => model.AllocateRevisionNo());
    }
}
