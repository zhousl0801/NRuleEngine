using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>条件输入及供 AST 引用的稳定键。</summary>
[Serializable]
public sealed class ConditionInputDto
{
    /// <summary>跨修订稳定的逻辑变量标识。</summary>
    public Guid StableKey { get; set; }
    /// <summary>场景输入编码。</summary>
    public string Code { get; set; }
    /// <summary>输入展示名称。</summary>
    public string Name { get; set; }
    /// <summary>基础值类型。</summary>
    public RuleValueKind ValueKind { get; set; }
    /// <summary>是否接受 JSON null。</summary>
    public bool IsNullable { get; set; }
    /// <summary>Decimal 总位数约束。</summary>
    public int? Precision { get; set; }
    /// <summary>Decimal 小数位约束。</summary>
    public int? Scale { get; set; }
    /// <summary>宿主中的枚举编码。</summary>
    public string? EnumCode { get; set; }

    /// <summary>映射修订中的固定输入快照。</summary>
    public ConditionInputDto(Guid stableKey, string code, string name, RuleValueKind valueKind,
        bool isNullable, int? precision, int? scale, string? enumCode)
    {
        StableKey = stableKey;
        Code = code;
        Name = name;
        ValueKind = valueKind;
        IsNullable = isNullable;
        Precision = precision;
        Scale = scale;
        EnumCode = enumCode;
    }
}
