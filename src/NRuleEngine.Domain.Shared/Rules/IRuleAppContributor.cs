namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>
/// 向目录贡献接入应用。
/// </summary>
public interface IRuleAppContributor
{
    /// <summary>向注册器提交本模块支持的接入应用。</summary>
    void Configure(IRuleAppRegistry registry);
}
