namespace NRuleEngine.Application.Contracts.Conditions;

/// <summary>区分正常无命中和判定错误，并返回稳定命中身份。</summary>
[Serializable]
public sealed class ConditionEvaluationDto
{
    /// <summary>判定过程是否成功；正常无命中也为 true。</summary>
    public bool Succeeded { get; set; }
    /// <summary>是否至少有一个候选命中。</summary>
    public bool Matched { get; set; }
    /// <summary>按请求模式返回的命中集合。</summary>
    public IReadOnlyList<ConditionMatchDto> Matches { get; set; }
    /// <summary>失败时的错误编码。</summary>
    public string? ErrorCode { get; set; }
    /// <summary>失败时的可读说明。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>映射候选判定的命中或错误结果。</summary>
    public ConditionEvaluationDto(bool succeeded, bool matched, IReadOnlyList<ConditionMatchDto> matches,
        string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        Matched = matched;
        Matches = matches;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
