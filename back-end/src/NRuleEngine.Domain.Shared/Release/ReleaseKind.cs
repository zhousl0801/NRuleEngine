namespace NRuleEngine.Domain.Shared.Release;

/// <summary>活动修订切换的原因。</summary>
public enum ReleaseKind
{
    /// <summary>首次激活一份通过门禁的草稿。</summary>
    Publish,
    /// <summary>重新激活一份历史发布修订。</summary>
    Rollback
}
