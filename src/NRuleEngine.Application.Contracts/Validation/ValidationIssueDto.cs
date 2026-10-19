namespace NRuleEngine.Application.Contracts.Validation;

/// <summary>校验问题及对象路径。</summary>
[Serializable]
public class ValidationIssueDto
{
    /// <summary>供调用方判断问题类别的稳定编码。</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>问题所在的规则、赋值或绑定路径。</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>供维护者理解和修复问题的说明。</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>表达式错误的可选起始字符位置。</summary>
    public int? Start { get; set; }
    /// <summary>表达式错误的可选字符跨度。</summary>
    public int? Length { get; set; }
}
