# NRuleEngine API 参考

本页按当前 [HttpApi 控制器](../../src/NRuleEngine.HttpApi/) 整理。请求字段、必填项和长度限制以 [Application.Contracts](../../src/NRuleEngine.Application.Contracts/) 为准。基础路径为 `/api/nrule-engine`；下表路径均相对于它。M、P、E 分别表示 NRuleEngine.Manage、Publish、Execute；“认证”表示目录端点仅要求认证。

## 1. 应用与场景目录

| 方法与路径 | 用途 | 权限 |
| --- | --- | --- |
| GET `/catalog/apps` | 全部注册应用 | 认证 |
| GET `/catalog/apps/{appCode}/scenarios` | 赋值场景的全部契约版本 | 认证 |
| GET `/catalog/apps/{appCode}/scenarios/{scenarioCode}/versions/{contractVersion}` | 精确赋值契约 | 认证 |
| GET `/catalog/apps/{appCode}/condition-scenarios` | 条件场景的全部契约版本 | 认证 |
| GET `/catalog/apps/{appCode}/condition-scenarios/{scenarioCode}/versions/{contractVersion}` | 精确条件契约 | 认证 |

App 与场景不提供写入接口；目录不提供动态业务候选值查询。

## 2. 模型、修订与变量

| 方法与路径 | 用途 | 权限 |
| --- | --- | --- |
| POST `/models` | 创建模型及首个草稿 | M |
| GET `/models?appCode=...&scenarioCode=...` | 按应用、可选场景查询，返回列表而非分页对象 | M |
| GET `/models/{modelId}` | 模型详情与活动指针 | M |
| POST `/models/{sourceModelId}/copy` | 从指定修订复制独立模型 | M |
| POST `/models/{modelId}/archive` | 归档，携带模型并发戳 | M |
| GET `/models/{modelId}/archive-impact` | 归档影响 | M |
| POST `/models/{modelId}/drafts` | 创建草稿，可指定来源修订 | M |
| GET `/models/{modelId}/revisions` | 修订摘要列表 | M |
| GET `/models/revisions/{revisionId}` | 完整修订、变量、规则、绑定和门禁 | M |
| POST `/models/revisions/{revisionId}/intermediate-variables` | 新增中间变量 | M |
| PUT `/models/revisions/{revisionId}/intermediate-variables/{stableKey}` | 修改中间变量编码与名称 | M |
| PUT `/models/revisions/{revisionId}/inputs/{stableKey}/default` | 设置或清除输入默认值 | M |
| PUT `/models/revisions/{revisionId}/intermediate-variables/{stableKey}/properties` | 默认值与单位 | M |
| PUT `/models/revisions/{revisionId}/intermediate-variables/{stableKey}/precision` | Decimal 精度及小数位 | M |
| GET `/models/revisions/{revisionId}/variables/{stableKey}/references` | 直接引用及下游影响 | M |
| DELETE `/models/revisions/{revisionId}/intermediate-variables/{stableKey}` | 删除无引用中间变量 | M |
| PUT `/models/revisions/{revisionId}/execution-semantics` | 设置执行模式及重复写入策略 | M |

除创建外，草稿写入通常携带 ExpectedRevisionConcurrencyStamp，模型操作携带 ExpectedModelConcurrencyStamp；以具体 DTO 为准。DELETE 草稿子对象也使用请求体传并发戳。成功后使用响应中的新戳，不从旧请求继续写入。

## 3. 规则、赋值、绑定与验证

本节令 `R = /models/revisions/{revisionId}`，只是表格缩写。

| 方法与路径 | 用途 | 权限 |
| --- | --- | --- |
| POST `R/rules` | 新增规则 | M |
| PUT / DELETE `R/rules/{ruleId}` | 修改/删除规则 | M |
| GET `R/rules/{ruleId}/removal-impact` | 规则删除影响 | M |
| POST `R/rules/{ruleId}/assignments` | 新增赋值 | M |
| PUT / DELETE `R/rules/{ruleId}/assignments/{assignmentId}` | 修改/删除赋值 | M |
| GET `R/rules/{ruleId}/assignments/{assignmentId}/removal-impact` | 赋值删除影响 | M |
| POST `R/bindings` | 新增绑定 | M |
| PUT / DELETE `R/bindings/{bindingId}` | 修改/删除绑定 | M |
| GET `R/validation` | 完整校验报告 | M |
| POST `/models/revisions/simulate` | 指定修订试算 | M |

影响查询包含当前并发戳，不产生锁定。赋值和条件使用 AST JSON 文本，格式见[表达式参考](expressions.md)。试算请求中的 ContextJson/InputsJson 是“字段编码 → JSON 值文本”的字典，字符串值需要额外 JSON 编码：

```json
{
  "revisionId": "11111111-1111-1111-1111-111111111111",
  "contextJson": {"productKind": "\"A\""},
  "inputsJson": {"inputWidth": "120"}
}
```

试算返回 Status、Success、Conditions、Assignments、Results，以及 ErrorCode、ErrorPath、Start/Length 等诊断字段。ErrorCode 使用内部诊断码，例如 NoMatchingRule、MissingInput、AssignmentFailed，不等同于运行接口的完整稳定码。试算只在指定修订内选绑定，不替代全场景模型裁决，也不保存 Trace。

## 4. 门禁、发布与回滚

| 方法与路径 | 用途 | 权限 |
| --- | --- | --- |
| POST `/models/revisions/{revisionId}/gate-cases` | 新增门禁用例 | M |
| DELETE `/models/revisions/{revisionId}/gate-cases/{gateCaseId}` | 删除门禁用例 | M |
| POST `/models/revisions/{revisionId}/publish` | 完整校验、门禁及跨模型检查后发布 | P |
| POST `/models/{modelId}/rollback` | 重新激活历史发布修订 | P |
| GET `/models/{modelId}/releases` | 发布及回滚历史 | P |

门禁的 ContextJson、InputsJson、ExpectedResultsJson 均为完整 JSON 文本字符串，与试算的字典格式不同。ExpectedResultsJson 是有序出口对象数组，不含 outputs 包装或来源规则 ID：

```json
{
  "name": "正常宽度",
  "contextJson": "{\"productKind\":\"A\"}",
  "inputsJson": "{\"inputWidth\":120}",
  "expectedResultsJson": "[{\"targetWidth\":125}]",
  "expectedRevisionConcurrencyStamp": "读取修订获得的当前戳"
}
```

省略 ExpectedStatus 时默认 Succeeded；也可使用 NotMatched，预期结果必须为 `[]`。成功样本要求 1–100 行；发布至少一条成功样本。失败状态不可作为发布放行预期。对象字段顺序无关，行和列表顺序必须一致，重复 JSON 字段拒绝。

发布请求携带 ExpectedModelConcurrencyStamp、ExpectedRevisionConcurrencyStamp 及可选 ChangeSummary。回滚请求携带 TargetRevisionId、ExpectedActiveRevisionId、ExpectedModelConcurrencyStamp、ExpectedTargetRevisionConcurrencyStamp。

## 5. 已发布规则执行

`POST /execute`，权限 E。Context 与 Inputs 使用原生 JSON：

```json
{
  "appCode": "DEMO",
  "scenarioCode": "size-expansion",
  "contractVersion": "1.0",
  "context": {"productKind": "A"},
  "inputs": {"inputWidth": 120}
}
```

ContractVersion 可选，指定时只考虑相应版本的活动修订。响应包含 ExecutionId、Status、RuleModelId、RevisionId、BindingId、Results、Error。成功结果结构如下，其字段与数值仅为示例：

```json
{
  "results": [{
    "sourceRuleIds": ["22222222-2222-2222-2222-222222222222"],
    "outputs": {
      "targetWidth": 125,
      "tolerance": {"min": -2, "max": 3}
    }
  }]
}
```

示例中的 tolerance 需由宿主契约声明，不能额外添加到 size-expansion 场景。原生数值、对象、数组不再包装成 JSON 字符串；也没有顶层 Outputs/OutputsJson。四种确定性状态均返回 HTTP 200，非成功 Results 为空。认证、请求校验及系统异常不属于此 HTTP 200 约定。

### 当前稳定运行错误码

所有完整码均以 `NRuleEngine:` 开头，下表省略此前缀。常量定义见 [NRuleEngineErrorCodes](../../src/NRuleEngine.Domain.Shared/NRuleEngineErrorCodes.cs)，实际映射见 [RuleExecutionAppService](../../src/NRuleEngine.Application/Runtime/RuleExecutionAppService.cs)。

| 通道/状态 | 错误码 |
| --- | --- |
| Rejected：契约 | Contract:AppNotRegistered、Contract:ScenarioNotRegistered、Contract:ContractVersionNotSupported、Contract:ScenarioContractMismatch |
| Rejected：模型/绑定 | Binding:NoPublishedModel、Binding:PublishedRevisionUnavailable、Binding:AmbiguousBinding |
| Rejected：输入 | Input:RequiredContextMissing、Input:RequiredInputMissing、Input:UnknownInput、Input:InputTypeMismatch、Input:InputConstraintViolated |
| NotMatched | Binding:NoMatchingBinding、Evaluation:NoMatchingRule |
| Failed | Evaluation:ExpressionEvaluationFailed、Evaluation:MultipleWriteConflict、Output:RequiredOutputMissing、Output:ResultLimitExceeded |
| 异常 | Integrity:PublishedContentCorrupted、System:StorageUnavailable、System:Timeout、System:Unexpected |

条件与赋值错误统一映射 ExpressionEvaluationFailed，再通过 ErrorPath、VariableCode、SourceStart/SourceLength 定位；没有独立的函数异常、除零或类型转换稳定运行码。管理端另有 Concurrency:Conflict、Variable:Referenced、Release:GateFailed 等 BusinessException 码，不使用规则集的四状态协议。

Error.Data 仅用于必要定位，不包含变量真实值、完整表达式或内部异常细节。宿主记录 ExecutionId 并自行决定重试或业务兜底。默认执行预算为两秒，内部超时走异常；执行中调用方取消的传播尚有缺口，见[架构说明](../architecture/overview.md#10-错误时间预算与权限)。

## 6. 条件集

| 方法与路径 | 用途 | 权限 |
| --- | --- | --- |
| POST `/condition-sets` | 创建条件集与首个草稿 | M |
| GET `/condition-sets/{setId}` | 读取集合 | M |
| GET `/condition-sets/{setId}/revisions` | 修订摘要 | M |
| GET `/condition-sets/revisions/{revisionId}` | 条件及输入稳定键 | M |
| POST `/condition-sets/{setId}/drafts` | 从指定或活动修订复制草稿 | M |
| PUT `/condition-sets/revisions/{revisionId}/condition` | 修改条件与优先级 | M |
| POST `/condition-sets/revisions/{revisionId}/publish` | 使用正反样本发布 | P |
| POST `/condition-sets/{setId}/rollback` | 回滚 | P |
| GET `/condition-sets/{setId}/history` | 发布历史 | M |
| POST `/condition-sets/{setId}/archive` | 归档 | M |
| POST `/condition-sets/evaluate` | 明确候选判定 | E |

没有 GET `/condition-sets` 列表、跨条件集复制、草稿废弃或独立草稿试算端点。发布提供 2–100 条完整输入样本且至少一正一反；不复用规则集的门禁数组协议。

判定请求提供 AppCode、ScenarioCode、精确 ContractVersion、CandidateCodes、Mode 和原生 JSON Inputs。Mode 为 AllMatches 或 FirstMatch。响应为 Succeeded、Matched、Matches、ErrorCode、ErrorMessage；每个命中包含 Code、ConditionSetId、RevisionId、Priority。

正常无命中仍为 Succeeded=true。InvalidMode、InvalidCandidates、InvalidCandidate、InvalidInput、EvaluationError、AmbiguousMatch 是条件判定自己的诊断码，详见 [ConditionSetEvaluator](../../src/NRuleEngine.Domain/Conditions/ConditionSetEvaluator.cs) 及 [ConditionSetAppService](../../src/NRuleEngine.Application/Conditions/ConditionSetAppService.cs)。前置目录错误走异常通道。

## 7. 客户端注意事项

- 文中枚举以 C# 名称表达语义；HTTP 的枚举编码由宿主 ABP JSON 配置决定，调用方按实际序列化协议生成，不把 AST 专用转换器误当成全部 DTO 的约定。
- 每次写入后替换本地并发戳；冲突后重新读取并核对变更，不自动重放覆盖。
- 变量、规则及绑定等编辑结果以服务端响应为准。浏览器 sessionStorage、原型发布检查及本地试算均不代表数据库状态。
- 在进程内直接调用应用服务时，宿主须保证授权和数据范围；上述权限表描述 HTTP 控制器入口。
