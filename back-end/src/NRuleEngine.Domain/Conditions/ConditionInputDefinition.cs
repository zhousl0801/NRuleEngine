using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Conditions;

/// <summary>条件场景的输入快照；StableKey 供 AST 跨修订引用。</summary>
public sealed class ConditionInputDefinition : Entity<Guid>, IExpressionVariable
{
    /// <summary>输入快照所属的条件集修订标识。</summary>
    public Guid RevisionId { get; private set; }
    /// <summary>跨修订稳定的逻辑变量标识，供 AST 引用。</summary>
    public Guid StableKey { get; private set; }
    /// <summary>场景契约中固定的输入编码。</summary>
    public string Code { get; private set; } = null!;
    /// <summary>输入的展示名称。</summary>
    public string Name { get; private set; } = null!;
    /// <summary>输入的基础值类型。</summary>
    public RuleValueKind ValueKind { get; private set; }
    /// <summary>是否接受 JSON null。</summary>
    public bool IsNullable { get; private set; }
    /// <summary>Decimal 输入允许的总位数。</summary>
    public int? Precision { get; private set; }
    /// <summary>Decimal 输入允许的小数位数。</summary>
    public int? Scale { get; private set; }
    /// <summary>宿主声明的枚举编码；非 Enum 输入为空。</summary>
    public string? EnumCode { get; private set; }

    private ConditionInputDefinition()
    {
    }

    private ConditionInputDefinition(Guid id, Guid revisionId, Guid stableKey, VariableContract contract) : base(id)
    {
        RevisionId = revisionId;
        StableKey = stableKey;
        Code = contract.Code;
        Name = contract.Name;
        ValueKind = contract.Type.Kind;
        IsNullable = contract.Type.IsNullable;
        Precision = contract.Type.Precision;
        Scale = contract.Type.Scale;
        EnumCode = contract.Type.EnumCode;
    }

    /// <summary>将场景输入契约实例化到修订，并生成新的稳定键。</summary>
    internal static ConditionInputDefinition FromContract(Guid revisionId, VariableContract contract)
    {
        return new ConditionInputDefinition(Guid.NewGuid(), revisionId, Guid.NewGuid(), contract);
    }

    /// <summary>复制到新草稿，保留逻辑稳定键并更换实体标识。</summary>
    internal ConditionInputDefinition CopyTo(Guid revisionId)
    {
        return new ConditionInputDefinition(Guid.NewGuid(), revisionId, StableKey,
            new VariableContract(Code, Name, RuleVariableDirection.Input,
                new RuleTypeDescriptor(ValueKind, IsNullable, Precision, Scale, EnumCode)));
    }
}
