using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 管理规则模型、草稿修订和中间变量的应用服务契约。
/// </summary>
public interface IRuleModelAppService : IApplicationService
{
    /// <summary>创建模型及首个草稿。</summary>
    Task<RuleModelDto> CreateAsync(CreateRuleModelDto input);

    /// <summary>获取模型详情。</summary>
    Task<RuleModelDto> GetAsync(Guid modelId);

    /// <summary>按应用和可选场景获取模型列表。</summary>
    Task<IReadOnlyList<RuleModelDto>> GetListAsync(string appCode, string? scenarioCode = null);

    /// <summary>校验并发戳后归档模型。</summary>
    Task<RuleModelDto> ArchiveAsync(Guid modelId, ArchiveRuleModelDto input);

    /// <summary>从指定或活动修订复制草稿。</summary>
    Task<RuleRevisionDto> CreateDraftAsync(Guid modelId, CreateDraftDto input);

    /// <summary>获取修订及其变量。</summary>
    Task<RuleRevisionDto> GetRevisionAsync(Guid revisionId);

    /// <summary>获取模型的修订摘要。</summary>
    Task<IReadOnlyList<RuleRevisionSummaryDto>> GetRevisionsAsync(Guid modelId);

    /// <summary>在草稿中新增中间变量。</summary>
    Task<RuleRevisionDto> AddIntermediateVariableAsync(Guid revisionId, AddIntermediateVariableDto input);

    /// <summary>重命名草稿中的中间变量。</summary>
    Task<RuleRevisionDto> RenameIntermediateVariableAsync(Guid revisionId, Guid stableKey, RenameIntermediateVariableDto input);

    /// <summary>设置草稿执行语义。</summary>
    Task<RuleRevisionDto> UpdateExecutionSemanticsAsync(Guid revisionId, UpdateExecutionSemanticsDto input);
}
