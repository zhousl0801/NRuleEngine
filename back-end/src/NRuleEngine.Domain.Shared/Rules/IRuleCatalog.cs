namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 提供已注册应用和场景的只读目录。
/// </summary>
public interface IRuleCatalog
{
    /// <summary>列出已注册的应用。</summary>
    IReadOnlyList<RuleAppDescriptor> GetApps();
    /// <summary>列出指定应用的场景及契约版本。</summary>
    IReadOnlyList<RuleScenarioDescriptor> GetScenarios(string appCode);
    /// <summary>按精确契约版本读取场景，不自动升级历史修订。</summary>
    RuleScenarioDescriptor GetScenario(string appCode, string scenarioCode, string contractVersion);

    /// <summary>列出应用的条件判定场景契约。</summary>
    IReadOnlyList<ConditionScenarioDescriptor> GetConditionScenarios(string appCode);

    /// <summary>按精确版本读取条件判定场景契约。</summary>
    ConditionScenarioDescriptor GetConditionScenario(string appCode, string scenarioCode, string contractVersion);
}
