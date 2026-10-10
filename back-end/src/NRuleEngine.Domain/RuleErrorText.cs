using System.Text.Json;

namespace NRuleEngine.Domain;

/// <summary>将可预期的框架异常转换为不泄露内部细节的中文诊断说明。</summary>
internal static class RuleErrorText
{
    /// <summary>为传给校验和试算结果的异常生成中文说明。</summary>
    /// <remarks>核心逻辑：保留已有中文诊断，其余框架异常按类型归类；调用时机：捕获可预期异常后组装结果时。</remarks>
    public static string Describe(Exception exception)
    {
        foreach (var character in exception.Message)
        {
            if (character is >= '\u4e00' and <= '\u9fff')
            {
                return exception.Message;
            }
        }

        return exception switch
        {
            JsonException => "JSON 格式无效。",
            ArithmeticException => "算术计算失败。",
            ArgumentException => "参数不符合规则约束。",
            InvalidOperationException => "当前操作不符合规则要求。",
            _ => "规则求值失败。"
        };
    }
}
