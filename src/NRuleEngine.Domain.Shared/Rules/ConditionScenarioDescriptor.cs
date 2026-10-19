namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>条件集使用的固定输入契约；条件判定没有业务输出变量。</summary>
/// <param name="AppCode">代码注册的接入应用编码。</param>
/// <param name="ScenarioCode">条件判定场景编码。</param>
/// <param name="ContractVersion">场景输入契约的精确版本。</param>
/// <param name="Inputs">该版本唯一允许的输入变量集合。</param>
public sealed record ConditionScenarioDescriptor(
    string AppCode,
    string ScenarioCode,
    string ContractVersion,
    IReadOnlyList<VariableContract> Inputs);
