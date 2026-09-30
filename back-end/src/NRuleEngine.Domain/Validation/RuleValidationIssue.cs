namespace NRuleEngine.Domain.Validation;

/// <summary>整份修订校验发现的一条问题。</summary>
/// <param name="Code">供程序判断的问题编码。</param>
/// <param name="Path">问题所属规则、赋值或绑定的路径。</param>
/// <param name="Message">供维护者排查的说明。</param>
/// <param name="Start">表达式源文本中的可选起始字符位置。</param>
/// <param name="Length">表达式源文本中的可选字符跨度。</param>
public sealed record RuleValidationIssue(string Code, string Path, string Message, int? Start = null, int? Length = null);
