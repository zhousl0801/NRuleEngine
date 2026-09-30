using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Bindings;

/// <summary>修订场景绑定中的一个维度条件。</summary>
public sealed class RuleBindingCriterion : Entity<Guid>
{
    /// <summary>所属场景绑定的标识。</summary>
    public Guid BindingId { get; private set; }
    /// <summary>场景契约声明的绑定维度编码。</summary>
    public string DimensionCode { get; private set; } = null!;
    /// <summary>该维度允许的比较操作符。</summary>
    public RuleBindingOperator Operator { get; private set; }
    /// <summary>按维度值类型编码的 JSON；In 使用非空数组。</summary>
    public string TypedValueJson { get; private set; } = null!;

    private RuleBindingCriterion()
    {
    }

    /// <summary>创建已由所属绑定完成契约校验的维度条件。</summary>
    internal RuleBindingCriterion(Guid id, Guid bindingId, string dimensionCode, RuleBindingOperator op,
        string typedValueJson) : base(id)
    {
        BindingId = bindingId;
        DimensionCode = dimensionCode;
        Operator = op;
        TypedValueJson = typedValueJson;
    }

    /// <summary>复制维度条件到新绑定并重新分配实体标识。</summary>
    internal RuleBindingCriterion CopyTo(Guid bindingId)
    {
        return new RuleBindingCriterion(Guid.NewGuid(), bindingId, DimensionCode, Operator, TypedValueJson);
    }
}
