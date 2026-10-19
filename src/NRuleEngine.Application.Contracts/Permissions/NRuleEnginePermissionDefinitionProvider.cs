using Volo.Abp.Authorization.Permissions;

namespace NRuleEngine.Application.Contracts.Permissions;

/// <summary>
/// 定义规则引擎的应用权限。
/// </summary>
public sealed class NRuleEnginePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(NRuleEnginePermissions.Group);
        group.AddPermission(NRuleEnginePermissions.Manage);
        group.AddPermission(NRuleEnginePermissions.Publish);
        group.AddPermission(NRuleEnginePermissions.Execute);
    }
}
