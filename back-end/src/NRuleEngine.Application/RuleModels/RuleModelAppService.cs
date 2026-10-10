using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;

namespace NRuleEngine.Application.RuleModels;

/// <summary>
/// 编排规则模型、草稿修订和中间变量的管理用例。
/// </summary>
public class RuleModelAppService : ApplicationService, IRuleModelAppService
{
    private readonly IRuleCatalog _catalog;
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public RuleModelAppService(IRuleCatalog catalog, IRuleModelRepository models,
        IRuleModelRevisionRepository revisions, IUnitOfWorkManager unitOfWorkManager)
    {
        _catalog = catalog;
        _models = models;
        _revisions = revisions;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 创建模型，并根据选定的场景契约建立首个草稿。
    /// </summary>
    public virtual async Task<RuleModelDto> CreateAsync(CreateRuleModelDto input)
    {
        var contract = _catalog.GetScenario(input.AppCode, input.ScenarioCode, input.ContractVersion);
        var code = (input.Code ?? string.Empty).Trim();
        if (await _models.ExistsAsync(input.AppCode, code))
        {
            throw new BusinessException(NRuleEngineErrorCodes.DuplicateModelCode);
        }

        var model = new RuleModel(GuidGenerator.Create(), input.AppCode, input.ScenarioCode,
            input.ContractVersion, code, input.Name, input.Description);
        await _models.InsertAsync(model, autoSave: true);
        var revision = new RuleModelRevision(GuidGenerator.Create(), model.Id, model.AllocateRevisionNo(), contract);
        await _revisions.InsertAsync(revision, autoSave: true);
        await _models.UpdateAsync(model, autoSave: true);
        return ToDto(model);
    }

    /// <summary>复制指定源修订为独立模型的首个草稿，保留变量稳定键与门禁内容。</summary>
    public virtual async Task<RuleModelDto> CopyAsync(Guid sourceModelId, CopyRuleModelDto input)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var sourceModel = await _models.GetAsync(sourceModelId);
        var source = await GetRevisionEntityAsync(input.SourceRevisionId);
        CheckStamp(source.ConcurrencyStamp, input.ExpectedSourceRevisionConcurrencyStamp);
        if (source.RuleModelId != sourceModelId || source.Status == RuleRevisionStatus.Abandoned)
        {
            throw new BusinessException(NRuleEngineErrorCodes.InvalidRevisionSource);
        }
        _catalog.GetScenario(sourceModel.AppCode, source.ScenarioCode, source.ContractVersion);
        var code = (input.Code ?? string.Empty).Trim();
        if (await _models.ExistsAsync(sourceModel.AppCode, code))
        {
            throw new BusinessException(NRuleEngineErrorCodes.DuplicateModelCode);
        }
        var model = new RuleModel(GuidGenerator.Create(), sourceModel.AppCode, source.ScenarioCode,
            source.ContractVersion, code, input.Name, input.Description);
        await _models.InsertAsync(model, autoSave: true);
        var copy = RuleModelRevision.CopyDraft(GuidGenerator.Create(), model.Id, model.AllocateRevisionNo(), source);
        await _revisions.InsertAsync(copy, autoSave: true);
        await _models.UpdateAsync(model, autoSave: true);
        await uow.CompleteAsync();
        return ToDto(model);
    }

    /// <summary>读取模型身份、状态和当前活动修订标识。</summary>
    public virtual async Task<RuleModelDto> GetAsync(Guid modelId)
    {
        return ToDto(await _models.GetAsync(modelId));
    }

    /// <summary>按应用及可选场景列出模型，不载入整个修订内容。</summary>
    public virtual async Task<IReadOnlyList<RuleModelDto>> GetListAsync(string appCode, string? scenarioCode = null)
    {
        var models = await _models.GetListAsync(appCode, scenarioCode);
        return models.Select(ToDto).ToArray();
    }

    /// <summary>核对模型并发戳后归档，保留历史修订。</summary>
    public virtual async Task<RuleModelDto> ArchiveAsync(Guid modelId, ArchiveRuleModelDto input)
    {
        var model = await _models.GetAsync(modelId);
        CheckStamp(model.ConcurrencyStamp, input.ExpectedModelConcurrencyStamp);
        model.Archive();
        await _models.UpdateAsync(model, autoSave: true);
        return ToDto(model);
    }

    /// <summary>归档只提示活动发布修订的运行影响，不因引用阻止操作。</summary>
    public virtual async Task<MaintenanceImpactDto> GetArchiveImpactAsync(Guid modelId)
    {
        var model = await _models.GetAsync(modelId);
        return new MaintenanceImpactDto
        {
            TargetKind = "Model", TargetId = modelId, ConcurrencyStamp = model.ConcurrencyStamp,
            References = model.Status == RuleModelStatus.Active && model.ActiveRevisionId is Guid revisionId
                ? [new ImpactReferenceDto { Kind = "ActiveRevision", Path = $"revisions/{revisionId}" },
                    new ImpactReferenceDto { Kind = "RuntimeSelection", Path = $"models/{modelId}/runtime-selection" }] : []
        };
    }

    /// <summary>
    /// 从指定修订或活动修订复制草稿；没有源修订时使用模型最初的场景契约。
    /// </summary>
    public virtual async Task<RuleRevisionDto> CreateDraftAsync(Guid modelId, CreateDraftDto input)
    {
        var model = await _models.GetAsync(modelId);
        CheckStamp(model.ConcurrencyStamp, input.ExpectedModelConcurrencyStamp);
        RuleModelRevision revision;
        var id = GuidGenerator.Create();
        var revisionNo = model.AllocateRevisionNo();
        var sourceId = input.BasedOnRevisionId ?? model.ActiveRevisionId;
        if (sourceId is Guid selectedSourceId)
        {
            var source = await GetRevisionEntityAsync(selectedSourceId);
            if (source.RuleModelId != modelId || source.Status == RuleRevisionStatus.Abandoned)
            {
                throw new BusinessException(NRuleEngineErrorCodes.InvalidRevisionSource);
            }

            _catalog.GetScenario(model.AppCode, model.ScenarioCode, source.ContractVersion);
            revision = RuleModelRevision.CopyDraft(id, modelId, revisionNo, source);
        }
        else
        {
            var contract = _catalog.GetScenario(model.AppCode, model.ScenarioCode, model.InitialContractVersion);
            revision = new RuleModelRevision(id, modelId, revisionNo, contract);
        }

        await _revisions.InsertAsync(revision, autoSave: true);
        await _models.UpdateAsync(model, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>读取修订及其变量、规则和绑定详情。</summary>
    public virtual async Task<RuleRevisionDto> GetRevisionAsync(Guid revisionId)
    {
        return ToDto(await GetRevisionEntityAsync(revisionId));
    }

    /// <summary>按修订号倒序列出模型的修订摘要。</summary>
    public virtual async Task<IReadOnlyList<RuleRevisionSummaryDto>> GetRevisionsAsync(Guid modelId)
    {
        await _models.GetAsync(modelId);
        var revisions = await _revisions.GetListByModelAsync(modelId);
        return revisions.Select(x => new RuleRevisionSummaryDto(x.Id, x.RevisionNo, x.BasedOnRevisionId,
            x.Status, x.ScenarioCode, x.ContractVersion, x.ConcurrencyStamp)).ToArray();
    }

    /// <summary>在并发检查后向草稿加入中间变量。</summary>
    public virtual async Task<RuleRevisionDto> AddIntermediateVariableAsync(Guid revisionId, AddIntermediateVariableDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.AddIntermediate(GuidGenerator.Create(), input.Code, input.Name, input.Type);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>按 StableKey 修改草稿中间变量的编码和名称。</summary>
    public virtual async Task<RuleRevisionDto> RenameIntermediateVariableAsync(Guid revisionId, Guid stableKey,
        RenameIntermediateVariableDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.RenameIntermediate(stableKey, input.Code, input.Name);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>按修订并发戳配置契约入口默认值。</summary>
    public virtual async Task<RuleRevisionDto> SetInputDefaultAsync(Guid revisionId, Guid stableKey,
        SetInputDefaultDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.SetInputDefault(stableKey, input.DefaultValueJson);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>按修订并发戳更新中间变量默认值与单位，保留当前精度。</summary>
    public virtual async Task<RuleRevisionDto> UpdateIntermediatePropertiesAsync(Guid revisionId, Guid stableKey,
        UpdateIntermediatePropertiesDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        var variable = revision.GetVariable(stableKey);
        revision.UpdateIntermediateProperties(stableKey, input.DefaultValueJson, input.Unit, variable.Precision);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>按修订并发戳单独更新中间变量 Decimal 精度。</summary>
    public virtual async Task<RuleRevisionDto> SetIntermediatePrecisionAsync(Guid revisionId, Guid stableKey,
        SetIntermediatePrecisionDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        var variable = revision.GetVariable(stableKey);
        revision.UpdateIntermediateProperties(stableKey, variable.DefaultValueJson, variable.Unit, input.Precision);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>返回变量引用与间接影响，供维护界面定位和删除前检查。</summary>
    public virtual async Task<MaintenanceImpactDto> GetVariableReferencesAsync(Guid revisionId, Guid stableKey)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        return ToImpact("Variable", stableKey, revision.ConcurrencyStamp,
            RuleRevisionImpactAnalyzer.GetVariableReferences(revision, stableKey), blockOnReferences: true);
    }

    /// <summary>按并发戳删除没有引用的草稿中间变量。</summary>
    public virtual async Task<RuleRevisionDto> RemoveIntermediateVariableAsync(Guid revisionId, Guid stableKey,
        DeleteDraftItemDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.RemoveIntermediate(stableKey);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>只读预览删除规则的下游影响。</summary>
    public virtual async Task<MaintenanceImpactDto> GetRuleRemovalImpactAsync(Guid revisionId, Guid ruleId)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        return ToImpact("Rule", ruleId, revision.ConcurrencyStamp,
            RuleRevisionImpactAnalyzer.GetRuleRemovalImpact(revision, ruleId), false);
    }

    /// <summary>只读预览删除赋值的下游影响。</summary>
    public virtual async Task<MaintenanceImpactDto> GetAssignmentRemovalImpactAsync(Guid revisionId, Guid ruleId,
        Guid assignmentId)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        return ToImpact("Assignment", assignmentId, revision.ConcurrencyStamp,
            RuleRevisionImpactAnalyzer.GetAssignmentRemovalImpact(revision, ruleId, assignmentId), false);
    }

    /// <summary>更新草稿命中模式与重复写入策略，并检查并发戳。</summary>
    public virtual async Task<RuleRevisionDto> UpdateExecutionSemanticsAsync(Guid revisionId,
        UpdateExecutionSemanticsDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.SetExecutionSemantics(input.ExecutionMode, input.MultipleWritePolicy);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>读取完整修订聚合，未找到时返回稳定业务错误码。</summary>
    protected virtual async Task<RuleModelRevision> GetRevisionEntityAsync(Guid revisionId)
    {
        return await _revisions.FindWithVariablesAsync(revisionId)
            ?? throw new BusinessException(NRuleEngineErrorCodes.RevisionNotFound);
    }

    /// <summary>阻止持旧并发戳的操作覆盖其他维护者的修改。</summary>
    protected virtual void CheckStamp(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || actual != expected)
        {
            throw new BusinessException(NRuleEngineErrorCodes.ConcurrencyConflict);
        }
    }

    /// <summary>把模型实体映射成不暴露聚合实现的传输对象。</summary>
    protected virtual RuleModelDto ToDto(RuleModel model)
    {
        return new RuleModelDto(model.Id, model.AppCode, model.ScenarioCode, model.Code, model.Name,
            model.Description, model.Status, model.ActiveRevisionId, model.ConcurrencyStamp);
    }

    /// <summary>将修订聚合映射为包含规则与绑定的完整传输对象。</summary>
    protected virtual RuleRevisionDto ToDto(RuleModelRevision revision)
    {
        return RuleRevisionDtoMapper.ToDto(revision);
    }

    /// <summary>统一把领域引用路径映射为只读维护影响响应。</summary>
    protected virtual MaintenanceImpactDto ToImpact(string kind, Guid targetId, string concurrencyStamp,
        IReadOnlyList<RuleImpactReference> references, bool blockOnReferences)
    {
        return new MaintenanceImpactDto
        {
            TargetKind = kind, TargetId = targetId, ConcurrencyStamp = concurrencyStamp,
            BlocksOperation = blockOnReferences && references.Any(x => x.BlocksVariableDeletion),
            References = references.Select(x => new ImpactReferenceDto
            {
                Kind = x.Kind, Path = x.Path, BlocksVariableDeletion = x.BlocksVariableDeletion
            }).ToArray()
        };
    }
}
