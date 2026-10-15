using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.RuleModels;

/// <summary>集中映射修订、规则、赋值和绑定的 API 投影。</summary>
internal static class RuleRevisionDtoMapper
{
    /// <summary>将修订快照映射为完整详情，规则按执行顺序展示。</summary>
    /// <remarks>核心逻辑：按顺序映射变量、规则、绑定和门禁内容；调用时机：应用服务返回完整修订详情时。</remarks>
    public static RuleRevisionDto ToDto(RuleModelRevision revision)
    {
        var dto = new RuleRevisionDto(revision.Id, revision.RuleModelId, revision.RevisionNo, revision.BasedOnRevisionId,
            revision.Status, revision.ScenarioCode, revision.ContractVersion, revision.ExecutionMode,
            revision.MultipleWritePolicy, revision.ConcurrencyStamp,
            revision.Variables.Select(v => new RuleVariableDto(v.Id, v.StableKey, v.Code, v.Name, v.Direction, v.Origin, new RuleTypeDescriptor(v.ValueKind, v.IsNullable, v.Precision, v.Scale, v.EnumCode, v.Shape))
            {
                DefaultValueJson = v.DefaultValueJson, Unit = v.Unit
            }).ToArray());
        dto.Rules = revision.Rules.OrderByDescending(r => r.Priority).ThenBy(r => r.SortOrder).Select(r => new RuleDefinitionDto
            {
                Id = r.Id, Name = r.Name, Priority = r.Priority, SortOrder = r.SortOrder,
                IsEnabled = r.IsEnabled, ConditionSource = r.ConditionSource,
                ConditionAstJson = r.ConditionAstJson,
                Assignments = r.Assignments.OrderBy(a => a.SortOrder).Select(a => new RuleAssignmentDto
                {
                    Id = a.Id, TargetVariableStableKey = a.TargetVariableStableKey,
                    ValueSourceKind = a.ValueSourceKind, ConstantValueJson = a.ConstantValueJson,
                    ExpressionSource = a.ExpressionSource, ExpressionAstJson = a.ExpressionAstJson,
                    SortOrder = a.SortOrder
                }).ToArray()
            }).ToArray();
        dto.Bindings = revision.Bindings.Select(b => new ScenarioBindingDto
        {
            Id = b.Id, Priority = b.Priority,
            Criteria = b.Criteria.Select(c => new BindingCriterionDto
            {
                DimensionCode = c.DimensionCode, Operator = c.Operator, TypedValueJson = c.TypedValueJson
            }).ToArray()
        }).ToArray();
        dto.GateCases = revision.GateCases.OrderBy(c => c.Name, StringComparer.Ordinal).Select(c => new ReleaseGateCaseDto
            {
                Id = c.Id, Name = c.Name, ContextJson = c.ContextJson,
                InputsJson = c.InputsJson, ExpectedOutputsJson = c.ExpectedOutputsJson
            }).ToArray();
        return dto;
    }
}
