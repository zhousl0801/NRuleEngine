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
    public virtual async Task<RuleExecutionResultDto> ExecuteAsync(ExecuteRuleDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executionId = GuidGenerator.Create();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            if (_options.Value.Timeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Rule execution timeout must be positive.");
            }
            timeout.CancelAfter(_options.Value.Timeout);
            return await ExecuteCoreAsync(executionId, input, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Rule execution {ExecutionId} exceeded its time budget", executionId);
            throw new BusinessException(NRuleEngineErrorCodes.ExecutionTimeout)
                .WithData("ExecutionId", executionId).WithData("TimeoutMilliseconds", _options.Value.Timeout.TotalMilliseconds);
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "Rule execution {ExecutionId} could not read storage", executionId);
            throw new BusinessException(NRuleEngineErrorCodes.StorageUnavailable).WithData("ExecutionId", executionId);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rule execution {ExecutionId} failed unexpectedly", executionId);
            throw new BusinessException(NRuleEngineErrorCodes.UnexpectedExecutionError)
                .WithData("ExecutionId", executionId);
        }
    }

    /// <summary>批量载入活动修订，验证发布内容后按全场景优先级选择唯一绑定。</summary>
    protected virtual async Task<RuleExecutionResultDto> ExecuteCoreAsync(Guid executionId, ExecuteRuleDto input,
        CancellationToken cancellationToken)
    {
        var appCode = input.AppCode?.Trim() ?? string.Empty;
        var scenarioCode = input.ScenarioCode?.Trim() ?? string.Empty;
        if (!_catalog.GetApps().Any(x => x.AppCode == appCode))
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.AppNotRegistered,
                "Rule application is not registered.", data: new() { ["AppCode"] = appCode });
        }
        var contracts = _catalog.GetScenarios(appCode).Where(x => x.ScenarioCode == scenarioCode)
            .ToDictionary(x => x.ContractVersion, StringComparer.Ordinal);
        if (contracts.Count == 0)
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ScenarioNotRegistered,
                "Rule scenario is not registered.", data: new() { ["AppCode"] = appCode, ["ScenarioCode"] = scenarioCode });
        }
        if (input.ContractVersion is not null && !contracts.ContainsKey(input.ContractVersion))
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ContractVersionNotSupported,
                "Rule scenario contract version is not supported.", data: new()
                {
                    ["AppCode"] = appCode, ["ScenarioCode"] = scenarioCode,
                    ["ContractVersion"] = input.ContractVersion
                });
        }

        var models = (await _models.GetListAsync(appCode, scenarioCode, cancellationToken))
            .Where(x => x.Status == RuleModelStatus.Active && x.ActiveRevisionId.HasValue).ToArray();
        if (models.Length == 0)
        {
            return Error(executionId, RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.NoPublishedModel,
                "No active published model exists for the scenario.");
        }
        var revisions = (await _revisions.GetByIdsWithDetailsAsync(
                models.Select(x => x.ActiveRevisionId!.Value).ToArray(), cancellationToken))
            .ToDictionary(x => x.Id);
        var candidates = new List<(RuleModel Model, RuleModelRevision Revision, Guid BindingId, int Priority,
            RuleScenarioDescriptor Contract)>();
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
                return Error(executionId, RuleExecutionStatus.Rejected,
                    NRuleEngineErrorCodes.PublishedRevisionUnavailable,
                    "An active published revision is unavailable.", model.Id, model.ActiveRevisionId);
            }
            if (!contracts.TryGetValue(revision.ContractVersion, out var contract))
            {
                return Error(executionId, RuleExecutionStatus.Rejected,
                    NRuleEngineErrorCodes.ScenarioContractMismatch,
                    "A pinned scenario contract is unavailable.", model.Id, revision.Id);
            }
            string actualHash;
            try
            {
                actualHash = RuleRevisionContentHasher.Compute(appCode, revision, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
            {
                _logger.LogError(ex, "Published rule content for {ExecutionId} could not be hashed", executionId);
                throw new BusinessException(NRuleEngineErrorCodes.PublishedContentCorrupted)
                    .WithData("ExecutionId", executionId).WithData("RuleModelId", model.Id)
                    .WithData("RevisionId", revision.Id);
            }
            if (actualHash != revision.ContentHash)
            {
                _logger.LogError("Published rule content hash differs for {ExecutionId}, model {RuleModelId}, revision {RevisionId}",
                    executionId, model.Id, revision.Id);
                throw new BusinessException(NRuleEngineErrorCodes.PublishedContentCorrupted)
                    .WithData("ExecutionId", executionId).WithData("RuleModelId", model.Id)
                    .WithData("RevisionId", revision.Id);
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
                    requiredDimensionCounts[criterion.DimensionCode] =
                        requiredDimensionCounts.GetValueOrDefault(criterion.DimensionCode) + 1;
                }
                if (RuleSimulator.MatchesBinding(binding, contract, context, cancellationToken))
                {
                    candidates.Add((model, revision, binding.Id, binding.Priority, contract));
                }
            }
        }
        if (!versionMatched)
        {
            return Error(executionId, RuleExecutionStatus.Rejected,
                NRuleEngineErrorCodes.ScenarioContractMismatch,
                "No active published revision uses the requested contract version.");
        }
        if (candidates.Count == 0)
        {
            var missingDimension = requiredDimensionCounts
                .Where(x => x.Value == bindingCount && !context.ContainsKey(x.Key))
                .Select(x => x.Key).Order(StringComparer.Ordinal).FirstOrDefault();
            if (missingDimension is not null)
            {
                return Error(executionId, RuleExecutionStatus.Rejected,
                    NRuleEngineErrorCodes.RequiredContextMissing,
                    "A required context dimension is missing.", data: new()
                    {
                        ["DimensionCode"] = missingDimension
                    });
            }
            return Error(executionId, RuleExecutionStatus.NotMatched, NRuleEngineErrorCodes.NoMatchingBinding,
                "No active binding matches the supplied context.", data: new()
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
                "Several bindings match at the highest priority.", data: new()
                {
                    ["Priority"] = highest,
                    ["CandidateModelIds"] = winners.Select(x => x.Model.Id).Distinct().Take(20).ToArray(),
                    ["CandidateBindingIds"] = winners.Select(x => x.BindingId).Take(20).ToArray()
                });
        }
        var selected = winners[0];
        var outcome = RuleSimulator.Simulate(selected.Revision, selected.Contract, context, inputs,
            cancellationToken);
        if (outcome.Success)
        {
            return new RuleExecutionResultDto
            {
                ExecutionId = executionId, Status = RuleExecutionStatus.Succeeded,
                RuleModelId = selected.Model.Id, RevisionId = selected.Revision.Id,
                BindingId = outcome.BindingId,
                Outputs = outcome.Outputs.ToDictionary(x => x.Key, x => ParseValue(x.Value), StringComparer.Ordinal)
            };
        }
        return MapSimulationFailure(executionId, selected.Model.Id, selected.Revision.Id, outcome);
    }

    /// <summary>把试算内部原因转换为已发布的稳定运行状态和错误码。</summary>
    protected virtual RuleExecutionResultDto MapSimulationFailure(Guid executionId, Guid modelId, Guid revisionId,
        RuleSimulationOutcome outcome)
    {
        var (status, code, message) = outcome.ErrorCode switch
        {
            "NoMatchingBinding" => (RuleExecutionStatus.NotMatched, NRuleEngineErrorCodes.NoMatchingBinding,
                "No active binding matches the supplied context."),
            "AmbiguousBinding" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.AmbiguousBinding,
                "Several bindings match at the highest priority."),
            "UnknownInput" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.UnknownInput,
                "An undeclared input was supplied."),
            "MissingInput" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.RequiredInputMissing,
                "A required input is missing."),
            "InputTypeMismatch" => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.InputTypeMismatch,
                "An input value is invalid."),
            "InputConstraintViolated" => (RuleExecutionStatus.Rejected,
                NRuleEngineErrorCodes.InputConstraintViolated, "An input value violates its declared constraints."),
            "MultipleWrite" => (RuleExecutionStatus.Failed, NRuleEngineErrorCodes.MultipleWriteConflict,
                "A variable was assigned more than once."),
            "MissingOutput" => (RuleExecutionStatus.Failed, NRuleEngineErrorCodes.RequiredOutputMissing,
                "A required output was not produced."),
            "ConditionFailed" or "AssignmentFailed" => (RuleExecutionStatus.Failed,
                NRuleEngineErrorCodes.ExpressionEvaluationFailed, "Rule evaluation failed."),
            _ => (RuleExecutionStatus.Rejected, NRuleEngineErrorCodes.ScenarioContractMismatch,
                "The published revision cannot be evaluated with its pinned contract.")
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

    private static JsonElement ParseValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static RuleExecutionResultDto Error(Guid executionId, RuleExecutionStatus status, string code,
        string message, Guid? modelId = null, Guid? revisionId = null, Guid? bindingId = null,
        Dictionary<string, object?>? data = null)
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
