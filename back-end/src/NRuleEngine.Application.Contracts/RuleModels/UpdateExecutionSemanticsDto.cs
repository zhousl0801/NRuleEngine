using System.ComponentModel.DataAnnotations;
using NRuleEngine.Domain.Shared.Rules;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>设置草稿的命中模式与多次写入策略。</summary>
[Serializable]
public class UpdateExecutionSemanticsDto
{
    /// <summary>规则按 FirstMatch 或 Sequential 方式执行。</summary>
    public RuleExecutionMode ExecutionMode { get; set; }
    /// <summary>同一变量被多次写入时采用的裁决策略。</summary>
    public MultipleWritePolicy MultipleWritePolicy { get; set; }
    /// <summary>读取草稿时获得的并发戳，保存时用于阻止静默覆盖。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
