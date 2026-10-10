using Volo.Abp;

namespace NRuleEngine.Domain.Shared;

/// <summary>为规则引擎的稳定业务错误码提供默认中文异常说明。</summary>
public static class NRuleEngineBusinessExceptions
{
    /// <summary>创建保留原错误码和数据字段的中文业务异常。</summary>
    public static BusinessException Create(string code)
    {
        var message = code switch
        {
            NRuleEngineErrorCodes.AppNotRegistered => "规则应用尚未注册。",
            NRuleEngineErrorCodes.ScenarioNotRegistered => "规则场景尚未注册。",
            NRuleEngineErrorCodes.ContractVersionNotSupported => "不支持请求的场景契约版本。",
            NRuleEngineErrorCodes.DuplicateModelCode => "同一应用下的规则模型编码重复。",
            NRuleEngineErrorCodes.DuplicateConditionSetCode => "同一应用下的条件集编码重复。",
            NRuleEngineErrorCodes.ConditionRevisionNotFound => "条件集修订不存在。",
            NRuleEngineErrorCodes.ConditionGateFailed => "条件集发布门禁未通过。",
            NRuleEngineErrorCodes.InvalidConditionCandidate => "候选条件集无效。",
            NRuleEngineErrorCodes.InvalidRevisionSource => "指定修订不能作为新草稿的来源。",
            NRuleEngineErrorCodes.RevisionNotFound => "规则修订不存在。",
            NRuleEngineErrorCodes.VariableReferenced => "中间变量仍被草稿引用，不能删除。",
            NRuleEngineErrorCodes.VariableNotFound => "变量不存在。",
            NRuleEngineErrorCodes.ConcurrencyConflict => "并发戳已过期，请刷新后重试。",
            NRuleEngineErrorCodes.PublishValidationFailed => "规则修订未通过发布校验。",
            NRuleEngineErrorCodes.ReleaseGateFailed => "规则发布门禁未通过。",
            NRuleEngineErrorCodes.AmbiguousActiveBinding => "活动模型存在相同优先级的绑定冲突。",
            NRuleEngineErrorCodes.InvalidReleaseTarget => "发布或回滚目标不符合要求。",
            NRuleEngineErrorCodes.NoPublishedModel => "此场景没有活动的已发布模型。",
            NRuleEngineErrorCodes.NoMatchingBinding => "没有活动绑定匹配给定场景上下文。",
            NRuleEngineErrorCodes.AmbiguousBinding => "最高优先级存在多条命中的绑定。",
            NRuleEngineErrorCodes.PublishedRevisionUnavailable => "活动的已发布修订不可用。",
            NRuleEngineErrorCodes.ScenarioContractMismatch => "已发布修订与场景契约不匹配。",
            NRuleEngineErrorCodes.RequiredContextMissing => "缺少必需的场景维度。",
            NRuleEngineErrorCodes.RequiredInputMissing => "缺少必需的输入变量。",
            NRuleEngineErrorCodes.UnknownInput => "提供了未声明的输入变量。",
            NRuleEngineErrorCodes.InputTypeMismatch => "输入值类型不正确。",
            NRuleEngineErrorCodes.InputConstraintViolated => "输入值不符合声明的约束。",
            NRuleEngineErrorCodes.ExpressionEvaluationFailed => "规则求值失败。",
            NRuleEngineErrorCodes.MultipleWriteConflict => "同一变量被重复赋值。",
            NRuleEngineErrorCodes.RequiredOutputMissing => "未生成必需的输出变量。",
            NRuleEngineErrorCodes.PublishedContentCorrupted => "已发布规则内容校验失败。",
            NRuleEngineErrorCodes.StorageUnavailable => "规则存储暂时不可用。",
            NRuleEngineErrorCodes.ExecutionTimeout => "规则执行超过允许时间。",
            NRuleEngineErrorCodes.UnexpectedExecutionError => "规则执行发生未预期错误。",
            _ => "规则引擎业务操作失败。"
        };
        return new BusinessException(code, message);
    }
}
