namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 接入应用的注册信息。
/// </summary>
/// <param name="AppCode">供所有场景和模型引用的应用编码。</param>
/// <param name="Name">应用的展示名称。</param>
/// <param name="Provider">注册该应用的宿主或模块。</param>
public sealed record RuleAppDescriptor(string AppCode, string Name, string Provider);
