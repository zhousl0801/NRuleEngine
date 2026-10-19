using NRuleEngine.Application.Contracts.RuleModels;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Release;

/// <summary>发布门禁维护、发布、回滚及发布历史的应用契约。</summary>
public interface IRuleReleaseAppService : IApplicationService
{
    /// <summary>向草稿追加发布回归用例。</summary>
    Task<RuleRevisionDto> AddGateCaseAsync(Guid revisionId, AddReleaseGateCaseDto input);
    /// <summary>从草稿移除发布回归用例。</summary>
    Task<RuleRevisionDto> RemoveGateCaseAsync(Guid revisionId, Guid gateCaseId, DeleteDraftItemDto input);
    /// <summary>校验并原子激活草稿修订。</summary>
    Task<ReleaseDto> PublishAsync(Guid revisionId, PublishRuleDto input);
    /// <summary>重新激活历史发布修订并留下新记录。</summary>
    Task<ReleaseDto> RollbackAsync(Guid modelId, RollbackRuleDto input);
    /// <summary>按最新发布在前列出模型的发布记录。</summary>
    Task<IReadOnlyList<ReleaseDto>> GetHistoryAsync(Guid modelId);
}
