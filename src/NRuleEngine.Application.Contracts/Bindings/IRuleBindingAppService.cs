using NRuleEngine.Application.Contracts.RuleModels;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Contracts.Bindings;

/// <summary>草稿场景绑定的维护契约。</summary>
public interface IRuleBindingAppService : IApplicationService
{
    /// <summary>新增带类型校验的场景绑定。</summary>
    Task<RuleRevisionDto> AddBindingAsync(Guid revisionId, AddBindingDto input);

    /// <summary>修改绑定优先级及维度条件。</summary>
    Task<RuleRevisionDto> UpdateBindingAsync(Guid revisionId, Guid bindingId, AddBindingDto input);

    /// <summary>删除指定草稿绑定。</summary>
    Task<RuleRevisionDto> RemoveBindingAsync(Guid revisionId, Guid bindingId, DeleteDraftItemDto input);
}
