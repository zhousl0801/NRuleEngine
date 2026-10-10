using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>单独修改用户中间变量的 Decimal 精度。</summary>
[Serializable]
public class SetIntermediatePrecisionDto
{
    /// <summary>Decimal 总位数；null 恢复类型默认精度。</summary>
    public int? Precision { get; set; }

    /// <summary>期望的修订并发戳。</summary>
    [Required]
    public string ExpectedRevisionConcurrencyStamp { get; set; } = string.Empty;
}
