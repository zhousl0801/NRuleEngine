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

    /// <summary>缺失入口或未赋值中间变量的默认 JSON 值；null 表示未配置。</summary>
    public string? DefaultValueJson { get; set; }

    /// <summary>用户中间变量的显示或业务单位。</summary>
    public string? Unit { get; set; }

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
