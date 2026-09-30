using NRuleEngine.Domain.Shared;
using NRuleEngine.Domain.Shared.Rules;
using Volo.Abp;

namespace NRuleEngine.Domain.Scenarios;

/// <summary>
/// 保存宿主代码注册的应用和场景契约，并在初始化后禁止修改。
/// </summary>
public sealed class RuleCatalog : IRuleCatalog, IRuleAppRegistry, IRuleScenarioRegistry, IConditionScenarioRegistry
{
    private readonly Dictionary<string, RuleAppDescriptor> _apps = new(StringComparer.Ordinal);
    private readonly Dictionary<(string App, string Scenario, string Version), RuleScenarioDescriptor> _scenarios = new();
    private readonly Dictionary<(string App, string Scenario, string Version), ConditionScenarioDescriptor> _conditionScenarios = new();
    private bool _initialized;

    /// <summary>
    /// 按注册顺序装配应用与场景；装配完成后目录保持只读。
    /// </summary>
    public void Initialize(IEnumerable<IRuleAppContributor> appContributors, IEnumerable<IRuleScenarioContributor> scenarioContributors,
        IEnumerable<IConditionScenarioContributor>? conditionContributors = null)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Rule catalog is already initialized.");
        }

        foreach (var contributor in appContributors)
        {
            contributor.Configure(this);
        }

        foreach (var contributor in scenarioContributors)
        {
            contributor.Configure(this);
        }

        foreach (var contributor in conditionContributors ?? [])
        {
            contributor.Configure(this);
        }

        _initialized = true;
    }

    /// <summary>注册应用编码，并拒绝与已注册应用重复。</summary>
    public void Add(RuleAppDescriptor descriptor)
    {
        EnsureMutable();
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.AppCode) ||
            string.IsNullOrWhiteSpace(descriptor.Name) || string.IsNullOrWhiteSpace(descriptor.Provider))
        {
            throw new ArgumentException("App descriptor requires code, name and provider.");
        }

        if (!_apps.TryAdd(descriptor.AppCode, descriptor))
        {
            throw new InvalidOperationException($"Duplicate AppCode: {descriptor.AppCode}.");
        }
    }

    /// <summary>校验并保存指定版本的场景契约快照。</summary>
    public void Add(RuleScenarioDescriptor descriptor)
    {
        EnsureMutable();
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.AppCode) ||
            string.IsNullOrWhiteSpace(descriptor.ScenarioCode) || string.IsNullOrWhiteSpace(descriptor.ContractVersion))
        {
            throw new ArgumentException("Scenario requires app, code and contract version.");
        }

        if (!_apps.ContainsKey(descriptor.AppCode))
        {
            throw new InvalidOperationException(
                $"Scenario {descriptor.ScenarioCode} references unregistered AppCode {descriptor.AppCode}.");
        }

        var variables = descriptor.Inputs.Concat(descriptor.RequiredOutputs).ToArray();
        if (variables.Any(v => string.IsNullOrWhiteSpace(v.Code) || string.IsNullOrWhiteSpace(v.Name)) ||
            variables.Select(v => v.Code).Distinct(StringComparer.Ordinal).Count() != variables.Length ||
            descriptor.Inputs.Any(v => v.Direction != RuleVariableDirection.Input) ||
            descriptor.RequiredOutputs.Any(v => v.Direction != RuleVariableDirection.Output) ||
            descriptor.RequiredOutputs.Count == 0)
        {
            throw new ArgumentException($"Scenario {descriptor.ScenarioCode} has invalid variable contracts.");
        }

        foreach (var variable in variables)
        {
            ValidateType(variable.Type);
        }

        if (descriptor.BindingDimensions.Any(d => string.IsNullOrWhiteSpace(d.Code) ||
            string.IsNullOrWhiteSpace(d.Name) || !Enum.IsDefined(d.ValueKind) ||
            d.AllowedOperators.Count == 0 || d.AllowedOperators.Any(op => !Enum.IsDefined(op))) ||
            descriptor.BindingDimensions.Select(d => d.Code).Distinct(StringComparer.Ordinal).Count() != descriptor.BindingDimensions.Count)
        {
            throw new ArgumentException($"Scenario {descriptor.ScenarioCode} has invalid binding dimensions.");
        }

        // 保存快照，避免注册方在初始化后通过原始集合修改场景契约。
        var copy = descriptor with
        {
            Inputs = Array.AsReadOnly(descriptor.Inputs.ToArray()),
            RequiredOutputs = Array.AsReadOnly(descriptor.RequiredOutputs.ToArray()),
            BindingDimensions = Array.AsReadOnly(descriptor.BindingDimensions
                .Select(d => d with { AllowedOperators = Array.AsReadOnly(d.AllowedOperators.ToArray()) }).ToArray())
        };
        if (!_scenarios.TryAdd((descriptor.AppCode, descriptor.ScenarioCode, descriptor.ContractVersion), copy))
        {
            throw new InvalidOperationException(
                $"Duplicate scenario contract: {descriptor.AppCode}/{descriptor.ScenarioCode}/{descriptor.ContractVersion}.");
        }
    }

    /// <summary>按应用编码返回只读应用目录。</summary>
    public IReadOnlyList<RuleAppDescriptor> GetApps()
    {
        return _apps.Values.OrderBy(x => x.AppCode).ToArray();
    }

    /// <summary>查询某应用的全部场景契约版本。</summary>
    public IReadOnlyList<RuleScenarioDescriptor> GetScenarios(string appCode)
    {
        if (!_apps.ContainsKey(appCode))
        {
            throw new BusinessException(NRuleEngineErrorCodes.AppNotRegistered).WithData("AppCode", appCode);
        }

        return _scenarios.Values.Where(x => x.AppCode == appCode)
            .OrderBy(x => x.ScenarioCode).ThenBy(x => x.ContractVersion).ToArray();
    }

    /// <summary>按应用、场景和精确版本读取契约，缺失时给出业务错误码。</summary>
    public RuleScenarioDescriptor GetScenario(string appCode, string scenarioCode, string contractVersion)
    {
        if (!_apps.ContainsKey(appCode))
        {
            throw new BusinessException(NRuleEngineErrorCodes.AppNotRegistered).WithData("AppCode", appCode);
        }

        if (_scenarios.TryGetValue((appCode, scenarioCode, contractVersion), out var descriptor))
        {
            return descriptor;
        }

        var code = _scenarios.Keys.Any(k => k.App == appCode && k.Scenario == scenarioCode)
            ? NRuleEngineErrorCodes.ContractVersionNotSupported
            : NRuleEngineErrorCodes.ScenarioNotRegistered;
        throw new BusinessException(code).WithData("AppCode", appCode)
            .WithData("ScenarioCode", scenarioCode).WithData("ContractVersion", contractVersion);
    }

    /// <summary>注册只声明入口变量的条件判定场景。</summary>
    public void Add(ConditionScenarioDescriptor descriptor)
    {
        EnsureMutable();
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.AppCode) ||
            string.IsNullOrWhiteSpace(descriptor.ScenarioCode) || string.IsNullOrWhiteSpace(descriptor.ContractVersion))
        {
            throw new ArgumentException("Condition scenario requires app, code and contract version.");
        }
        if (!_apps.ContainsKey(descriptor.AppCode))
        {
            throw new InvalidOperationException("Condition scenario references an unregistered app.");
        }
        if (descriptor.Inputs.Any(v => string.IsNullOrWhiteSpace(v.Code) || string.IsNullOrWhiteSpace(v.Name) ||
                                       v.Direction != RuleVariableDirection.Input) ||
            descriptor.Inputs.Select(v => v.Code).Distinct(StringComparer.Ordinal).Count() != descriptor.Inputs.Count)
        {
            throw new ArgumentException("Condition scenario has invalid input contracts.");
        }
        foreach (var input in descriptor.Inputs)
        {
            ValidateType(input.Type);
        }
        var copy = descriptor with { Inputs = Array.AsReadOnly(descriptor.Inputs.ToArray()) };
        if (!_conditionScenarios.TryAdd((descriptor.AppCode, descriptor.ScenarioCode, descriptor.ContractVersion), copy))
        {
            throw new InvalidOperationException("Duplicate condition scenario contract.");
        }
    }

    /// <summary>列出应用注册的条件判定场景。</summary>
    public IReadOnlyList<ConditionScenarioDescriptor> GetConditionScenarios(string appCode)
    {
        if (!_apps.ContainsKey(appCode))
        {
            throw new BusinessException(NRuleEngineErrorCodes.AppNotRegistered).WithData("AppCode", appCode);
        }
        return _conditionScenarios.Values.Where(x => x.AppCode == appCode)
            .OrderBy(x => x.ScenarioCode).ThenBy(x => x.ContractVersion).ToArray();
    }

    /// <summary>按固定版本查找条件判定场景。</summary>
    public ConditionScenarioDescriptor GetConditionScenario(string appCode, string scenarioCode, string contractVersion)
    {
        if (!_apps.ContainsKey(appCode))
        {
            throw new BusinessException(NRuleEngineErrorCodes.AppNotRegistered).WithData("AppCode", appCode);
        }
        if (_conditionScenarios.TryGetValue((appCode, scenarioCode, contractVersion), out var descriptor))
        {
            return descriptor;
        }
        var code = _conditionScenarios.Keys.Any(k => k.App == appCode && k.Scenario == scenarioCode)
            ? NRuleEngineErrorCodes.ContractVersionNotSupported : NRuleEngineErrorCodes.ScenarioNotRegistered;
        throw new BusinessException(code).WithData("AppCode", appCode)
            .WithData("ScenarioCode", scenarioCode).WithData("ContractVersion", contractVersion);
    }

    /// <summary>检查首版变量类型、Decimal 精度和 Enum 编码。</summary>
    internal static void ValidateType(RuleTypeDescriptor type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        if (!Enum.IsDefined(type.Kind))
        {
            throw new ArgumentException("Unsupported variable value kind.");
        }

        if (type.Kind == RuleValueKind.Decimal)
        {
            var precision = type.Precision ?? 28;
            var scale = type.Scale ?? 10;
            if (precision is < 1 or > 28 || scale < 0 || scale > precision)
            {
                throw new ArgumentException("Decimal precision must be 1..28 and scale must be 0..precision.");
            }
        }
        else if (type.Precision is not null || type.Scale is not null)
        {
            throw new ArgumentException("Only Decimal supports precision and scale.");
        }

        if (type.Kind == RuleValueKind.Enum && string.IsNullOrWhiteSpace(type.EnumCode))
        {
            throw new ArgumentException("Enum requires EnumCode.");
        }

        if (type.Kind != RuleValueKind.Enum && type.EnumCode is not null)
        {
            throw new ArgumentException("Only Enum supports EnumCode.");
        }
    }

    private void EnsureMutable()
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Rule catalog cannot change after initialization.");
        }
    }
}
