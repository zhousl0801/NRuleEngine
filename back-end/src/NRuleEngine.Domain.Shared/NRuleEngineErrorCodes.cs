namespace NRuleEngine.Domain.Shared;

/// <summary>
/// 规则引擎对外稳定的业务错误码。
/// </summary>
public static class NRuleEngineErrorCodes
{
    /// <summary>应用编码未注册在规则目录中。</summary>
    public const string AppNotRegistered = "NRuleEngine:Contract:AppNotRegistered";
    /// <summary>指定应用下的场景未注册。</summary>
    public const string ScenarioNotRegistered = "NRuleEngine:Contract:ScenarioNotRegistered";
    /// <summary>场景存在，但请求的契约版本不可用。</summary>
    public const string ContractVersionNotSupported = "NRuleEngine:Contract:ContractVersionNotSupported";
    /// <summary>同一应用下的模型编码重复。</summary>
    public const string DuplicateModelCode = "NRuleEngine:Model:DuplicateCode";
    /// <summary>同一应用下的条件集业务编码重复。</summary>
    public const string DuplicateConditionSetCode = "NRuleEngine:ConditionSet:DuplicateCode";
    /// <summary>请求的条件修订不存在或不属于目标条件集。</summary>
    public const string ConditionRevisionNotFound = "NRuleEngine:ConditionSet:RevisionNotFound";
    /// <summary>条件集发布样本缺失、非法或未通过。</summary>
    public const string ConditionGateFailed = "NRuleEngine:ConditionSet:GateFailed";
    /// <summary>候选集包含未知、未发布或不适用的条件集。</summary>
    public const string InvalidConditionCandidate = "NRuleEngine:ConditionSet:InvalidCandidate";
    /// <summary>指定修订不可用作新草稿的来源。</summary>
    public const string InvalidRevisionSource = "NRuleEngine:Revision:InvalidSource";
    /// <summary>请求的修订不存在。</summary>
    public const string RevisionNotFound = "NRuleEngine:Revision:NotFound";
    /// <summary>中间变量仍被草稿内容引用，不能删除。</summary>
    public const string VariableReferenced = "NRuleEngine:Variable:Referenced";
    /// <summary>修订中不存在指定稳定键的变量。</summary>
    public const string VariableNotFound = "NRuleEngine:Variable:NotFound";
    /// <summary>提交的并发戳已过期或缺失。</summary>
    public const string ConcurrencyConflict = "NRuleEngine:Concurrency:Conflict";
    /// <summary>修订未通过完整发布校验。</summary>
    public const string PublishValidationFailed = "NRuleEngine:Release:ValidationFailed";
    /// <summary>发布回归用例缺失或未通过。</summary>
    public const string ReleaseGateFailed = "NRuleEngine:Release:GateFailed";
    /// <summary>发布修订与其他活动模型的同优先级绑定可能同时命中。</summary>
    public const string AmbiguousActiveBinding = "NRuleEngine:Release:AmbiguousActiveBinding";
    /// <summary>发布或回滚请求指定了不合格的修订或模型状态。</summary>
    public const string InvalidReleaseTarget = "NRuleEngine:Release:InvalidTarget";
    /// <summary>场景尚无活动发布模型。</summary>
    public const string NoPublishedModel = "NRuleEngine:Binding:NoPublishedModel";
    /// <summary>上下文未匹配任何活动发布绑定。</summary>
    public const string NoMatchingBinding = "NRuleEngine:Binding:NoMatchingBinding";
    /// <summary>最高优先级出现多个同时命中的绑定。</summary>
    public const string AmbiguousBinding = "NRuleEngine:Binding:AmbiguousBinding";
    /// <summary>活动指针所指修订缺失或不是发布状态。</summary>
    public const string PublishedRevisionUnavailable = "NRuleEngine:Binding:PublishedRevisionUnavailable";
    /// <summary>请求的场景契约版本与活动修订不匹配。</summary>
    public const string ScenarioContractMismatch = "NRuleEngine:Contract:ScenarioContractMismatch";
    /// <summary>缺少场景绑定要求的上下文维度。</summary>
    public const string RequiredContextMissing = "NRuleEngine:Input:RequiredContextMissing";
    /// <summary>缺少场景契约声明的必需输入。</summary>
    public const string RequiredInputMissing = "NRuleEngine:Input:RequiredInputMissing";
    /// <summary>提供了场景契约之外的输入。</summary>
    public const string UnknownInput = "NRuleEngine:Input:UnknownInput";
    /// <summary>输入 JSON 值与声明类型不符。</summary>
    public const string InputTypeMismatch = "NRuleEngine:Input:InputTypeMismatch";
    /// <summary>输入值违反可空性或数值精度约束。</summary>
    public const string InputConstraintViolated = "NRuleEngine:Input:InputConstraintViolated";
    /// <summary>表达式或函数在运行中计算失败。</summary>
    public const string ExpressionEvaluationFailed = "NRuleEngine:Evaluation:ExpressionEvaluationFailed";
    /// <summary>已选择模型，但没有规则命中。</summary>
    public const string NoMatchingRule = "NRuleEngine:Evaluation:NoMatchingRule";
    /// <summary>结果行数或序列化大小超过限制。</summary>
    public const string ResultLimitExceeded = "NRuleEngine:Output:ResultLimitExceeded";
    /// <summary>不允许的重复变量赋值。</summary>
    public const string MultipleWriteConflict = "NRuleEngine:Evaluation:MultipleWriteConflict";
    /// <summary>必需出口未得到有效值。</summary>
    public const string RequiredOutputMissing = "NRuleEngine:Output:RequiredOutputMissing";
    /// <summary>发布内容与记录的内容哈希不一致。</summary>
    public const string PublishedContentCorrupted = "NRuleEngine:Integrity:PublishedContentCorrupted";
    /// <summary>存储层暂时不可用。</summary>
    public const string StorageUnavailable = "NRuleEngine:System:StorageUnavailable";
    /// <summary>引擎内部运行预算耗尽。</summary>
    public const string ExecutionTimeout = "NRuleEngine:System:Timeout";
    /// <summary>未预期的运行程序故障。</summary>
    public const string UnexpectedExecutionError = "NRuleEngine:System:Unexpected";
}
