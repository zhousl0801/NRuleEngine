# NRuleEngine 运行错误与异常模型

> 状态：已确认  
> 版本：v0.3  
> 日期：2026-09-28  
> 范围：运行时执行与首版试算

## 1. 设计目标

- 调用方可以仅根据稳定错误码决定处理方式，不解析消息文本。
- 无匹配、输入不合法、表达式失败与基础设施故障具有不同语义。
- 错误信息能够定位 App、场景、模型、修订、规则、变量和表达式位置。
- 默认消息不包含输入值、完整表达式、连接信息或其他敏感内容。
- ODS 可以把引擎错误转换为自己的业务异常，但不能丢失原始错误码和执行标识。

## 2. 两条错误通道

### 2.1 可预期执行结果

规则执行已经正常进入 NRuleEngine，但因为业务上下文、输入或规则内容无法产生结果时，`ExecuteAsync` 返回 `RuleExecutionResultDto`，不使用异常控制业务分支。请求与输出值均以原生 JSON 值传输。

```csharp
public enum RuleExecutionStatus
{
    Succeeded,
    NotMatched,
    Rejected,
    Failed
}

public class RuleExecutionResultDto
{
    public Guid ExecutionId { get; set; }
    public RuleExecutionStatus Status { get; set; }
    public Guid? RuleModelId { get; set; }
    public Guid? RevisionId { get; set; }
    public Guid? BindingId { get; set; }
    public Dictionary<string, JsonElement> Outputs { get; set; } = [];
    public RuleExecutionErrorDto? Error { get; set; }
}

public class RuleExecutionErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object?> Data { get; set; } = [];
}
```

状态含义：

| 状态 | 含义 | 是否建议原请求重试 |
|---|---|---|
| Succeeded | 已选出唯一模型并得到合法输出 | 不需要 |
| NotMatched | 没有任何绑定匹配业务上下文 | 否；除非上下文发生变化 |
| Rejected | 请求、契约或配置不满足执行前提 | 否；修正输入或配置后再执行 |
| Failed | 已进入规则计算，但表达式、函数或输出计算失败 | 否；修正规则或数据后再执行 |

运行 API 对上述四种确定性结果均返回完整 `RuleExecutionResult`。HTTP 层统一返回 200，由 `Status` 和 `Error.Code` 表达规则执行结果；认证、授权、请求 JSON 无法解析等 HTTP 层错误仍使用标准 HTTP/ABP 错误响应。

### 2.2 不可预期系统异常

数据库不可用、序列化组件故障、程序缺陷等不属于规则结果，继续通过异常通道处理：

- 领域和应用层使用带稳定错误码的 `BusinessException` 包装已知系统边界错误。
- 调用方传入的 `CancellationToken` 取消时，`OperationCanceledException` 原样传播，不包装成业务失败；HTTP 连接已断开时不保证还能返回响应。
- NRuleEngine 同时应用自身配置的执行时间预算；预算耗尽时抛出 `NRuleEngine:System:Timeout`。
- 未知异常由执行应用服务记录后包装为 `NRuleEngine:System:Unexpected`，客户端只获得安全消息和 `ExecutionId`。
- 日志保存原始异常堆栈，并通过结构化字段记录定位信息。

## 3. 错误码规范

格式：

```text
NRuleEngine:<Category>:<Name>
```

例如：

```text
NRuleEngine:Binding:NoMatchingBinding
NRuleEngine:Input:RequiredInputMissing
NRuleEngine:Evaluation:ExpressionEvaluationFailed
```

约束：

- 错误码发布后不可改名、不可复用为其他语义。
- 调用方只依赖 Code；Message 可以本地化和调整措辞。
- 错误码定义在 `NRuleEngine.Domain.Shared` 的 `NRuleEngineErrorCodes`。
- 本地化资源键直接使用完整错误码。
- `Data` 的字段名使用 PascalCase，值必须是可安全序列化的标量或有上限的标量集合。

## 4. 运行错误目录

### 4.1 App、场景和版本

| Code | Status | 默认中文信息 | Data |
|---|---|---|---|
| `NRuleEngine:Contract:AppNotRegistered` | Rejected | 未注册规则应用 `{AppCode}`。 | AppCode |
| `NRuleEngine:Contract:ScenarioNotRegistered` | Rejected | 应用 `{AppCode}` 未注册场景 `{ScenarioCode}`。 | AppCode, ScenarioCode |
| `NRuleEngine:Contract:ContractVersionNotSupported` | Rejected | 场景 `{ScenarioCode}` 不支持契约版本 `{ContractVersion}`。 | AppCode, ScenarioCode, ContractVersion, SupportedVersions |
| `NRuleEngine:Contract:ScenarioContractMismatch` | Rejected | 已发布规则使用的场景契约与当前注册契约不兼容。 | AppCode, ScenarioCode, ContractVersion, RevisionId |

### 4.2 模型和绑定选择

| Code | Status | 默认中文信息 | Data |
|---|---|---|---|
| `NRuleEngine:Binding:NoPublishedModel` | Rejected | 场景 `{ScenarioCode}` 没有可执行的已发布规则模型。 | AppCode, ScenarioCode |
| `NRuleEngine:Binding:NoMatchingBinding` | NotMatched | 当前业务上下文未匹配到规则模型。 | AppCode, ScenarioCode, ContextKeys |
| `NRuleEngine:Binding:AmbiguousBinding` | Rejected | 当前业务上下文同时匹配多个相同最高优先级的规则模型。 | AppCode, ScenarioCode, Priority, CandidateModelIds, CandidateBindingIds |
| `NRuleEngine:Binding:PublishedRevisionUnavailable` | Rejected | 规则模型的活动发布修订不可用。 | RuleModelId, RevisionId |

说明：

- `NoPublishedModel` 表示场景没有任何活动发布修订，是配置缺失。
- `NoMatchingBinding` 表示存在活动模型，但当前上下文没有命中，是允许宿主处理的业务结果。
- `AmbiguousBinding` 是配置冲突，禁止自动选择任意模型。

### 4.3 上下文和输入

| Code | Status | 默认中文信息 | Data |
|---|---|---|---|
| `NRuleEngine:Input:RequiredContextMissing` | Rejected | 缺少必需场景上下文 `{DimensionCode}`。 | DimensionCode |
| `NRuleEngine:Input:RequiredInputMissing` | Rejected | 缺少必需输入变量 `{VariableCode}`。 | VariableCode, VariableKey |
| `NRuleEngine:Input:UnknownInput` | Rejected | 输入变量 `{VariableCode}` 未在场景契约中定义。 | VariableCode |
| `NRuleEngine:Input:InputTypeMismatch` | Rejected | 输入变量 `{VariableCode}` 的类型不正确，期望 `{ExpectedType}`，实际为 `{ActualType}`。 | VariableCode, VariableKey, ExpectedType, ActualType |
| `NRuleEngine:Input:InputConstraintViolated` | Rejected | 输入变量 `{VariableCode}` 不满足约束 `{Constraint}`。 | VariableCode, VariableKey, Constraint |

首版采用严格输入：未知输入返回错误，不静默忽略。业务扩展信息应放入 `Context` 中已注册的维度，而不是混入 `Inputs`。

### 4.4 表达式、变量和函数

| Code | Status | 默认中文信息 | Data |
|---|---|---|---|
| `NRuleEngine:Evaluation:ExpressionCompileFailed` | Rejected | 表达式无法编译。 | RevisionId, RuleCode, SourceStart, SourceLength, Diagnostic |
| `NRuleEngine:Evaluation:CircularDependencyDetected` | Rejected | 变量依赖存在循环：`{DependencyPath}`。 | RevisionId, DependencyPath |
| `NRuleEngine:Evaluation:ExpressionEvaluationFailed` | Failed | 规则 `{RuleCode}` 的表达式计算失败。 | RevisionId, RuleCode, VariableCode, SourceStart, SourceLength, FailureKind |
| `NRuleEngine:Evaluation:VariableNotAssigned` | Failed | 变量 `{VariableCode}` 在读取前尚未赋值。 | RevisionId, RuleCode, VariableCode, VariableKey |
| `NRuleEngine:Evaluation:FunctionNotRegistered` | Rejected | 表达式函数 `{FunctionName}` 未注册或版本不兼容。 | FunctionName, RevisionId |
| `NRuleEngine:Evaluation:FunctionExecutionFailed` | Failed | 表达式函数 `{FunctionName}` 执行失败。 | FunctionName, RevisionId, RuleCode, FailureKind |
| `NRuleEngine:Evaluation:MultipleWriteConflict` | Failed | 变量 `{VariableCode}` 发生不允许的重复赋值。 | VariableCode, VariableKey, FirstRuleCode, CurrentRuleCode, Policy |
| `NRuleEngine:Evaluation:ExecutionLimitExceeded` | Failed | 规则执行超过允许的资源限制。 | LimitName, LimitValue, RevisionId |

其中 `ExpressionCompileFailed` 和 `CircularDependencyDetected` 主要出现在草稿试算；正常发布流程应在发布前阻止它们进入生产运行。

`FailureKind` 用于进一步分类，例如 `DivideByZero`、`Overflow`、`NullOperand`、`InvalidOperation`。它不是独立稳定错误码，调用方的主分支仍依据 `Error.Code`。

### 4.5 输出

| Code | Status | 默认中文信息 | Data |
|---|---|---|---|
| `NRuleEngine:Output:RequiredOutputMissing` | Failed | 必需输出变量 `{VariableCode}` 未产生结果。 | VariableCode, VariableKey, RevisionId |
| `NRuleEngine:Output:OutputTypeMismatch` | Failed | 输出变量 `{VariableCode}` 的类型不正确，期望 `{ExpectedType}`，实际为 `{ActualType}`。 | VariableCode, VariableKey, ExpectedType, ActualType, RevisionId |
| `NRuleEngine:Output:OutputConversionFailed` | Failed | 输出变量 `{VariableCode}` 无法转换为场景契约类型。 | VariableCode, VariableKey, TargetType, RevisionId |

必需出口由场景契约定义。任一必需出口缺失或类型错误时，本次执行整体失败，不返回部分成功结果。

### 4.6 发布内容和系统故障

| Code | 通道 | 默认中文信息 | Data |
|---|---|---|---|
| `NRuleEngine:Integrity:PublishedContentCorrupted` | Exception | 已发布规则内容校验失败。 | RuleModelId, RevisionId, ExpectedHash, ActualHash, ExecutionId |
| `NRuleEngine:System:StorageUnavailable` | Exception | 规则存储暂时不可用。 | ExecutionId, Operation |
| `NRuleEngine:System:Timeout` | Exception | 规则执行超过允许时间。 | ExecutionId, TimeoutMilliseconds |
| `NRuleEngine:System:Unexpected` | Exception | 规则执行发生未预期错误。 | ExecutionId |

系统异常消息不得包含数据库地址、SQL、堆栈、完整输入、表达式全文或领域实体序列化内容。

## 5. 首版试算错误

试算复用与生产执行相同的输入校验、表达式编译和计算路径，但不进行发布模型选择：调用方明确指定草稿 `RevisionId`。

首版试算返回：

- `Succeeded`：输入、表达式和必需输出均正确。
- `Rejected`：输入、表达式编译、循环依赖或函数注册错误。
- `Failed`：表达式计算、变量赋值或最终输出错误。

试算不落库，只在响应中返回：

- 命中规则编码。
- 最终输出。
- 一个主要错误和可选诊断列表。
- 表达式错误字符范围。

首版不要求保存逐节点计算值、执行时间线或历史试算记录。

## 6. 异常数据与定位字段

每次执行生成 `ExecutionId`。日志、API 结果和 ODS 转换后的异常都应保留该值。

推荐公共字段：

| 字段 | 用途 |
|---|---|
| ExecutionId | 串联调用结果和服务端日志 |
| AppCode、ScenarioCode、ContractVersion | 定位场景契约 |
| RuleModelId、RevisionId | 定位实际执行内容 |
| BindingId、Priority | 定位模型选择 |
| RuleCode | 定位命中或失败规则 |
| VariableCode、VariableKey | 定位输入、赋值或输出 |
| SourceStart、SourceLength | 定位表达式字符范围 |
| FailureKind | 提供错误子类型，不作为稳定分支码 |

禁止放入客户端 Error.Data：

- 完整输入和输出对象。
- 变量真实值。
- 完整表达式源文本。
- 异常堆栈、SQL、连接串和内部文件路径。

## 7. ABP 实现约定

错误码定义示例：

```csharp
public static class NRuleEngineErrorCodes
{
    public static class Binding
    {
        public const string NoPublishedModel = "NRuleEngine:Binding:NoPublishedModel";
        public const string NoMatchingBinding = "NRuleEngine:Binding:NoMatchingBinding";
        public const string AmbiguousBinding = "NRuleEngine:Binding:AmbiguousBinding";
    }

    public static class Input
    {
        public const string RequiredInputMissing = "NRuleEngine:Input:RequiredInputMissing";
        public const string InputTypeMismatch = "NRuleEngine:Input:InputTypeMismatch";
    }
}
```

- 常量放在 `NRuleEngine.Domain.Shared`。
- 中文和英文消息放在 NRuleEngine 本地化资源中。
- 管理类 API 的可预期业务限制使用 `BusinessException(code).WithData(...)`。
- 运行 API 的确定性失败填入 `RuleExecutionResult.Error`，不先抛出再捕获转换。
- HTTP Controller 不包含错误判断逻辑，只负责传递应用服务结果。

### 7.1 超时与调用方取消

执行服务同时监听两个取消来源：

1. 调用方传入的 `CancellationToken`。
2. NRuleEngine 根据宿主配置创建的执行预算 CancellationToken。

实现时使用 linked token 传递给绑定查询、表达式函数和其他异步操作。捕获取消后必须辨别来源：

- 调用方 Token 已取消：继续抛出 `OperationCanceledException`。
- 仅内部预算 Token 已取消：转换为 `NRuleEngine:System:Timeout`，并携带 `ExecutionId` 和 `TimeoutMilliseconds`。
- 两者几乎同时取消：优先按调用方取消处理，避免把已经终止的请求误报为引擎故障。

具体默认超时时长在性能预算设计时确定，宿主可以配置，但不能由单次规则输入任意放大。

## 8. ODS 适配建议

| NRuleEngine 错误 | ODS 建议处理 |
|---|---|
| NoMatchingBinding | 由 ODS 决定返回业务错误、空结果或执行显式配置的兜底流程 |
| AmbiguousBinding | 视为规则配置错误；停止当前设计流程并告警，不重试相同请求 |
| RequiredInputMissing / InputTypeMismatch | 视为 ODS 输入映射或订单数据错误；修正数据后重试 |
| ExpressionEvaluationFailed / FunctionExecutionFailed | 视为已发布规则缺陷；记录模型和修订并告警 |
| RequiredOutputMissing / OutputTypeMismatch | 视为契约违约；停止使用本次结果，不接受部分输出 |
| StorageUnavailable / Timeout | 由 ODS 按业务流程的幂等性决定是否有限次重试 |
| Unexpected | 记录 ExecutionId 并进入通用系统异常处理 |

ODS 转换异常时应保留：`EngineErrorCode`、`ExecutionId`、`RuleModelId` 和 `RevisionId`，便于跨模块定位。NRuleEngine 不自动回退到旧修订；任何兜底必须由 ODS 显式决定。

## 9. 验收标准

- 所有稳定错误码均定义为 Domain.Shared 常量并有本地化消息。
- 调用方不解析 Message 即可根据 Code 完成处理分支。
- NoMatchingBinding、AmbiguousBinding、输入错误、表达式错误和输出错误均有独立测试。
- 试算与生产执行对同一输入和表达式使用相同的类型检查及计算逻辑。
- 任一必需输出缺失时不返回部分成功。
- 客户端错误数据不包含变量值、表达式全文和内部异常细节。
- 未知异常对客户端统一表现为 `NRuleEngine:System:Unexpected`，日志保留原始异常。

## 10. 已确认的协议选择

1. `NotMatched / Rejected / Failed` 属于引擎已处理的确定性结果，HTTP 统一返回 200。
2. 场景契约未声明的额外 `Inputs` 返回 `NRuleEngine:Input:UnknownInput`，不静默忽略。
3. NRuleEngine 同时响应内部执行时间预算和调用方 `CancellationToken`，并区分两种取消来源。
