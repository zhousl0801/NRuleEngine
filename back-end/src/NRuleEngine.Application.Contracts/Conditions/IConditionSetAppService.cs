using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>条件集编制、发布与候选集判定用例。</summary>
public interface IConditionSetAppService : IApplicationService
{
    /// <summary>创建条件集及首个草稿。</summary>
    Task<ConditionSetDto> CreateAsync(CreateConditionSetDto input);
    /// <summary>读取条件集身份和活动修订。</summary>
    Task<ConditionSetDto> GetAsync(Guid setId);
    /// <summary>读取条件集的修订摘要。</summary>
    Task<IReadOnlyList<ConditionRevisionSummaryDto>> GetRevisionsAsync(Guid setId);
    /// <summary>读取发布与回滚历史。</summary>
    Task<IReadOnlyList<ConditionReleaseDto>> GetHistoryAsync(Guid setId);
    /// <summary>归档条件集并保留历史。</summary>
    Task<ConditionSetDto> ArchiveAsync(Guid setId, ArchiveConditionSetDto input);
    /// <summary>读取完整修订及输入稳定键。</summary>
    Task<ConditionSetRevisionDto> GetRevisionAsync(Guid revisionId);
    /// <summary>按来源修订或活动修订创建草稿。</summary>
    Task<ConditionSetRevisionDto> CreateDraftAsync(Guid setId, CreateConditionDraftDto input);
    /// <summary>修改草稿的布尔条件。</summary>
    Task<ConditionSetRevisionDto> UpdateConditionAsync(Guid revisionId, UpdateConditionDto input);
    /// <summary>正反样本通过后发布修订。</summary>
    Task<ConditionSetDto> PublishAsync(Guid revisionId, PublishConditionSetDto input);
    /// <summary>激活历史已发布修订。</summary>
    Task<ConditionSetDto> RollbackAsync(Guid setId, RollbackConditionSetDto input);
    /// <summary>只对明确指定的候选集进行判定。</summary>
    Task<ConditionEvaluationDto> EvaluateAsync(EvaluateConditionSetsDto input);
}
