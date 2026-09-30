using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.RuleModels;

/// <summary>
/// 编排规则模型、草稿修订和中间变量的管理用例。
/// </summary>
public class RuleModelAppService : ApplicationService, IRuleModelAppService
{
    private readonly IRuleCatalog _catalog;
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;

    public RuleModelAppService(IRuleCatalog catalog, IRuleModelRepository models, IRuleModelRevisionRepository revisions)
    {
        _catalog = catalog;
        _models = models;
        _revisions = revisions;
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
}
