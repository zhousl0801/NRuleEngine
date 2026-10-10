using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.Contracts.Rules;
using NRuleEngine.Application.RuleModels;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Rules;

/// <summary>编排草稿规则与赋值的维护操作。</summary>
public class RuleAuthoringAppService : ApplicationService, IRuleAuthoringAppService
{
    private readonly IRuleModelRevisionRepository _revisions;

    public RuleAuthoringAppService(IRuleModelRevisionRepository revisions)
    {
        _revisions = revisions;
    }

    /// <summary>并发检查后新增规则，条件 AST 在领域层校验。</summary>
    /// <remarks>核心逻辑：核对草稿版本并由聚合检查条件 AST 与排序；调用时机：维护者新增规则时。</remarks>
    public virtual async Task<RuleRevisionDto> AddRuleAsync(Guid revisionId, AddRuleDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.AddRule(GuidGenerator.Create(), input.Name, input.Priority, input.SortOrder,
            input.IsEnabled, input.ConditionSource, input.ConditionAstJson);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>修改草稿规则条件与顺序，保持规则标识。</summary>
    /// <remarks>核心逻辑：保持规则标识并重新验证条件及执行顺序；调用时机：维护者修改规则时。</remarks>
    public virtual async Task<RuleRevisionDto> UpdateRuleAsync(Guid revisionId, Guid ruleId, AddRuleDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.UpdateRule(ruleId, input.Name, input.Priority, input.SortOrder, input.IsEnabled,
            input.ConditionSource, input.ConditionAstJson);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>向指定规则新增常量或表达式赋值。</summary>
    /// <remarks>核心逻辑：由聚合校验目标、来源和同规则内的顺序；调用时机：维护者新增规则赋值时。</remarks>
    public virtual async Task<RuleRevisionDto> AddAssignmentAsync(Guid revisionId, Guid ruleId, AddAssignmentDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.AddAssignment(ruleId, GuidGenerator.Create(), input.TargetVariableStableKey,
            input.ValueSourceKind, input.ConstantValueJson, input.ExpressionSource,
            input.ExpressionAstJson, input.SortOrder);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>修改赋值目标与来源，并拒绝重复目标和顺序冲突。</summary>
    /// <remarks>核心逻辑：重新校验赋值类型、目标和冲突；调用时机：维护者修改规则赋值时。</remarks>
    public virtual async Task<RuleRevisionDto> UpdateAssignmentAsync(Guid revisionId, Guid ruleId,
        Guid assignmentId, AddAssignmentDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.UpdateAssignment(ruleId, assignmentId, input.TargetVariableStableKey,
            input.ValueSourceKind, input.ConstantValueJson, input.ExpressionSource,
            input.ExpressionAstJson, input.SortOrder);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>删除草稿规则及随规则归属的赋值。</summary>
    /// <remarks>核心逻辑：核对版本后删除规则及其归属赋值；调用时机：维护者删除草稿规则时。</remarks>
    public virtual async Task<RuleRevisionDto> RemoveRuleAsync(Guid revisionId, Guid ruleId, DeleteDraftItemDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.RemoveRule(ruleId);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>删除指定规则下的一条赋值。</summary>
    /// <remarks>核心逻辑：核对版本后删除指定规则中的赋值；调用时机：维护者删除草稿赋值时。</remarks>
    public virtual async Task<RuleRevisionDto> RemoveAssignmentAsync(Guid revisionId, Guid ruleId, Guid assignmentId,
        DeleteDraftItemDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.RemoveAssignment(ruleId, assignmentId);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>加载修订及全部编制内容；未找到时使用稳定业务错误码。</summary>
    /// <remarks>核心逻辑：读取带规则和赋值的完整草稿聚合；调用时机：新增、更新或删除编制内容前。</remarks>
    protected virtual async Task<RuleModelRevision> GetRevisionEntityAsync(Guid revisionId)
    {
        return await _revisions.FindWithVariablesAsync(revisionId)
            ?? throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.RevisionNotFound);
    }

    /// <summary>阻止持旧并发戳的编辑覆盖其他维护者的改动。</summary>
    /// <remarks>核心逻辑：比较请求戳与当前实体戳并拒绝过期写入；调用时机：执行任何草稿或发布写操作前。</remarks>
    protected virtual void CheckStamp(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || actual != expected)
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.ConcurrencyConflict);
        }
    }
}
