namespace NRuleEngine.Application.Contracts.Runtime;

/// <summary>不包含输入值和表达式全文的稳定运行错误。</summary>
[Serializable]
public class RuleExecutionErrorDto
{
    /// <summary>供调用方分支处理的稳定错误码。</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>可调整或本地化的安全错误描述。</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>对象标识、字段编码及字符位置等安全定位信息。</summary>
    public Dictionary<string, object?> Data { get; set; } = [];
}
