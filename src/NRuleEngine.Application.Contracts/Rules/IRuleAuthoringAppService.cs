using NRuleEngine.Application.Contracts.RuleModels;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Rules;

/// <summary>草稿规则和赋值的维护契约。</summary>
public interface IRuleAuthoringAppService : IApplicationService
{
    /// <summary>新增草稿规则。</summary>
    Task<RuleRevisionDto> AddRuleAsync(Guid revisionId, AddRuleDto input);

    /// <summary>修改规则条件与执行顺序。</summary>
    Task<RuleRevisionDto> UpdateRuleAsync(Guid revisionId, Guid ruleId, AddRuleDto input);

    /// <summary>删除草稿规则及其赋值。</summary>
    Task<RuleRevisionDto> RemoveRuleAsync(Guid revisionId, Guid ruleId, DeleteDraftItemDto input);

    /// <summary>增加一条规则赋值。</summary>
    Task<RuleRevisionDto> AddAssignmentAsync(Guid revisionId, Guid ruleId, AddAssignmentDto input);

    /// <summary>修改指定赋值。</summary>
    Task<RuleRevisionDto> UpdateAssignmentAsync(Guid revisionId, Guid ruleId, Guid assignmentId, AddAssignmentDto input);

    /// <summary>删除指定赋值。</summary>
    Task<RuleRevisionDto> RemoveAssignmentAsync(Guid revisionId, Guid ruleId, Guid assignmentId, DeleteDraftItemDto input);
}
