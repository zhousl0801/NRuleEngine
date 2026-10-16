using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NRuleEngine.Application.Contracts.Runtime;
using NRuleEngine.Domain.Release;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.Domain.Simulation;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Runtime;

/// <summary>从活动发布修订中选择唯一绑定，并执行确定性规则计算。</summary>
public class RuleExecutionAppService : ApplicationService, IRuleExecutionAppService
{
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;
    private readonly IRuleCatalog _catalog;
    private readonly IOptions<RuleExecutionOptions> _options;
    private readonly ILogger<RuleExecutionAppService> _logger;

    public RuleExecutionAppService(IRuleModelRepository models, IRuleModelRevisionRepository revisions,
        IRuleCatalog catalog, IOptions<RuleExecutionOptions> options, ILogger<RuleExecutionAppService> logger)
    {
        _models = models;
        _revisions = revisions;
        _catalog = catalog;
        _options = options;
        _logger = logger;
    }

    /// <summary>在宿主配置的时间预算内完成目录、绑定、输入和规则求值。</summary>
    /// <remarks>核心逻辑：区分调用方取消、内部超时与存储或程序故障；调用时机：宿主调用规则执行入口时。</remarks>
    public virtual async Task<RuleExecutionResultDto> ExecuteAsync(ExecuteRuleDto input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executionId = GuidGenerator.Create();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            if (_options.Value.Timeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("规则执行超时时长必须大于零。");
            }

            timeout.CancelAfter(_options.Value.Timeout);
            return await ExecuteCoreAsync(executionId, input, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("规则执行 {ExecutionId} 超过时间预算", executionId);

            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.ExecutionTimeout).WithData("ExecutionId", executionId).WithData("TimeoutMilliseconds", _options.Value.Timeout.TotalMilliseconds);
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "规则执行 {ExecutionId} 无法读取存储", executionId);
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.StorageUnavailable).WithData("ExecutionId", executionId);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则执行 {ExecutionId} 发生意外错误", executionId);
            throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.UnexpectedExecutionError).WithData("ExecutionId", executionId);
        }
    }

    /// <summary>批量载入活动修订，验证发布内容后按全场景优先级选择唯一绑定。</summary>
    /// <remarks>核心逻辑：校验活动发布内容哈希并在所有模型间选择最高优先级的唯一绑定；调用时机：执行入口完成超时设置后。</remarks>
    protected virtual async Task<RuleExecutionResultDto> ExecuteCoreAsync(Guid executionId, ExecuteRuleDto input, CancellationToken cancellationToken)
    {
        var appCode = input.AppCode?.Trim() ?? string.Empty;
        var scenarioCode = input.ScenarioCode?.Trim() ?? string.Empty;
        if (_catalog.GetApps().All(x => x.AppCode != appCode))
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.AppNotRegistered, "规则应用尚未注册。", data: new() { ["AppCode"] = appCode });
        }

        var contracts = _catalog.GetScenarios(appCode).Where(x => x.ScenarioCode == scenarioCode).ToDictionary(x => x.ContractVersion, StringComparer.Ordinal);

        if (contracts.Count == 0)
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ScenarioNotRegistered, "规则场景尚未注册。", data: new() { ["AppCode"] = appCode, ["ScenarioCode"] = scenarioCode });
        }

        if (input.ContractVersion is not null && !contracts.ContainsKey(input.ContractVersion))
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ContractVersionNotSupported,
                "不支持请求的规则场景契约版本。", data: new()
                {
                    ["AppCode"] = appCode, ["ScenarioCode"] = scenarioCode,
                    ["ContractVersion"] = input.ContractVersion
                });
        }

        var models = (await _models.GetListAsync(appCode, scenarioCode, cancellationToken)).Where(x => x.Status == RuleModelStatus.Active && x.ActiveRevisionId.HasValue).ToArray();
        if (models.Length == 0)
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.NoPublishedModel, "此场景没有活动的已发布模型。");
        }

        var revisions = (await _revisions.GetByIdsWithDetailsAsync(models.Select(x => x.ActiveRevisionId!.Value).ToArray(), cancellationToken)).ToDictionary(x => x.Id);
        var candidates = new List<(RuleModel Model, RuleModelRevision Revision, Guid BindingId, int Priority, RuleScenarioDescriptor Contract)>();
        var requiredDimensionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var bindingCount = 0;
        var context = (input.Context ?? []).ToDictionary(x => x.Key, x => x.Value.GetRawText(), StringComparer.Ordinal);
        var inputs = (input.Inputs ?? []).ToDictionary(x => x.Key, x => x.Value.GetRawText(), StringComparer.Ordinal);
        var versionMatched = false;
        foreach (var model in models)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!revisions.TryGetValue(model.ActiveRevisionId!.Value, out var revision) ||
                revision.RuleModelId != model.Id || revision.Status != RuleRevisionStatus.Published ||
                string.IsNullOrWhiteSpace(revision.ContentHash))
            {
                return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.PublishedRevisionUnavailable, "活动的已发布修订不可用。", model.Id, model.ActiveRevisionId);
            }

            if (!contracts.TryGetValue(revision.ContractVersion, out var contract))
            {
                return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ScenarioContractMismatch, "已发布修订锁定的场景契约不可用。", model.Id, revision.Id);
            }

            string actualHash;
            try
            {
                actualHash = RuleRevisionContentHasher.Compute(appCode, revision, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
            {
                _logger.LogError(ex, "执行 {ExecutionId} 的已发布规则内容无法计算哈希", executionId);
                throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.PublishedContentCorrupted).WithData("ExecutionId", executionId).WithData("RuleModelId", model.Id).WithData("RevisionId", revision.Id);
            }

            if (actualHash != revision.ContentHash)
            {
                _logger.LogError("执行 {ExecutionId} 的已发布规则内容哈希不一致，模型 {RuleModelId}，修订 {RevisionId}", executionId, model.Id, revision.Id);
                throw NRuleEngineBusinessExceptions.Create(NRuleEngineErrorCodes.PublishedContentCorrupted).WithData("ExecutionId", executionId).WithData("RuleModelId", model.Id).WithData("RevisionId", revision.Id);
            }

            if (input.ContractVersion is not null && revision.ContractVersion != input.ContractVersion)
            {
                continue;
            }

            versionMatched = true;
            foreach (var binding in revision.Bindings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bindingCount++;
                foreach (var criterion in binding.Criteria)
                {
                    requiredDimensionCounts[criterion.DimensionCode] = requiredDimensionCounts.GetValueOrDefault(criterion.DimensionCode) + 1;
                }

                if (RuleSimulator.MatchesBinding(binding, contract, context, cancellationToken))
                {
                    candidates.Add((model, revision, binding.Id, binding.Priority, contract));
                }
            }
        }

        if (!versionMatched)
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ScenarioContractMismatch, "没有活动的已发布修订使用请求的契约版本。");
        }

        if (candidates.Count == 0)
        {
            var missingDimension = requiredDimensionCounts.Where(x => x.Value == bindingCount && !context.ContainsKey(x.Key)).Select(x => x.Key).Order(StringComparer.Ordinal).FirstOrDefault();
            if (missingDimension is not null)
            {
                return Error(executionId, RuleExecutionStatus.Rejected,
                    NRuleEngineErrorCodes.RequiredContextMissing,
                    "缺少必需的场景维度。", data: new()
                    {
                        ["DimensionCode"] = missingDimension
                    });
            }

            return Error(executionId, RuleExecutionStatus.NotMatched, NRuleEngineErrorCodes.NoMatchingBinding,
                "没有活动绑定匹配给定场景上下文。", data: new()
                {
                    ["AppCode"] = appCode, ["ScenarioCode"] = scenarioCode,
                    ["ContextKeys"] = context.Keys.Order(StringComparer.Ordinal).Take(20).ToArray()
                });
        }

        var highest = candidates.Max(x => x.Priority);
        var winners = candidates.Where(x => x.Priority == highest).ToArray();
        if (winners.Length > 1)
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.AmbiguousBinding,
                "最高优先级存在多条命中的绑定。", data: new()
                {
                    ["Priority"] = highest,
                    ["CandidateModelIds"] = winners.Select(x => x.Model.Id).Distinct().Take(20).ToArray(),
                    ["CandidateBindingIds"] = winners.Select(x => x.BindingId).Take(20).ToArray()
                });
        }

        var selected = winners[0];
        var outcome = RuleSimulator.Simulate(selected.Revision, selected.Contract, context, inputs, cancellationToken);
        if (outcome.Success)
        {
            return new RuleExecutionResultDto
            {
                ExecutionId = executionId, Status = RuleExecutionStatus.Succeeded,
                RuleModelId = selected.Model.Id, RevisionId = selected.Revision.Id,
                BindingId = outcome.BindingId,
                Results = RuleResultRowDtoMapper.Map(outcome.Results)
            };
        }

        return MapSimulationFailure(executionId, selected.Model.Id, selected.Revision.Id, outcome);
    }

    /// <summary>把试算内部原因转换为已发布的稳定运行状态和错误码。</summary>
    /// <remarks>核心逻辑：将内部试算失败归类为稳定状态和错误码并附定位字段；调用时机：已选中发布修订但未生成有效结果时。</remarks>
    protected virtual RuleExecutionResultDto MapSimulationFailure(Guid executionId, Guid modelId, Guid revisionId, RuleSimulationOutcome outcome)
    {
        var (status, code, message) = outcome.ErrorCode switch
        {
            "NoMatchingRule" => (RuleExecutionStatus.NotMatched, NRuleEngineErrorCodes.NoMatchingRule, "当前模型没有规则命中。"),
            "ResultLimitExceeded" => (RuleExecutionStatus.Failed, NRuleEngineErrorCodes.ResultLimitExceeded, "结果行数或大小超过限制。"),
            "NoMatchingBinding" => (RuleExecutionStatus.NotMatched, NRuleEngineErrorCodes.NoMatchingBinding,
                "没有活动绑定匹配给定场景上下文。"),
            "AmbiguousBinding" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.AmbiguousBinding,
                "最高优先级存在多条命中的绑定。"),
            "UnknownInput" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.UnknownInput,
                "提供了未声明的输入变量。"),
            "MissingInput" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.RequiredInputMissing,
                "缺少必需的输入变量。"),
            "InputTypeMismatch" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.InputTypeMismatch,
                "输入值无效。"),
            "InputConstraintViolated" => (RuleExecutionStatus.Rejected,
                NRuleEngineErrorCodes.InputConstraintViolated, "输入值不符合声明的约束。"),
            "MultipleWrite" => (RuleExecutionStatus.Failed, NRuleEngineErrorCodes.MultipleWriteConflict,
                "同一变量被重复赋值。"),
            "MissingOutput" => (RuleExecutionStatus.Failed, NRuleEngineErrorCodes.RequiredOutputMissing,
                "未生成必需的输出变量。"),
            "ConditionFailed" or "AssignmentFailed" => (RuleExecutionStatus.Failed,
                NRuleEngineErrorCodes.ExpressionEvaluationFailed, "规则求值失败。"),
            _ => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ScenarioContractMismatch,
                "已发布修订无法使用其锁定的契约求值。")
        };
        var data = new Dictionary<string, object?>();
        if (outcome.ErrorPath is not null)
        {
            data["ErrorPath"] = outcome.ErrorPath;
        }

        if (outcome.ErrorVariableCode is not null)
        {
            data["VariableCode"] = outcome.ErrorVariableCode[..Math.Min(128, outcome.ErrorVariableCode.Length)];
        }

        if (outcome.Start is not null)
        {
            data["SourceStart"] = outcome.Start;
        }

        if (outcome.Length is not null)
        {
            data["SourceLength"] = outcome.Length;
        }

        return Error(executionId, status, code, message, modelId, revisionId, outcome.BindingId, data);
    }

    /// <summary>生成带稳定错误码及定位数据的执行结果。</summary>
    /// <remarks>核心逻辑：补入模型、修订和绑定标识并保持错误数据结构；调用时机：执行遇到可预期失败时。</remarks>
    private static RuleExecutionResultDto Error(Guid executionId, RuleExecutionStatus status, string code, string message, Guid? modelId = null, Guid? revisionId = null, Guid? bindingId = null, Dictionary<string, object?>? data = null)
    {
        data ??= [];
        if (modelId is not null)
        {
            data.TryAdd("RuleModelId", modelId);
        }

        if (revisionId is not null)
        {
            data.TryAdd("RevisionId", revisionId);
        }

        if (bindingId is not null)
        {
            data.TryAdd("BindingId", bindingId);
        }

        return new RuleExecutionResultDto
        {
            ExecutionId = executionId, Status = status, RuleModelId = modelId, RevisionId = revisionId,
            BindingId = bindingId,
            Error = new RuleExecutionErrorDto { Code = code, Message = message, Data = data }
        };
    }
}
