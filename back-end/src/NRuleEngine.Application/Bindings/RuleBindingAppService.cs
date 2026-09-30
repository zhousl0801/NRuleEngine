using NRuleEngine.Application.Contracts.Bindings;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.RuleModels;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Bindings;

/// <summary>编排草稿场景绑定的维护操作。</summary>
public class RuleBindingAppService : ApplicationService, IRuleBindingAppService
{
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;
    private readonly IRuleCatalog _catalog;

    public RuleBindingAppService(IRuleModelRepository models, IRuleModelRevisionRepository revisions,
        IRuleCatalog catalog)
    {
        _models = models;
        _revisions = revisions;
        _catalog = catalog;
    }

    /// <summary>按锁定场景契约检查维度和值后新增绑定。</summary>
    public virtual async Task<RuleRevisionDto> AddBindingAsync(Guid revisionId, AddBindingDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        var model = await _models.GetAsync(revision.RuleModelId);
        var contract = _catalog.GetScenario(model.AppCode, revision.ScenarioCode, revision.ContractVersion);
        revision.AddBinding(GuidGenerator.Create(), contract, input.Priority,
            input.Criteria.Select(c => (c.DimensionCode, c.Operator, c.TypedValueJson)).ToArray());
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>校验完整的新条件集合，再替换指定绑定。</summary>
    public virtual async Task<RuleRevisionDto> UpdateBindingAsync(Guid revisionId, Guid bindingId, AddBindingDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        var model = await _models.GetAsync(revision.RuleModelId);
        var contract = _catalog.GetScenario(model.AppCode, revision.ScenarioCode, revision.ContractVersion);
        revision.UpdateBinding(bindingId, contract, input.Priority,
            input.Criteria.Select(c => (c.DimensionCode, c.Operator, c.TypedValueJson)).ToArray());
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>在并发检查后移除草稿绑定。</summary>
    public virtual async Task<RuleRevisionDto> RemoveBindingAsync(Guid revisionId, Guid bindingId,
        DeleteDraftItemDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.RemoveBinding(bindingId);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>加载修订及全部编制内容；未找到时使用稳定业务错误码。</summary>
    protected virtual async Task<RuleModelRevision> GetRevisionEntityAsync(Guid revisionId)
    {
        return await _revisions.FindWithVariablesAsync(revisionId)
            ?? throw new BusinessException(NRuleEngineErrorCodes.RevisionNotFound);
    }

    /// <summary>阻止持旧并发戳的编辑覆盖其他维护者的改动。</summary>
    protected virtual void CheckStamp(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || actual != expected)
        {
            throw new BusinessException(NRuleEngineErrorCodes.ConcurrencyConflict);
        }
    }
}
