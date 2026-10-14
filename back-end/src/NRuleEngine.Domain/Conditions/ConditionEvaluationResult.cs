namespace NRuleEngine.Domain.Conditions;

/// <summary>条件判定结果；空匹配列表表示正常无命中，错误单独给出。</summary>
/// <param name="Matches">按裁决模式返回的命中集合。</param>
/// <param name="ErrorCode">失败时的错误编码；成功时为空。</param>
/// <param name="ErrorMessage">失败时的可读说明。</param>
public sealed record ConditionEvaluationResult(
    IReadOnlyList<ConditionMatch> Matches,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>判定过程是否成功；正常无命中也属于成功。</summary>
    public bool Succeeded
    {
        get
        {
            return ErrorCode is null;
        }
    }

    /// <summary>是否至少有一个条件集命中。</summary>
    public bool Matched
    {
        get
        {
            return Matches.Count > 0;
        }
    }
}
