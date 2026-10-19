using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 管理规则模型、草稿修订和中间变量的应用服务契约。
/// </summary>
public interface IRuleModelAppService : IApplicationService
{
    /// <summary>创建模型及首个草稿。</summary>
    Task<RuleModelDto> CreateAsync(CreateRuleModelDto input);

    /// <summary>从指定修订复制独立的新模型及首个草稿。</summary>
    Task<RuleModelDto> CopyAsync(Guid sourceModelId, CopyRuleModelDto input);

    /// <summary>获取模型详情。</summary>
    Task<RuleModelDto> GetAsync(Guid modelId);

    /// <summary>按应用和可选场景获取模型列表。</summary>
    Task<IReadOnlyList<RuleModelDto>> GetListAsync(string appCode, string? scenarioCode = null);

    /// <summary>校验并发戳后归档模型。</summary>
    Task<RuleModelDto> ArchiveAsync(Guid modelId, ArchiveRuleModelDto input);

    /// <summary>查看归档模型会影响的活动修订。</summary>
    Task<MaintenanceImpactDto> GetArchiveImpactAsync(Guid modelId);

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

    /// <summary>配置草稿契约入口变量的默认值。</summary>
    Task<RuleRevisionDto> SetInputDefaultAsync(Guid revisionId, Guid stableKey, SetInputDefaultDto input);

    /// <summary>更新草稿用户中间变量的默认值和单位。</summary>
    Task<RuleRevisionDto> UpdateIntermediatePropertiesAsync(Guid revisionId, Guid stableKey, UpdateIntermediatePropertiesDto input);

    /// <summary>单独修改草稿用户中间变量的 Decimal 精度。</summary>
    Task<RuleRevisionDto> SetIntermediatePrecisionAsync(Guid revisionId, Guid stableKey, SetIntermediatePrecisionDto input);

    /// <summary>查看变量的直接引用和下游影响。</summary>
    Task<MaintenanceImpactDto> GetVariableReferencesAsync(Guid revisionId, Guid stableKey);

    /// <summary>删除没有引用的草稿中间变量。</summary>
    Task<RuleRevisionDto> RemoveIntermediateVariableAsync(Guid revisionId, Guid stableKey, DeleteDraftItemDto input);

    /// <summary>查看删除草稿规则的影响，引用仅提示。</summary>
    Task<MaintenanceImpactDto> GetRuleRemovalImpactAsync(Guid revisionId, Guid ruleId);

    /// <summary>查看删除草稿赋值的影响，引用仅提示。</summary>
    Task<MaintenanceImpactDto> GetAssignmentRemovalImpactAsync(Guid revisionId, Guid ruleId, Guid assignmentId);

    /// <summary>设置草稿执行语义。</summary>
    Task<RuleRevisionDto> UpdateExecutionSemanticsAsync(Guid revisionId, UpdateExecutionSemanticsDto input);
}
