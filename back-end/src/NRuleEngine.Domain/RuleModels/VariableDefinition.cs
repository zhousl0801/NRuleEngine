using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.RuleModels;

/// <summary>
/// 修订中的变量，使用稳定标识关联不同修订里的同一变量。
/// </summary>
public sealed class VariableDefinition : Entity<Guid>, IExpressionVariable
{
    /// <summary>本变量所属的修订标识。</summary>
    public Guid RevisionId { get; private set; }
    /// <summary>跨修订保持不变的逻辑变量标识；AST 用它引用变量。</summary>
    public Guid StableKey { get; private set; }
    /// <summary>场景或用户定义的变量编码；允许在中间变量上修改。</summary>
    public string Code { get; private set; } = null!;
    /// <summary>变量的展示名称。</summary>
    public string Name { get; private set; } = null!;
    /// <summary>入口、中间变量或必需出口方向。</summary>
    public RuleVariableDirection Direction { get; private set; }
    /// <summary>来自代码契约或草稿中的用户定义。</summary>
    public RuleVariableOrigin Origin { get; private set; }
    /// <summary>Decimal、String、Boolean 或 Enum 值类型。</summary>
    public RuleValueKind ValueKind { get; private set; }
    /// <summary>输入与赋值是否接受 JSON null。</summary>
    public bool IsNullable { get; private set; }
    /// <summary>Decimal 值允许的总位数。</summary>
    public int? Precision { get; private set; }
    /// <summary>Decimal 值允许的小数位数。</summary>
    public int? Scale { get; private set; }
    /// <summary>Enum 类型在宿主中的枚举编码。</summary>
    public string? EnumCode { get; private set; }
    /// <summary>可选默认值的 JSON 表示；当前编制 API 尚未维护。</summary>
    public string? DefaultValueJson { get; private set; }
    /// <summary>变量的显示或业务单位。</summary>
    public string? Unit { get; private set; }
    /// <summary>变量在列表中的展示顺序。</summary>
    public int SortOrder { get; private set; }

    private VariableDefinition()
    {
    }

    private VariableDefinition(Guid id, Guid revisionId, Guid stableKey, string code, string name,
        RuleVariableDirection direction, RuleVariableOrigin origin, RuleTypeDescriptor type) : base(id)
    {
        RevisionId = revisionId;
        StableKey = stableKey;
        Code = code;
        Name = name;
        Direction = direction;
        Origin = origin;
        ValueKind = type.Kind;
        IsNullable = type.IsNullable;
        Precision = type.Precision;
        Scale = type.Scale;
        EnumCode = type.EnumCode;
    }

    /// <summary>将场景契约变量实例化到指定修订，并生成新的稳定标识。</summary>
    internal static VariableDefinition FromContract(Guid id, Guid revisionId, VariableContract contract)
    {
        return new VariableDefinition(id, revisionId, Guid.NewGuid(), contract.Code, contract.Name,
            contract.Direction, RuleVariableOrigin.Contract, contract.Type);
    }

    /// <summary>创建只在本规则模型内部使用的中间变量。</summary>
    internal static VariableDefinition NewIntermediate(Guid id, Guid revisionId, Guid stableKey,
        string code, string name, RuleTypeDescriptor type)
    {
        return new VariableDefinition(id, revisionId, stableKey, code, name,
            RuleVariableDirection.Intermediate, RuleVariableOrigin.UserDefined, type);
    }

    /// <summary>复制到新草稿，保留 StableKey 并重新分配实体标识。</summary>
    internal VariableDefinition CopyTo(Guid revisionId)
    {
        return new VariableDefinition(Guid.NewGuid(), revisionId, StableKey, Code, Name, Direction, Origin,
            new RuleTypeDescriptor(ValueKind, IsNullable, Precision, Scale, EnumCode))
        {
            DefaultValueJson = DefaultValueJson,
            Unit = Unit,
            SortOrder = SortOrder
        };
    }

    /// <summary>只允许重命名用户定义的中间变量，契约变量保持固定。</summary>
    internal void Rename(string code, string name)
    {
        if (Origin != RuleVariableOrigin.UserDefined)
        {
            throw new InvalidOperationException("Contract variable code and name cannot be changed through this operation.");
        }
        Code = code;
        Name = name;
    }
}
