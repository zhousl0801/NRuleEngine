using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>从指定修订或活动修订复制条件草稿。</summary>
[Serializable]
public sealed class CreateConditionDraftDto
{
    /// <summary>可选来源修订；不指定时使用活动修订。</summary>
    public Guid? BasedOnRevisionId { get; set; }

    /// <summary>条件集当前的乐观并发戳。</summary>
    [Required]
    public string ExpectedSetConcurrencyStamp { get; set; } = string.Empty;
}
