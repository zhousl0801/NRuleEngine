namespace NRuleEngine.Application.Contracts.Validation;

/// <summary>整份草稿的校验结果。</summary>
[Serializable]
public class ValidationReportDto
{
    /// <summary>已校验的规则修订标识。</summary>
    public Guid RevisionId { get; set; }
    /// <summary>当前修订是否通过整份校验。</summary>
    public bool IsValid { get; set; }
    /// <summary>未通过时的具体问题；通过时为空集合。</summary>
    public IReadOnlyList<ValidationIssueDto> Issues { get; set; } = [];
}
