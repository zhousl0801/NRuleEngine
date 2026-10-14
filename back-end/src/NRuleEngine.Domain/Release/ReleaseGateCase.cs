using System.Text.Json;
using Volo.Abp.Domain.Entities;

namespace NRuleEngine.Domain.Release;

/// <summary>修订内的发布回归用例；每次发布按锁定契约重新试算。</summary>
public sealed class ReleaseGateCase : Entity<Guid>
{
    /// <summary>所属修订的标识。</summary>
    public Guid RevisionId { get; private set; }

    /// <summary>维护者识别此用例的名称。</summary>
    public string Name { get; private set; } = null!;

    /// <summary>按绑定维度编码索引的 JSON 对象，属性值也是 JSON 值。</summary>
    public string ContextJson { get; private set; } = null!;

    /// <summary>按入口变量编码索引的 JSON 对象，属性值也是 JSON 值。</summary>
    public string InputsJson { get; private set; } = null!;

    /// <summary>按出口变量编码索引的预期 JSON 对象，发布时要求逐项相等。</summary>
    public string ExpectedOutputsJson { get; private set; } = null!;

    private ReleaseGateCase()
    {
    }

    /// <summary>创建输入与期望均为 JSON 对象的发布门禁用例。</summary>
    public ReleaseGateCase(
        Guid id,
        Guid revisionId,
        string name,
        string contextJson,
        string inputsJson,
        string expectedOutputsJson) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            throw new ArgumentException("门禁用例名称长度必须在 1 到 256 个字符之间。", nameof(name));
        }

        ValidateObject(contextJson, nameof(contextJson));
        ValidateObject(inputsJson, nameof(inputsJson));
        ValidateObject(expectedOutputsJson, nameof(expectedOutputsJson));
        RevisionId = revisionId;
        Name = name.Trim();
        ContextJson = contextJson;
        InputsJson = inputsJson;
        ExpectedOutputsJson = expectedOutputsJson;
    }

    /// <summary>复制用例内容到新草稿，并更换实体与所属修订标识。</summary>
    internal ReleaseGateCase CopyTo(Guid revisionId)
    {
        return new ReleaseGateCase(Guid.NewGuid(), revisionId, Name, ContextJson, InputsJson, ExpectedOutputsJson);
    }

    private static void ValidateObject(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("门禁用例数据必须是 JSON 对象。", name);
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (document.RootElement.EnumerateObject().Any(property => !keys.Add(property.Name)))
        {
            throw new ArgumentException("门禁用例数据包含重复字段。", name);
        }
    }
}
