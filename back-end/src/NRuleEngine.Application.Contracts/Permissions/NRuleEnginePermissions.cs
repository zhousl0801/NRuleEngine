namespace NRuleEngine.Application.Contracts.Permissions;

/// <summary>
/// 规则引擎管理、发布和执行权限名称。
/// </summary>
public static class NRuleEnginePermissions
{
    public const string Group = "NRuleEngine";
    public const string Manage = Group + ".Manage";
    public const string Publish = Group + ".Publish";
    public const string Execute = Group + ".Execute";
}
