namespace NRuleEngine.Domain.Shared.Release;

/// <summary>发布或回滚记录所属的对象类型。</summary>
public enum ReleaseTargetType
{
    /// <summary>规则模型。</summary>
    RuleModel,
    /// <summary>条件集。</summary>
    ConditionSet
}
