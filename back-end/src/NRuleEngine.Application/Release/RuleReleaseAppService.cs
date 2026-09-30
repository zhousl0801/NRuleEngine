using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Application.RuleModels;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Release;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Validation;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;

namespace NRuleEngine.Application.Release;

/// <summary>编排发布门禁、跨模型绑定检查和原子活动修订切换。</summary>
public class RuleReleaseAppService : ApplicationService, IRuleReleaseAppService
{
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;
    private readonly IReleaseRecordRepository _records;
    private readonly IReleaseScopeRepository _scopes;
    private readonly IRuleCatalog _catalog;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public RuleReleaseAppService(IRuleModelRepository models, IRuleModelRevisionRepository revisions,
        IReleaseRecordRepository records, IReleaseScopeRepository scopes, IRuleCatalog catalog,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _models = models;
        _revisions = revisions;
        _records = records;
        _scopes = scopes;
        _catalog = catalog;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>核对草稿并发戳后新增发布回归用例。</summary>
    public virtual async Task<RuleRevisionDto> AddGateCaseAsync(Guid revisionId, AddReleaseGateCaseDto input)
    {
        var revision = await GetRevisionAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.AddGateCase(GuidGenerator.Create(), input.Name, input.ContextJson, input.InputsJson,
            input.ExpectedOutputsJson);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>核对草稿并发戳后移除发布回归用例。</summary>
    public virtual async Task<RuleRevisionDto> RemoveGateCaseAsync(Guid revisionId, Guid gateCaseId,
        DeleteDraftItemDto input)
    {
        var revision = await GetRevisionAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.RemoveGateCase(gateCaseId);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>在事务内执行完整校验、门禁和跨模型歧义检查，再切换活动修订。</summary>
    public virtual async Task<ReleaseDto> PublishAsync(Guid revisionId, PublishRuleDto input)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var revision = await GetRevisionAsync(revisionId);
        var model = await _models.GetAsync(revision.RuleModelId);
        CheckStamp(model.ConcurrencyStamp, input.ExpectedModelConcurrencyStamp);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        if (model.Status != RuleModelStatus.Active || revision.Status != RuleRevisionStatus.Draft)
        {
            throw new BusinessException(NRuleEngineErrorCodes.InvalidReleaseTarget);
        }

        // 数据库协调行先取得写锁；后续跨模型查询在同一事务中看到前一个发布的结果。
        await _scopes.AcquireAsync(model.AppCode, model.ScenarioCode);
        var contract = _catalog.GetScenario(model.AppCode, revision.ScenarioCode, revision.ContractVersion);
        if (RuleRevisionValidator.Validate(revision, contract).Count > 0)
        {
            throw new BusinessException(NRuleEngineErrorCodes.PublishValidationFailed);
        }
        await CheckActiveBindingsAsync(model, revision, contract);
        var gateFailures = ReleaseGateEvaluator.Evaluate(revision, contract);
        if (gateFailures.Count > 0)
        {
            throw new BusinessException(NRuleEngineErrorCodes.ReleaseGateFailed)
                .WithData("GateFailures", gateFailures);
        }

        var hash = RuleRevisionContentHasher.Compute(model.AppCode, revision);
        var previous = model.ActiveRevisionId;
        revision.Publish(hash, input.ChangeSummary);
        var releaseNo = model.Activate(revision.Id);
        var record = new ReleaseRecord(GuidGenerator.Create(), model.Id, revision.Id, previous,
            releaseNo, ReleaseKind.Publish, hash, $"Validation passed; {revision.GateCases.Count} gate cases passed.",
            revision.ChangeSummary);
        await _revisions.UpdateAsync(revision, autoSave: true);
        await _models.UpdateAsync(model, autoSave: true);
        await _records.InsertAsync(record, autoSave: true);
        await uow.CompleteAsync();
        return ToDto(record, model.ConcurrencyStamp);
    }

    /// <summary>在事务内校验历史修订、当前指针和跨模型绑定，再写一条回滚记录。</summary>
    public virtual async Task<ReleaseDto> RollbackAsync(Guid modelId, RollbackRuleDto input)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var model = await _models.GetAsync(modelId);
        CheckStamp(model.ConcurrencyStamp, input.ExpectedModelConcurrencyStamp);
        if (model.Status != RuleModelStatus.Active || model.ActiveRevisionId != input.ExpectedActiveRevisionId ||
            model.ActiveRevisionId == input.TargetRevisionId)
        {
            throw new BusinessException(NRuleEngineErrorCodes.InvalidReleaseTarget);
        }
        await _scopes.AcquireAsync(model.AppCode, model.ScenarioCode);
        var target = await GetRevisionAsync(input.TargetRevisionId);
        CheckStamp(target.ConcurrencyStamp, input.ExpectedTargetRevisionConcurrencyStamp);
        if (target.RuleModelId != modelId || target.Status != RuleRevisionStatus.Published || target.ContentHash is null)
        {
            throw new BusinessException(NRuleEngineErrorCodes.InvalidReleaseTarget);
        }
        var contract = _catalog.GetScenario(model.AppCode, target.ScenarioCode, target.ContractVersion);
        await CheckActiveBindingsAsync(model, target, contract);
        if (RuleRevisionContentHasher.Compute(model.AppCode, target) != target.ContentHash)
        {
            throw new BusinessException(NRuleEngineErrorCodes.InvalidReleaseTarget);
        }

        var previous = model.ActiveRevisionId;
        var releaseNo = model.Activate(target.Id);
        var record = new ReleaseRecord(GuidGenerator.Create(), model.Id, target.Id, previous,
            releaseNo, ReleaseKind.Rollback, target.ContentHash, "Historical published revision reactivated.",
            target.ChangeSummary);
        await _models.UpdateAsync(model, autoSave: true);
        await _records.InsertAsync(record, autoSave: true);
        await uow.CompleteAsync();
        return ToDto(record, model.ConcurrencyStamp);
    }

    /// <summary>读取模型的发布与回滚审计历史。</summary>
    public virtual async Task<IReadOnlyList<ReleaseDto>> GetHistoryAsync(Guid modelId)
    {
        var model = await _models.GetAsync(modelId);
        var records = await _records.GetListByModelAsync(modelId);
        return records.Select(x => ToDto(x, model.ConcurrencyStamp)).ToArray();
    }

    /// <summary>比较目标修订和其他活动模型的绑定；不同契约版本保守视为可能重叠。</summary>
    protected virtual async Task CheckActiveBindingsAsync(RuleModel model, RuleModelRevision target,
        RuleScenarioDescriptor contract)
    {
        var others = await _models.GetListAsync(model.AppCode, model.ScenarioCode);
        foreach (var other in others.Where(x => x.Id != model.Id && x.Status == RuleModelStatus.Active &&
                     x.ActiveRevisionId.HasValue))
        {
            var active = await GetRevisionAsync(other.ActiveRevisionId!.Value);
            if (active.Status != RuleRevisionStatus.Published)
            {
                throw new BusinessException(NRuleEngineErrorCodes.InvalidReleaseTarget);
            }
            foreach (var candidate in target.Bindings)
            {
                foreach (var existing in active.Bindings.Where(x => x.Priority == candidate.Priority))
                {
                    if (active.ContractVersion != target.ContractVersion ||
                        RuleRevisionValidator.BindingsMayOverlap(candidate, existing, contract))
                    {
                        throw new BusinessException(NRuleEngineErrorCodes.AmbiguousActiveBinding);
                    }
                }
            }
        }
    }

    /// <summary>读取完整修订聚合；未找到时返回稳定错误码。</summary>
    protected virtual async Task<RuleModelRevision> GetRevisionAsync(Guid revisionId)
    {
        return await _revisions.FindWithVariablesAsync(revisionId)
            ?? throw new BusinessException(NRuleEngineErrorCodes.RevisionNotFound);
    }

    /// <summary>确保请求仍基于当前模型或修订并发戳。</summary>
    protected virtual void CheckStamp(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || actual != expected)
        {
            throw new BusinessException(NRuleEngineErrorCodes.ConcurrencyConflict);
        }
    }

    private static ReleaseDto ToDto(ReleaseRecord record, string modelStamp)
    {
        return new ReleaseDto
        {
            Id = record.Id, RuleModelId = record.RuleModelId, ActivatedRevisionId = record.ActivatedRevisionId,
            PreviousRevisionId = record.PreviousRevisionId, ReleaseNo = record.ReleaseNo,
            Kind = record.Kind, ContentHash = record.ContentHash,
            ValidationSummary = record.ValidationSummary, ChangeSummary = record.ChangeSummary,
            CreatorId = record.CreatorId,
            CreationTime = record.CreationTime, ModelConcurrencyStamp = modelStamp
        };
    }
}
