namespace NRuleEngine.Domain.RuleModels;

/// <summary>维护操作涉及的修订对象路径及是否阻止变量删除。</summary>
public sealed class RuleImpactReference
{
    /// <summary>引用或下游影响类别。</summary>
    public string Kind { get; }
    /// <summary>修订内可定位的对象路径。</summary>
    public string Path { get; }
    /// <summary>该引用是否阻止变量删除。</summary>
    public bool BlocksVariableDeletion { get; }

    /// <summary>构造只读维护影响项。</summary>
    public RuleImpactReference(string kind, string path, bool blocksVariableDeletion)
    {
        Kind = kind;
        Path = path;
        BlocksVariableDeletion = blocksVariableDeletion;
    }
}
