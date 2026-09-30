namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 规则模型的状态。
/// </summary>
public enum RuleModelStatus
{
    /// <summary>允许继续创建草稿的模型。</summary>
    Active,
    /// <summary>停止新增草稿但保留历史的模型。</summary>
    Archived
}
