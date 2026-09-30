# ConditionSet：条件集设计与使用

## 定位与边界

`RuleModel` 表达“条件命中后执行赋值并产生必需输出”；`ConditionSet` 表达“某个候选是否满足布尔条件”。条件集没有业务输出字段、赋值动作或 ODS 参数值。ODS 可以用命中的稳定编码找机组参数、MIC 属性等业务数据。引擎只负责条件判定，不接管这些值的生命周期。

一个 `ConditionSet` 属于固定 `AppCode + ScenarioCode`，`Code` 在应用内唯一并跨修订稳定。`ConditionSetRevision` 锁定 `ConditionScenarioDescriptor` 的精确契约版本，保存输入快照、布尔 AST、优先级、发布状态和内容哈希。AST 用输入的 `StableKey` 引用变量；复制草稿保留这些键、新建实体 ID。已发布修订只读，回滚只切换活动指针并记审计记录。归档条件集不会删除历史修订，运行时不接受归档候选。

## 宿主注册与编制

宿主通过 `IRuleAppContributor` 注册应用，并通过 `IConditionScenarioContributor` 注册 `ConditionScenarioDescriptor`。条件场景只声明输入；变量方向必须为 `Input`，编码在契约内唯一，类型沿用 `RuleTypeDescriptor`。该目录独立于要求必需出口的 `RuleScenarioDescriptor`。一个场景版本被历史发布引用时，宿主须继续注册它。

管理端可从 `GET /api/nrule-engine/catalog/apps/{appCode}/condition-scenarios` 读取契约，随后调用 `POST /api/nrule-engine/condition-sets` 创建条件集和首个草稿，再通过 `GET /api/nrule-engine/condition-sets/{setId}/revisions` 找到草稿标识。`GET .../revisions/{revisionId}` 返回输入 `StableKey`；`PUT .../revisions/{revisionId}/condition` 写入 AST、可选原始文本及优先级。条件 AST 必须通过现有表达式静态检查且返回 Boolean，原始文本仅供展示，不参与执行。

编制操作要求最新并发戳。复制草稿可指定来源修订，默认来自当前活动修订；首发前无活动修订时从最初契约创建。编码重复由业务检查与数据库唯一索引共同阻止。

## 发布与回滚

`POST .../revisions/{revisionId}/publish` 检查条件集及修订的并发戳、精确场景契约与输入快照，并要求 2–100 条发布样本，其中至少一条预期 `true`、一条预期 `false`。每条样本提供完整的输入 JSON，实际布尔结果必须等于预期。验证通过后，事务内冻结修订、计算 `sha256:v1` 内容哈希、切换活动指针并保存发布记录及样本。失败不会激活草稿。`POST .../{setId}/rollback` 只接受同一条件集内已发布且哈希一致的历史修订，并记录回滚。`GET .../{setId}/history` 返回发布序号和目标修订；`POST .../{setId}/archive` 停止后续运行。

发布样本应使用可共享的非敏感测试数据，因为它们会保存到发布历史。当前内容哈希覆盖输入稳定键与类型、契约版本、优先级和执行 AST；原始条件文本及发布说明不影响执行语义。

## 候选判定

`POST /api/nrule-engine/condition-sets/evaluate` 要求精确 `AppCode`、`ScenarioCode`、`ContractVersion`、1–100 个互不重复的 `CandidateCodes`、完整输入 JSON 以及 `Mode`：

- `AllMatches`：按优先级降序、编码升序返回所有命中候选，适合一个工艺步骤可选多个参数的情况。
- `FirstMatch`：返回最高优先级命中；同优先级候选仍需判定，多个命中时返回 `AmbiguousMatch`；找到赢家后不计算更低优先级的表达式。

`Succeeded=true, Matched=false, Matches=[]` 表示正常不命中。输入缺失、多余、类型/精度错误、候选不存在或不可用、表达式异常均令 `Succeeded=false`，并返回独立 `ErrorCode`。接口只查询请求指定的候选；不会扫描全场景后猜测业务范围。调用方应在自己的事务/业务流程中把返回的稳定 `Code` 映射到机组参数或 MIC 属性，并自行处理旧标识映射。

管理、发布、运行端点分别使用 `NRuleEngine.Manage`、`NRuleEngine.Publish`、`NRuleEngine.Execute` 权限。EF 表包括 `NreConditionSets`、`NreConditionSetRevisions`、`NreConditionInputDefinitions` 和 `NreConditionSetReleaseRecords`；宿主接入时仍需生成并执行其数据库迁移。本仓库未连接或修改生产 ODS。

## 能力验证与待确认口径

Domain、Application 和 EF 测试使用合成场景覆盖条件命中与不命中、优先级歧义、草稿复制、样本门禁和回滚。测试仅验证 NRuleEngine 的组件能力。真实业务迁移仍需核对旧引擎的启用状态、批量返回排序、异常和空值口径，并通过同输入结果对照确认适配行为。
