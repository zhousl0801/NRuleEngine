using NRuleEngine.Domain.Scenarios;
using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class RuleCatalogTests
{
    [Fact]
    public void RegistersSimulatedSizeExpansionContract()
    {
        // 读取注册目录并核对应用、场景和版本的准确关联。
        var catalog = new RuleCatalog();
        catalog.Initialize([new SimulatedRuleAppContributor()], [new SimulatedSizeExpansionContributor()]);

        var contract = catalog.GetScenario("TEST-ODS", "size-expansion", "1.0");
        Assert.Equal(ScenarioManagementMode.Workbench, contract.ManagementMode);
        Assert.Equal(2, contract.Inputs.Count);
        Assert.Single(contract.RequiredOutputs);
        Assert.Equal("productKind", Assert.Single(contract.BindingDimensions).Code);
        var initialized = Assert.Throws<InvalidOperationException>(() => catalog.Add(SimulatedSizeExpansionScenario.App));
        Assert.Contains("规则目录初始化后不能修改", initialized.Message);
        var unsupported = Assert.Throws<BusinessException>(() =>
            catalog.GetScenario("TEST-ODS", "size-expansion", "2.0"));
        Assert.Equal(NRuleEngineErrorCodes.ContractVersionNotSupported, unsupported.Code);
        Assert.Contains("不支持请求的场景契约版本", unsupported.Message);
    }

    [Fact]
    public void RejectsMissingAppAndDuplicateContracts()
    {
        // 构造缺失应用与重复契约，确认启动注册阶段阻止无效目录。
        var missingApp = new RuleCatalog();
        Assert.Throws<InvalidOperationException>(() =>
            missingApp.Add(SimulatedSizeExpansionScenario.Contract));

        var duplicate = new RuleCatalog();
        duplicate.Add(SimulatedSizeExpansionScenario.App);
        duplicate.Add(SimulatedSizeExpansionScenario.Contract);
        Assert.Throws<InvalidOperationException>(() =>
            duplicate.Add(SimulatedSizeExpansionScenario.Contract));
    }

    [Fact]
    public void EnforcesManagementModeConsistencyAcrossContractVersions()
    {
        // 同一场景的不同契约版本必须共用维护模式，避免工作台列表按版本产生歧义。
        var catalog = new RuleCatalog();
        catalog.Add(SimulatedSizeExpansionScenario.App);
        catalog.Add(SimulatedSizeExpansionScenario.Contract);

        var externalRuleScenario = SimulatedSizeExpansionScenario.Contract with
        {
            ContractVersion = "2.0",
            ManagementMode = ScenarioManagementMode.External
        };
        Assert.Throws<InvalidOperationException>(() => catalog.Add(externalRuleScenario));

        var conditionScenario = new ConditionScenarioDescriptor("TEST-ODS", "thickness-eligibility", "1.0",
            [new VariableContract("thickness", "Thickness", RuleVariableDirection.Input, new RuleTypeDescriptor(RuleValueKind.Decimal))]);
        catalog.Add(conditionScenario);
        var externalConditionScenario = conditionScenario with
        {
            ContractVersion = "2.0",
            ManagementMode = ScenarioManagementMode.External
        };
        Assert.Throws<InvalidOperationException>(() => catalog.Add(externalConditionScenario));
    }
}
