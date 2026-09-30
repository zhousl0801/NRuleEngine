using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 修订中的变量定义。
/// </summary>
[Serializable]
public class RuleVariableDto
{
    /// <summary>
    /// 标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 跨修订稳定标识。
    /// </summary>
    public Guid StableKey { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 变量方向。
    /// </summary>
    public RuleVariableDirection Direction { get; set; }

    /// <summary>
    /// 变量来源。
    /// </summary>
    public RuleVariableOrigin Origin { get; set; }

    /// <summary>
    /// 变量类型。
    /// </summary>
    public RuleTypeDescriptor Type { get; set; } = null!;

    public RuleVariableDto()
    {
    }

    public RuleVariableDto(
        Guid id, Guid stableKey, string code,
        string name, RuleVariableDirection direction, RuleVariableOrigin origin,
        RuleTypeDescriptor type)
    {
        Id = id;
        StableKey = stableKey;
        Code = code;
        Name = name;
        Direction = direction;
        Origin = origin;
        Type = type;
    }
}
