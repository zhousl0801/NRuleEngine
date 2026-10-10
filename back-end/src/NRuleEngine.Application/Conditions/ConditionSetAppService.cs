using System.Text.Json;
using NRuleEngine.Application.Contracts.Conditions;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Release;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NRuleEngine.Application.Conditions;

/// <summary>编排条件集草稿、带样本的发布、回滚和指定候选集判定。</summary>
public class ConditionSetAppService : ApplicationService, IConditionSetAppService
{
    private readonly IRuleCatalog _catalog;
    private readonly IConditionSetRepository _sets;
    private readonly IConditionSetRevisionRepository _revisions;
    private readonly IRepository<ConditionSetReleaseRecord, Guid> _records;
    private readonly IUnitOfWorkManager _uowManager;

    /// <summary>注入场景目录、条件集仓储及事务协调器。</summary>
    public ConditionSetAppService(IRuleCatalog catalog, IConditionSetRepository sets,
        IConditionSetRevisionRepository revisions, IRepository<ConditionSetReleaseRecord, Guid> records,
        IUnitOfWorkManager uowManager)
    {
        _catalog = catalog;
        _sets = sets;
        _revisions = revisions;
        _records = records;
        _uowManager = uowManager;
    }

    /// <summary>创建条件集，并按固定契约建立首个草稿。</summary>
    /// <remarks>核心逻辑：检查业务编码唯一性并按场景契约建立首个修订；调用时机：维护者新建条件集时。</remarks>
    public virtual async Task<ConditionSetDto> CreateAsync(CreateConditionSetDto input)
    {
        using var uow = _uowManager.Begin(requiresNew: true, isTransactional: true);
        var contract = _catalog.GetConditionScenario(input.AppCode, input.ScenarioCode, input.ContractVersion);
        var code = (input.Code ?? string.Empty).Trim();
        if (await _sets.ExistsAsync(input.AppCode, code))
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.DuplicateConditionSetCode);
        }

        var set = new ConditionSet(GuidGenerator.Create(), input.AppCode, input.ScenarioCode,
            input.ContractVersion, code, input.Name, input.Description);
        await _sets.InsertAsync(set, autoSave: true);
        var revision = new ConditionSetRevision(GuidGenerator.Create(), set.Id, set.AllocateRevisionNo(), contract);
        await _revisions.InsertAsync(revision, autoSave: true);
        await _sets.UpdateAsync(set, autoSave: true);
        await uow.CompleteAsync();
        return ToDto(set);
    }

    /// <summary>读取条件集身份与活动修订。</summary>
    /// <remarks>核心逻辑：读取聚合并映射身份与活动状态；调用时机：维护者查看条件集时。</remarks>
    public virtual async Task<ConditionSetDto> GetAsync(Guid setId)
    {
        return ToDto(await _sets.GetAsync(setId));
    }

    /// <summary>按修订号倒序列出条件集的历史修订。</summary>
    /// <remarks>核心逻辑：按修订号排序并映射摘要；调用时机：维护者查看修订历史时。</remarks>
    public virtual async Task<IReadOnlyList<ConditionRevisionSummaryDto>> GetRevisionsAsync(Guid setId)
    {
        await _sets.GetAsync(setId);
        var revisions = await _revisions.GetListBySetAsync(setId);
        return revisions.Select(x => new ConditionRevisionSummaryDto(x.Id, x.RevisionNo,
            x.BasedOnRevisionId, x.Status, x.ContractVersion, x.ConcurrencyStamp)).ToArray();
    }

    /// <summary>按发布序号倒序读取发布与回滚记录。</summary>
    /// <remarks>核心逻辑：按发布序号读取审计记录；调用时机：维护者查看发布历史时。</remarks>
    public virtual async Task<IReadOnlyList<ConditionReleaseDto>> GetHistoryAsync(Guid setId)
    {
        await _sets.GetAsync(setId);
        var records = await _records.GetListAsync(x => x.ConditionSetId == setId);
        return records.OrderByDescending(x => x.ReleaseNo).Select(x => new ConditionReleaseDto(x.Id,
            x.ActivatedRevisionId, x.PreviousRevisionId, x.ReleaseNo, x.Kind, x.ContentHash,
            x.ChangeSummary)).ToArray();
    }

    /// <summary>核对并发戳后归档条件集。</summary>
    /// <remarks>核心逻辑：核对并发戳后改变聚合状态并持久化；调用时机：维护者归档条件集时。</remarks>
    public virtual async Task<ConditionSetDto> ArchiveAsync(Guid setId, ArchiveConditionSetDto input)
    {
        var set = await _sets.GetAsync(setId);
        CheckStamp(set.ConcurrencyStamp, input.ExpectedSetConcurrencyStamp);
        set.Archive();
        await _sets.UpdateAsync(set, autoSave: true);
        return ToDto(set);
    }

    /// <summary>读取修订及其输入稳定键和条件内容。</summary>
    /// <remarks>核心逻辑：载入带输入快照的完整修订；调用时机：维护者打开修订详情时。</remarks>
    public virtual async Task<ConditionSetRevisionDto> GetRevisionAsync(Guid revisionId)
    {
        return ToDto(await GetRevisionEntityAsync(revisionId));
    }

    /// <summary>从指定或活动修订复制草稿；首次发布前按初始契约创建。</summary>
    /// <remarks>核心逻辑：选定源修订或初始契约并分配新修订号；调用时机：维护者创建下一版草稿时。</remarks>
    public virtual async Task<ConditionSetRevisionDto> CreateDraftAsync(Guid setId, CreateConditionDraftDto input)
    {
        using var uow = _uowManager.Begin(requiresNew: true, isTransactional: true);
        var set = await _sets.GetAsync(setId);
        CheckStamp(set.ConcurrencyStamp, input.ExpectedSetConcurrencyStamp);
        var sourceId = input.BasedOnRevisionId ?? set.ActiveRevisionId;
        ConditionSetRevision draft;
        var id = GuidGenerator.Create();
        var revisionNo = set.AllocateRevisionNo();
        if (sourceId is Guid selected)
        {
            var source = await GetRevisionEntityAsync(selected);
            if (source.ConditionSetId != setId || source.Status == RuleRevisionStatus.Abandoned)
            {
                throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.InvalidRevisionSource);
            }

            _catalog.GetConditionScenario(set.AppCode, set.ScenarioCode, source.ContractVersion);
            draft = ConditionSetRevision.CopyDraft(id, setId, revisionNo, source);
        }
        else
        {
            var contract = _catalog.GetConditionScenario(set.AppCode, set.ScenarioCode, set.InitialContractVersion);
            draft = new ConditionSetRevision(id, setId, revisionNo, contract);
        }
        await _revisions.InsertAsync(draft, autoSave: true);
        await _sets.UpdateAsync(set, autoSave: true);
        await uow.CompleteAsync();
        return ToDto(draft);
    }

    /// <summary>核对并发戳后修改草稿布尔条件。</summary>
    /// <remarks>核心逻辑：检查并发戳后由领域层校验布尔 AST 与优先级；调用时机：维护者保存条件草稿时。</remarks>
    public virtual async Task<ConditionSetRevisionDto> UpdateConditionAsync(Guid revisionId, UpdateConditionDto input)
    {
        var revision = await GetRevisionEntityAsync(revisionId);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        revision.SetCondition(input.ConditionSource, input.ConditionAstJson, input.Priority);
        await _revisions.UpdateAsync(revision, autoSave: true);
        return ToDto(revision);
    }

    /// <summary>在事务内验证正反样本、冻结修订并切换活动指针。</summary>
    /// <remarks>核心逻辑：在事务内核对样本、内容哈希和活动指针；调用时机：维护者发布条件集时。</remarks>
    public virtual async Task<ConditionSetDto> PublishAsync(Guid revisionId, PublishConditionSetDto input)
    {
        using var uow = _uowManager.Begin(requiresNew: true, isTransactional: true);
        var revision = await GetRevisionEntityAsync(revisionId);
        var set = await _sets.GetAsync(revision.ConditionSetId);
        CheckStamp(set.ConcurrencyStamp, input.ExpectedSetConcurrencyStamp);
        CheckStamp(revision.ConcurrencyStamp, input.ExpectedRevisionConcurrencyStamp);
        if (set.Status != RuleModelStatus.Active || revision.Status != RuleRevisionStatus.Draft ||
            revision.ConditionAstJson is null)
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.InvalidReleaseTarget);
        }

        var contract = _catalog.GetConditionScenario(set.AppCode, set.ScenarioCode, revision.ContractVersion);
        if (!MatchesContract(revision, contract) || input.GateCases is null ||
            !GatePassed(revision, contract, input.GateCases))
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.ConditionGateFailed);
        }

        var gateCasesJson = JsonSerializer.Serialize(input.GateCases);
        if (gateCasesJson.Length > 262144)
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.ConditionGateFailed);
        }

        var hash = ConditionSetContentHasher.Compute(revision);
        var previous = set.ActiveRevisionId;
        revision.Publish(hash, input.ChangeSummary);
        var number = set.Activate(revision.Id);
        await _revisions.UpdateAsync(revision, autoSave: true);
        await _sets.UpdateAsync(set, autoSave: true);
        await _records.InsertAsync(new ConditionSetReleaseRecord(GuidGenerator.Create(), set.Id, revision.Id,
            previous, number, ReleaseKind.Publish, hash, input.ChangeSummary,
            gateCasesJson), autoSave: true);
        await uow.CompleteAsync();
        return ToDto(set);
    }

    /// <summary>在事务内重新激活一份同条件集的完整历史发布修订。</summary>
    /// <remarks>核心逻辑：验证历史发布内容后写审计记录并切换活动指针；调用时机：维护者回滚条件集时。</remarks>
    public virtual async Task<ConditionSetDto> RollbackAsync(Guid setId, RollbackConditionSetDto input)
    {
        using var uow = _uowManager.Begin(requiresNew: true, isTransactional: true);
        var set = await _sets.GetAsync(setId);
        CheckStamp(set.ConcurrencyStamp, input.ExpectedSetConcurrencyStamp);
        if (input.ChangeSummary?.Length > 2048)
        {
            throw new ArgumentException("变更说明不能超过 2048 个字符。", nameof(input));
        }

        var target = await GetRevisionEntityAsync(input.TargetRevisionId);
        if (set.Status != RuleModelStatus.Active || target.ConditionSetId != setId ||
            target.Status != RuleRevisionStatus.Published || target.ContentHash is null ||
            target.ContentHash != ConditionSetContentHasher.Compute(target))
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.InvalidReleaseTarget);
        }

        _catalog.GetConditionScenario(set.AppCode, set.ScenarioCode, target.ContractVersion);
        var previous = set.ActiveRevisionId;
        var number = set.Activate(target.Id);
        await _sets.UpdateAsync(set, autoSave: true);
        await _records.InsertAsync(new ConditionSetReleaseRecord(GuidGenerator.Create(), set.Id, target.Id,
            previous, number, ReleaseKind.Rollback, target.ContentHash, input.ChangeSummary), autoSave: true);
        await uow.CompleteAsync();
        return ToDto(set);
    }

    /// <summary>只载入明确候选并返回结构化命中或错误结果。</summary>
    /// <remarks>核心逻辑：只加载请求中的候选修订并交给领域判定器裁决；调用时机：业务方请求条件集判定时。</remarks>
    public virtual async Task<ConditionEvaluationDto> EvaluateAsync(EvaluateConditionSetsDto input)
    {
        var contract = _catalog.GetConditionScenario(input.AppCode, input.ScenarioCode, input.ContractVersion);
        if (input.CandidateCodes is null || input.CandidateCodes.Count is 0 or > 100 ||
            input.CandidateCodes.Any(string.IsNullOrWhiteSpace) ||
            input.CandidateCodes.Distinct(StringComparer.Ordinal).Count() != input.CandidateCodes.Count)
        {
            return Error("InvalidCandidates", "请提供 1 到 100 个不重复的候选编码。");
        }

        if (input.Inputs is null)
        {
            return Error("InvalidInput", "必须提供输入值。");
        }

        var sets = await _sets.GetByCodesAsync(input.AppCode, input.ScenarioCode, input.CandidateCodes);
        if (sets.Count != input.CandidateCodes.Count || sets.Any(x => x.ActiveRevisionId is null))
        {
            return Error("InvalidCandidate", "候选条件集不存在或尚未发布。");
        }

        var ids = sets.Select(x => x.ActiveRevisionId!.Value).ToArray();
        var revisions = await _revisions.GetByIdsWithInputsAsync(ids);
        if (revisions.Count != ids.Length)
        {
            return Error("InvalidCandidate", "活动修订不可用。");
        }

        var byId = revisions.ToDictionary(x => x.Id);
        var candidates = sets.Select(x => new ConditionCandidate(x, byId[x.ActiveRevisionId!.Value])).ToArray();
        Dictionary<string, string> json;
        try
        {
            json = input.Inputs.ToDictionary(x => x.Key, x => x.Value.GetRawText(), StringComparer.Ordinal);
        }
        catch (InvalidOperationException)
        {
            return Error("InvalidInput", "输入值不是有效的 JSON。");
        }
        var result = ConditionSetEvaluator.Evaluate(candidates, contract, json, input.Mode);
        return new ConditionEvaluationDto(result.Succeeded, result.Matched,
            result.Matches.Select(x => new ConditionMatchDto(x.Code, x.ConditionSetId, x.RevisionId, x.Priority)).ToArray(),
            result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>将可预期的判定失败映射为协议结果。</summary>
    /// <remarks>核心逻辑：保留稳定错误码并填充可读说明；调用时机：发生可预期判定失败时。</remarks>
    protected virtual ConditionEvaluationDto Error(string code, string message)
    {
        return new ConditionEvaluationDto(false, false, [], code, message);
    }

    /// <summary>要求正反样本均通过，并拒绝缺失或过大的输入。</summary>
    /// <remarks>核心逻辑：分别验证应命中和不应命中的样本；调用时机：发布条件集前执行门禁时。</remarks>
    protected virtual bool GatePassed(ConditionSetRevision revision, ConditionScenarioDescriptor contract,
        IReadOnlyList<ConditionGateCaseDto> cases)
    {
        if (cases.Count is < 2 or > 100 || cases.Any(x => x is null) ||
            !cases.Any(x => x.ExpectedMatch) || !cases.Any(x => !x.ExpectedMatch))
        {
            return false;
        }

        try
        {
            foreach (var gateCase in cases)
            {
                if (gateCase is null || gateCase.Inputs is null)
                {
                    return false;
                }

                if (gateCase.Inputs.Values.Any(x => x.GetRawText().Length > 4096))
                {
                    return false;
                }

                if (!gateCase.Inputs.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(contract.Inputs.Select(x => x.Code)))
                {
                    return false;
                }

                var values = new Dictionary<Guid, object?>();
                foreach (var variable in revision.Inputs)
                {
                    var value = RuleExpressionEngine.ReadValue(variable.ValueKind, gateCase.Inputs[variable.Code].GetRawText());
                    RuleExpressionEngine.CheckValue(variable, value);
                    values.Add(variable.StableKey, value);
                }
                if (RuleExpressionEngine.Evaluate(revision.ConditionAstJson!, values) is not bool matched ||
                    matched != gateCase.ExpectedMatch)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or JsonException)
        {
            return false;
        }
    }

    /// <summary>防止发布修订的输入快照与所锁定的场景契约漂移。</summary>
    /// <remarks>核心逻辑：比较修订快照与注册契约的输入定义；调用时机：判定或发布已保存修订时。</remarks>
    protected virtual bool MatchesContract(ConditionSetRevision revision, ConditionScenarioDescriptor contract)
    {
        return revision.Inputs.Count == contract.Inputs.Count && contract.Inputs.All(x => revision.Inputs.Any(v =>
            v.Code == x.Code && v.ValueKind == x.Type.Kind && v.IsNullable == x.Type.IsNullable &&
            v.Precision == x.Type.Precision && v.Scale == x.Type.Scale && v.EnumCode == x.Type.EnumCode));
    }

    /// <summary>读取完整修订，缺失时返回稳定业务错误码。</summary>
    /// <remarks>核心逻辑：按标识加载完整聚合并统一处理缺失情况；调用时机：编制操作读取修订时。</remarks>
    protected virtual async Task<ConditionSetRevision> GetRevisionEntityAsync(Guid id)
    {
        return await _revisions.FindWithInputsAsync(id)
            ?? throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.ConditionRevisionNotFound);
    }

    /// <summary>阻止持旧并发戳的写入覆盖其他维护者的修改。</summary>
    /// <remarks>核心逻辑：比较请求戳与当前实体戳并拒绝过期写入；调用时机：执行任何草稿或发布写操作前。</remarks>
    protected virtual void CheckStamp(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || actual != expected)
        {
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.ConcurrencyConflict);
        }
    }

    /// <summary>映射条件集的稳定身份和活动状态。</summary>
    /// <remarks>核心逻辑：将领域实体转换为对外传输对象；调用时机：应用服务返回读取或写入结果时。</remarks>
    protected virtual ConditionSetDto ToDto(ConditionSet set)
    {
        return new ConditionSetDto(set.Id, set.AppCode, set.ScenarioCode, set.Code,
            set.Name, set.Description, set.Status, set.ActiveRevisionId, set.ConcurrencyStamp);
    }

    /// <summary>映射条件修订、输入稳定键及编制状态。</summary>
    /// <remarks>核心逻辑：将领域实体转换为对外传输对象；调用时机：应用服务返回读取或写入结果时。</remarks>
    protected virtual ConditionSetRevisionDto ToDto(ConditionSetRevision revision)
    {
        return new ConditionSetRevisionDto(revision.Id, revision.ConditionSetId,
            revision.RevisionNo, revision.BasedOnRevisionId, revision.Status, revision.ScenarioCode,
            revision.ContractVersion, revision.Priority, revision.ConditionSource,
            revision.ConditionAstJson, revision.ContentHash,
            revision.Inputs.Select(input => new ConditionInputDto(input.StableKey, input.Code,
                input.Name, input.ValueKind, input.IsNullable, input.Precision, input.Scale,
                input.EnumCode)).ToArray(), revision.ConcurrencyStamp);
    }
}
