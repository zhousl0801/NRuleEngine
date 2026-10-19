namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 场景中的变量契约。
/// </summary>
/// <param name="Code">场景输入输出映射使用的稳定编码。</param>
/// <param name="Name">展示给维护者的变量名称。</param>
/// <param name="Direction">入口或必需出口方向。</param>
/// <param name="Type">值类型、可空性及相关约束。</param>
public sealed record VariableContract(
    string Code,
    string Name,
    RuleVariableDirection Direction,
    RuleTypeDescriptor Type);
