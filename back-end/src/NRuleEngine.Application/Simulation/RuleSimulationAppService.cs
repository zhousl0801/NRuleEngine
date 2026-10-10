using NRuleEngine.Application.Contracts.Simulation;
using NRuleEngine.Domain.Simulation;
using NRuleEngine.Application.Contracts.RuleModels;
using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Application.Services;

namespace NRuleEngine.Application.Simulation;

/// <summary>对草稿运行一次不落库的试算。</summary>
public class RuleSimulationAppService : ApplicationService, IRuleSimulationAppService
{
    private readonly IRuleModelRepository _models;
    private readonly IRuleModelRevisionRepository _revisions;
    private readonly IRuleCatalog _catalog;

    public RuleSimulationAppService(IRuleModelRepository models, IRuleModelRevisionRepository revisions,
        IRuleCatalog catalog)
    {
        _models = models;
        _revisions = revisions;
        _catalog = catalog;
    }

    /// <summary>基于指定修订执行内存试算，返回命中、赋值及出口。</summary>
    /// <remarks>核心逻辑：加载草稿及锁定契约后调用领域试算器；调用时机：维护者提交试算请求时。</remarks>
    public virtual async Task<SimulationResultDto> SimulateAsync(SimulateRuleDto input)
    {
        var revision = await _revisions.FindWithVariablesAsync(input.RevisionId)
            ?? throw new ArgumentException("修订不存在。", nameof(input));
        var model = await _models.GetAsync(revision.RuleModelId);
        var contract = _catalog.GetScenario(model.AppCode, revision.ScenarioCode, revision.ContractVersion);
        var result = RuleSimulator.Simulate(revision, contract, input.ContextJson, input.InputsJson);
        return new SimulationResultDto
        {
            RevisionId = result.RevisionId,
            BindingId = result.BindingId,
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage,
            ErrorPath = result.ErrorPath,
            ErrorVariableCode = result.ErrorVariableCode,
            Start = result.Start,
            Length = result.Length,
            Conditions = result.Conditions.Select(c => new RuleConditionResultDto
            {
                RuleId = c.RuleId, RuleName = c.RuleName, Matched = c.Matched
            }).ToArray(),
            Assignments = result.Assignments.Select(a => new RuleAssignmentResultDto
            {
                RuleId = a.RuleId, AssignmentId = a.AssignmentId,
                TargetVariableStableKey = a.TargetVariableStableKey, ValueJson = a.ValueJson
            }).ToArray(),
            OutputsJson = result.Outputs
        };
    }
}
