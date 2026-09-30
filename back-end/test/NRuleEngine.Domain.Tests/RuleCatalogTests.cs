using NRuleEngine.Domain.Scenarios;
using NRuleEngine.Domain.Shared;
using Volo.Abp;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class RuleCatalogTests
{
    [Fact]
    public void RegistersSimulatedSizeExpansionContract()
    {
        var catalog = new RuleCatalog();
        catalog.Initialize([new SimulatedRuleAppContributor()], [new SimulatedSizeExpansionContributor()]);

        var contract = catalog.GetScenario("TEST-ODS", "size-expansion", "1.0");
        Assert.Equal(2, contract.Inputs.Count);
        Assert.Single(contract.RequiredOutputs);
        Assert.Equal("productKind", Assert.Single(contract.BindingDimensions).Code);
        Assert.Throws<InvalidOperationException>(() => catalog.Add(SimulatedSizeExpansionScenario.App));
        var unsupported = Assert.Throws<BusinessException>(() =>
            catalog.GetScenario("TEST-ODS", "size-expansion", "2.0"));
        Assert.Equal(NRuleEngineErrorCodes.ContractVersionNotSupported, unsupported.Code);
    }

    [Fact]
    public void RejectsMissingAppAndDuplicateContracts()
    {
        var missingApp = new RuleCatalog();
        Assert.Throws<InvalidOperationException>(() =>
            missingApp.Add(SimulatedSizeExpansionScenario.Contract));

        var duplicate = new RuleCatalog();
        duplicate.Add(SimulatedSizeExpansionScenario.App);
        duplicate.Add(SimulatedSizeExpansionScenario.Contract);
        Assert.Throws<InvalidOperationException>(() =>
            duplicate.Add(SimulatedSizeExpansionScenario.Contract));
    }
}
