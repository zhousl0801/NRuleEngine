using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NRuleEngine.Application.Contracts;
using NRuleEngine.HttpApi.Json;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace NRuleEngine.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(NRuleEngineApplicationContractsModule))]
public sealed class NRuleEngineHttpApiModule : AbpModule
{
    /// <summary>注册只作用于 NRuleEngine 枚举的字符串序列化，并放到 ABP 兼容转换器之前。</summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<JsonOptions>().Configure(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new NRuleEngineStringEnumConverterFactory());
        });
        context.Services.PostConfigure<JsonOptions>(options =>
        {
            var converter = options.JsonSerializerOptions.Converters
                .OfType<NRuleEngineStringEnumConverterFactory>()
                .FirstOrDefault();
            if (converter is not null)
            {
                options.JsonSerializerOptions.Converters.Remove(converter);
                options.JsonSerializerOptions.Converters.Insert(0, converter);
            }
        });
    }
}
