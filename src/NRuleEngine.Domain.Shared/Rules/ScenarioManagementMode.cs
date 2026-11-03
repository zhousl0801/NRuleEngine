namespace NRuleEngine.Domain.Shared.Rules;

/// <summary>场景数据的维护入口归属。</summary>
public enum ScenarioManagementMode
{
    /// <summary>由通用规则引擎工作台维护。</summary>
    Workbench,
    /// <summary>由业务系统其他入口维护，通用工作台列表默认不展示。</summary>
    External
}
