namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 变量类型及其可选约束。
/// </summary>
/// <param name="Kind">首版支持 Decimal、String、Boolean 和 Enum。</param>
/// <param name="IsNullable">该变量是否可以取空值。</param>
/// <param name="Precision">Decimal 的总位数，其他类型不设置。</param>
/// <param name="Scale">Decimal 的小数位数，其他类型不设置。</param>
/// <param name="EnumCode">Enum 在宿主中的编码，其他类型不设置。</param>
public sealed record RuleTypeDescriptor(
    RuleValueKind Kind,
    bool IsNullable = false,
    int? Precision = null,
    int? Scale = null,
    string? EnumCode = null);
