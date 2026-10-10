namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>维护影响中的可定位引用或下游对象。</summary>
[Serializable]
public class ImpactReferenceDto
{
    /// <summary>引用或影响类别。</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>修订内可定位的对象路径。</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>该引用是否会阻止变量删除。</summary>
    public bool BlocksVariableDeletion { get; set; }
}
