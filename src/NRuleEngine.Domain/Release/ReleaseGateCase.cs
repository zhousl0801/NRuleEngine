using System.Text.Json;
using System.Text;
using NRuleEngine.Domain.Shared.Runtime;
using NRuleEngine.Domain.Simulation;
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

    /// <summary>有序的预期出口对象数组，不包含来源规则标识。</summary>
    public string ExpectedResultsJson { get; private set; } = null!;

    /// <summary>预期成功或正常未匹配，不能用失败样本放行发布。</summary>
    public RuleExecutionStatus ExpectedStatus { get; private set; }

    private ReleaseGateCase()
    {
    }

    /// <summary>创建上下文、输入对象及有序预期结果行的发布门禁用例。</summary>
    public ReleaseGateCase(
        Guid id,
        Guid revisionId,
        string name,
        string contextJson,
        string inputsJson,
        string expectedResultsJson,
        RuleExecutionStatus expectedStatus = RuleExecutionStatus.Succeeded) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            throw new ArgumentException("门禁用例名称长度必须在 1 到 256 个字符之间。", nameof(name));
        }

        ValidateObject(contextJson, nameof(contextJson));
        ValidateObject(inputsJson, nameof(inputsJson));
        if (expectedStatus is not (RuleExecutionStatus.Succeeded or RuleExecutionStatus.NotMatched) ||
            Encoding.UTF8.GetByteCount(expectedResultsJson) > RuleResultRows.MaxJsonBytes)
        {
            throw new ArgumentException("门禁只接受成功或未匹配预期，结果 JSON 不得超过 65536 字节。");
        }
        using var expected = JsonDocument.Parse(expectedResultsJson);
        var rows = expected.RootElement;
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > RuleResultRows.MaxRows ||
            (expectedStatus == RuleExecutionStatus.Succeeded ? rows.GetArrayLength() == 0 : rows.GetArrayLength() != 0) ||
            rows.EnumerateArray().Any(row => row.ValueKind != JsonValueKind.Object))
        {
            throw new ArgumentException("预期成功必须提供 1 到 100 个出口对象，未匹配预期必须提供空数组。");
        }
        ValidateUniqueProperties(rows);
        RevisionId = revisionId;
        Name = name.Trim();
        ContextJson = contextJson;
        InputsJson = inputsJson;
        ExpectedResultsJson = expectedResultsJson;
        ExpectedStatus = expectedStatus;
    }

    /// <summary>复制用例内容到新草稿，并更换实体与所属修订标识。</summary>
    internal ReleaseGateCase CopyTo(Guid revisionId)
    {
        return new ReleaseGateCase(Guid.NewGuid(), revisionId, Name, ContextJson, InputsJson, ExpectedResultsJson, ExpectedStatus);
    }

    private static void ValidateUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name))
                {
                    throw new ArgumentException("门禁用例数据包含重复字段。");
                }
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                ValidateUniqueProperties(item);
            }
        }
    }

    private static void ValidateObject(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("门禁用例数据必须是 JSON 对象。", name);
        }

        ValidateUniqueProperties(document.RootElement);
    }
}
