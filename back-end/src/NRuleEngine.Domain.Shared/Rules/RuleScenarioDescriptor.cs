namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 固定版本的规则场景契约。
/// </summary>
/// <param name="AppCode">由宿主代码注册的应用编码。</param>
/// <param name="ScenarioCode">应用内的场景编码。</param>
/// <param name="ContractVersion">契约版本，修订创建时将其锁定。</param>
/// <param name="Inputs">调用方必须提供的入口变量契约。</param>
/// <param name="RequiredOutputs">规则必须生成的出口变量契约。</param>
/// <param name="BindingDimensions">维护者可配置的场景绑定维度。</param>
public sealed record RuleScenarioDescriptor(
    string AppCode,
    string ScenarioCode,
    string ContractVersion,
    IReadOnlyList<VariableContract> Inputs,
    IReadOnlyList<VariableContract> RequiredOutputs,
    IReadOnlyList<BindingDimensionContract> BindingDimensions);
