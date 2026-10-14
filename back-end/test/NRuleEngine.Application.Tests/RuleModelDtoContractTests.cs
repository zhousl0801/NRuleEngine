using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using Xunit;

namespace NRuleEngine.Application.Tests;

public sealed class RuleModelDtoContractTests
{
    [Fact]
    public void CreateInputRoundTripsThroughJsonAndValidatesLengths()
    {
        // 做 DTO 序列化往返并运行注解校验，确认传输契约一致。
        var input = new CreateRuleModelDto("TEST-ODS", "size-expansion", "1.0", "width", "Width", null);
        var json = JsonSerializer.Serialize(input);
        var restored = JsonSerializer.Deserialize<CreateRuleModelDto>(json);

        Assert.NotNull(restored);
        Assert.Equal(input.AppCode, restored.AppCode);
        Assert.Equal(input.ScenarioCode, restored.ScenarioCode);
        Assert.Equal(input.Code, restored.Code);
        Assert.True(Validator.TryValidateObject(restored, new ValidationContext(restored), [], true));

        restored.Code = new string('x', 129);
        Assert.False(Validator.TryValidateObject(restored, new ValidationContext(restored), [], true));
    }

    [Fact]
    public void RevisionOutputSerializesVariableDetails()
    {
        // 序列化修订详情并核对变量字段，防止输出契约遗漏。
        var revision = new RuleRevisionDto(Guid.NewGuid(), Guid.NewGuid(), 1, null, RuleRevisionStatus.Draft, "size-expansion", "1.0", RuleExecutionMode.FirstMatch, MultipleWritePolicy.ErrorOnMultipleWrite, "stamp", [new RuleVariableDto(Guid.NewGuid(), Guid.NewGuid(), "inputWidth", "Input width", RuleVariableDirection.Input, RuleVariableOrigin.Contract, new RuleTypeDescriptor(RuleValueKind.Decimal))]);

        var restored = JsonSerializer.Deserialize<RuleRevisionDto>(JsonSerializer.Serialize(revision));

        Assert.NotNull(restored);
        Assert.Equal(revision.Id, restored.Id);
        Assert.Equal("inputWidth", Assert.Single(restored.Variables).Code);
        Assert.Equal(RuleValueKind.Decimal, restored.Variables[0].Type.Kind);
    }
}
