using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NRuleEngine.HttpApi.Json;

/// <summary>只把 NRuleEngine 枚举按名称输出，避免改变宿主其他模块的协议。</summary>
/// <remarks>核心逻辑：按类型命名空间选择转换器并保留整数输入兼容；调用时机：MVC 读写 JSON 时。</remarks>
public sealed class NRuleEngineStringEnumConverterFactory : JsonConverterFactory
{
    private static readonly JsonStringEnumConverter InnerConverter = new(namingPolicy: null, allowIntegerValues: true);

    /// <summary>判断类型是否属于 NRuleEngine 且为枚举。</summary>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum && typeToConvert.Namespace?.StartsWith("NRuleEngine", StringComparison.Ordinal) == true;
    }

    /// <summary>为具体枚举创建系统自带的字符串转换器。</summary>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return InnerConverter.CreateConverter(typeToConvert, options);
    }
}
