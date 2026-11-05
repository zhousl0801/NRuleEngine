using NRuleEngine.Application.Contracts.Release;
using NRuleEngine.Domain.Conditions;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Release;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NRuleEngine.Application.Release;

/// <summary>汇总规则模型和条件集的发布与回滚记录。</summary>
public class ReleaseHistoryAppService : ApplicationService, IReleaseHistoryAppService
{
    private readonly IRuleModelRepository _models;
    private readonly IConditionSetRepository _conditionSets;
    private readonly IReleaseRecordRepository _modelRecords;
    private readonly IRepository<ConditionSetReleaseRecord, Guid> _conditionRecords;
    private readonly IRuleModelRevisionRepository _modelRevisions;
    private readonly IConditionSetRevisionRepository _conditionRevisions;

    /// <summary>注入模型、条件集、修订及发布记录仓储。</summary>
    public ReleaseHistoryAppService(
        IRuleModelRepository models,
        IConditionSetRepository conditionSets,
        IReleaseRecordRepository modelRecords,
        IRepository<ConditionSetReleaseRecord, Guid> conditionRecords,
        IRuleModelRevisionRepository modelRevisions,
        IConditionSetRevisionRepository conditionRevisions)
    {
        _models = models;
        _conditionSets = conditionSets;
        _modelRecords = modelRecords;
        _conditionRecords = conditionRecords;
        _modelRevisions = modelRevisions;
        _conditionRevisions = conditionRevisions;
    }

    /// <summary>读取指定应用和场景下两类对象的最近发布记录。</summary>
    /// <remarks>核心逻辑：分别装载目标对象、发布记录和修订号后合并排序；调用时机：维护者查看发布记录时。</remarks>
    public virtual async Task<IReadOnlyList<ReleaseHistoryDto>> GetListAsync(string appCode, string? scenarioCode = null,
        ReleaseTargetType? targetType = null, int maxResultCount = 200)
    {
        var take = Math.Clamp(maxResultCount, 1, 500);
        var result = new List<ReleaseHistoryDto>();

        if (targetType is null or ReleaseTargetType.RuleModel)
        {
            result.AddRange(await GetModelReleasesAsync(appCode, scenarioCode));
        }

        if (targetType is null or ReleaseTargetType.ConditionSet)
        {
            result.AddRange(await GetConditionSetReleasesAsync(appCode, scenarioCode));
        }

        return result
            .OrderByDescending(x => x.CreationTime)
            .ThenByDescending(x => x.ReleaseNo)
            .Take(take)
            .ToArray();
    }

    /// <summary>读取规则模型的发布记录并补齐对象名称和修订号。</summary>
    protected virtual async Task<List<ReleaseHistoryDto>> GetModelReleasesAsync(string appCode, string? scenarioCode)
    {
        var models = await _models.GetListAsync(appCode, scenarioCode);
        if (models.Count == 0)
        {
            return [];
        }

        var modelIds = models.Select(x => x.Id).ToArray();
        var records = await _modelRecords.GetListAsync(x => modelIds.Contains(x.RuleModelId));
        var revisionIds = records.Select(x => x.ActivatedRevisionId).Distinct().ToArray();
        var revisionNumbers = revisionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await _modelRevisions.GetListAsync(x => revisionIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.RevisionNo);
        var modelById = models.ToDictionary(x => x.Id);

        return records
            .Where(record => modelById.ContainsKey(record.RuleModelId))
            .Select(record => ToDto(record, modelById[record.RuleModelId], revisionNumbers))
            .ToList();
    }

    /// <summary>读取条件集的发布记录并补齐对象名称和修订号。</summary>
    protected virtual async Task<List<ReleaseHistoryDto>> GetConditionSetReleasesAsync(string appCode, string? scenarioCode)
    {
        var sets = await _conditionSets.GetListAsync(appCode, scenarioCode);
        if (sets.Count == 0)
        {
            return [];
        }

        var setIds = sets.Select(x => x.Id).ToArray();
        var records = await _conditionRecords.GetListAsync(x => setIds.Contains(x.ConditionSetId));
        var revisionIds = records.Select(x => x.ActivatedRevisionId).Distinct().ToArray();
        var revisionNumbers = revisionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await _conditionRevisions.GetListAsync(x => revisionIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.RevisionNo);
        var setById = sets.ToDictionary(x => x.Id);

        return records
            .Where(record => setById.ContainsKey(record.ConditionSetId))
            .Select(record => ToDto(record, setById[record.ConditionSetId], revisionNumbers))
            .ToList();
    }

    /// <summary>把模型发布记录映射为统一历史项。</summary>
    protected virtual ReleaseHistoryDto ToDto(ReleaseRecord record, RuleModel model,
        IReadOnlyDictionary<Guid, int> revisionNumbers)
    {
        return new ReleaseHistoryDto
        {
            Id = record.Id,
            TargetType = ReleaseTargetType.RuleModel,
            TargetId = model.Id,
            TargetCode = model.Code,
            TargetName = model.Name,
            ReleaseNo = record.ReleaseNo,
            ActivatedRevisionId = record.ActivatedRevisionId,
            RevisionNo = revisionNumbers.GetValueOrDefault(record.ActivatedRevisionId),
            Kind = record.Kind,
            ContentHash = record.ContentHash,
            ChangeSummary = record.ChangeSummary,
            CreationTime = record.CreationTime
        };
    }

    /// <summary>把条件集发布记录映射为统一历史项。</summary>
    protected virtual ReleaseHistoryDto ToDto(ConditionSetReleaseRecord record, ConditionSet set,
        IReadOnlyDictionary<Guid, int> revisionNumbers)
    {
        return new ReleaseHistoryDto
        {
            Id = record.Id,
            TargetType = ReleaseTargetType.ConditionSet,
            TargetId = set.Id,
            TargetCode = set.Code,
            TargetName = set.Name,
            ReleaseNo = record.ReleaseNo,
            ActivatedRevisionId = record.ActivatedRevisionId,
            RevisionNo = revisionNumbers.GetValueOrDefault(record.ActivatedRevisionId),
            Kind = record.Kind,
            ContentHash = record.ContentHash,
            ChangeSummary = record.ChangeSummary,
            CreationTime = record.CreationTime
        };
    }
}
