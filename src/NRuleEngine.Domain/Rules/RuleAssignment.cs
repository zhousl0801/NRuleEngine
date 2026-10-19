using NRuleEngine.Domain.RuleModels;
using NRuleEngine.Domain.Expressions;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Rules;

/// <summary>规则命中后对一个目标变量的赋值。</summary>
public sealed class RuleAssignment : Entity<Guid>
{
    /// <summary>本赋值所属的规则标识。</summary>
    public Guid RuleId { get; private set; }

    /// <summary>目标变量的稳定标识；入口变量不能作为目标。</summary>
    public Guid TargetVariableStableKey { get; private set; }

    /// <summary>常量或表达式赋值来源。</summary>
    public RuleValueSourceKind ValueSourceKind { get; private set; }

    /// <summary>常量来源时的 JSON 值；表达式来源时为空。</summary>
    public string? ConstantValueJson { get; private set; }

    /// <summary>供编辑器展示的表达式源文本，不参与计算。</summary>
    public string? ExpressionSource { get; private set; }

    /// <summary>表达式来源时的版本化 AST JSON。</summary>
    public string? ExpressionAstJson { get; private set; }

    /// <summary>同一规则内的赋值执行顺序。</summary>
    public int SortOrder { get; private set; }

    private RuleAssignment()
    {
    }

    /// <summary>验证目标与值来源，创建只能写入出口或中间变量的赋值。</summary>
    internal RuleAssignment(
        Guid id,
        Guid ruleId,
        Guid targetKey,
        RuleValueSourceKind sourceKind,
        string? constantJson,
        string? expressionSource,
        string? expressionAstJson,
        int sortOrder,
        IReadOnlyCollection<VariableDefinition> variables) : base(id)
    {
        var target = variables.SingleOrDefault(v => v.StableKey == targetKey)
            ?? throw new ArgumentException("赋值目标变量不存在。");
        if (target.Direction == RuleVariableDirection.Input)
        {
            throw new ArgumentException("不能对输入变量赋值。");
        }

        if (sourceKind == RuleValueSourceKind.Constant)
        {
            if (constantJson is null || expressionAstJson is not null)
            {
                throw new ArgumentException("常量赋值只能提供常量值。");
            }

            _ = RuleValueEngine.ReadValue(target, constantJson);
        }
        else if (sourceKind == RuleValueSourceKind.Expression)
        {
            if (constantJson is not null || string.IsNullOrWhiteSpace(expressionAstJson))
            {
                throw new ArgumentException("表达式赋值只能提供 AST。");
            }

            RuleValueEngine.ValidateExpression(target, expressionAstJson, variables, out _);
        }
        else
        {
            throw new ArgumentException("不支持的赋值来源。");
        }

        RuleId = ruleId;
        TargetVariableStableKey = targetKey;
        ValueSourceKind = sourceKind;
        ConstantValueJson = constantJson;
        ExpressionSource = expressionSource;
        ExpressionAstJson = expressionAstJson;
        SortOrder = sortOrder;
    }

    /// <summary>复制赋值到新规则，并以新修订变量重新校验 StableKey。</summary>
    internal RuleAssignment CopyTo(Guid ruleId, IReadOnlyCollection<VariableDefinition> variables)
    {
        return new RuleAssignment(Guid.NewGuid(), ruleId, TargetVariableStableKey, ValueSourceKind, ConstantValueJson, ExpressionSource, ExpressionAstJson, SortOrder, variables);
    }

    /// <summary>先验证新值，再在不更换赋值标识的前提下更新。</summary>
    internal void Update(Guid targetKey, RuleValueSourceKind sourceKind, string? constantJson, string? expressionSource, string? expressionAstJson, int sortOrder, IReadOnlyCollection<VariableDefinition> variables)
    {
        var checkedValue = new RuleAssignment(Id, RuleId, targetKey, sourceKind, constantJson, expressionSource, expressionAstJson, sortOrder, variables);
        TargetVariableStableKey = checkedValue.TargetVariableStableKey;
        ValueSourceKind = checkedValue.ValueSourceKind;
        ConstantValueJson = checkedValue.ConstantValueJson;
        ExpressionSource = checkedValue.ExpressionSource;
        ExpressionAstJson = checkedValue.ExpressionAstJson;
        SortOrder = checkedValue.SortOrder;
    }
}
