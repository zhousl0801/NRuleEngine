# NRuleEngine 设计文档

NRuleEngine 是供 ODS 等 ABP 应用引用的通用规则编制与运行组件。当前已建立 .NET 10 / ABP 10.6 的模块骨架，完成赋值型规则与独立布尔条件集的场景注册、草稿、发布、回滚及运行能力。宿主集成尚未执行。

设计文档按用途归档在 [docs](docs/)；可编译项目位于本目录。本文是后端文档总入口。

## 文档地图

| 分类 | 文档 | 用途 | 当前版本 |
|---|---|---|---|
| 产品 `docs/product/` | [产品设计草案](docs/product/NRuleEngine-产品设计草案.md) | 产品目标、首版范围、能力、交互和产品验收 | v0.9 |
| 架构 `docs/architecture/` | [系统设计草案](docs/architecture/NRuleEngine-系统设计草案.md) | ABP 结构、扩展契约、领域模型、接口和技术验收 | v0.9 |
| 架构 `docs/architecture/` | [运行错误与异常模型](docs/architecture/runtime-error-model.md) | 执行状态、稳定错误码、异常数据和 ODS 映射 | v0.3 |
| 架构 `docs/architecture/` | [编制 AST v1](docs/architecture/authoring-ast-v1.md) | 草稿表达式 JSON 格式与当前 API 范围 | v0.1 |
| 架构 `docs/architecture/` | [条件集设计与使用](docs/architecture/ConditionSet-条件集设计与使用.md) | 无赋值布尔条件的领域边界、API 与候选匹配语义 | 2026-09-29 |
| 决策 `docs/adr/` | [架构决策记录](docs/adr/README.md) | 已确认决策、替代方案和后果 | ADR-0001–0019 |

## 已确认的首版边界

- 只提供后端领域能力和 API，前端待 API 稳定后设计。
- App 和场景由接入应用代码注册。
- 同一场景允许多个已发布模型，按绑定优先级裁决唯一结果。
- 场景契约固定入口和必需出口，用户可维护中间变量。
- 试算只验证输入、表达式、规则命中和最终输出，不持久化完整 Trace。
- 不提供独立 Host、不包含 ODS 集成项目、不采用软删除。

## 骨架验证

先进入 `back-end/`，再执行：

```bash
dotnet restore NRuleEngine.slnx
dotnet build NRuleEngine.slnx --no-restore
dotnet test NRuleEngine.slnx --no-build --no-restore
```

解决方案按 `src`、`test` 文件夹分组。上述命令构建六个 ABP 模块，并运行 Domain、Application、EntityFrameworkCore 三层测试；`TestBase` 是共享测试基础项目。已有代码注册目录、模型与草稿、契约/中间变量、草稿规则与赋值、绑定、EF 映射、完整校验、试算、发布、回滚及执行 API。测试中的尺寸展开契约是模拟数据，不代表 ODS 真实字段。生产宿主的迁移、鉴权和 HTTP 联调尚未执行。

## 当前 API

HTTP 控制器由接入宿主装配；仓库本身不启动服务。

| 路径 | 能力 |
|---|---|
| `GET /api/nrule-engine/catalog/apps`、`GET /api/nrule-engine/catalog/apps/{appCode}/scenarios` | 查询代码注册的目录与契约版本 |
| `POST /api/nrule-engine/models`、`GET /api/nrule-engine/models` | 创建和查询模型；创建时自动生成首个草稿 |
| `GET /api/nrule-engine/models/{modelId}/revisions`、`GET /api/nrule-engine/models/revisions/{revisionId}` | 发现草稿并读取变量与并发戳 |
| `POST /api/nrule-engine/models/{modelId}/drafts` | 从指定修订或当前活动修订复制草稿；无活动修订时新建空草稿 |
| `POST /api/nrule-engine/models/revisions/{revisionId}/intermediate-variables` | 增加中间变量；修改操作携带期望并发戳 |
| `POST .../revisions/{revisionId}/rules`、`PUT/DELETE .../rules/{ruleId}` | 创建、修改、删除草稿规则；赋值使用 `.../rules/{ruleId}/assignments` 子路径 |
| `POST .../revisions/{revisionId}/bindings`、`PUT/DELETE .../bindings/{bindingId}` | 创建、修改、删除草稿绑定 |
| `PUT /api/nrule-engine/models/revisions/{revisionId}/execution-semantics` | 设置 FirstMatch/Sequential 和多次写入策略 |
| `GET /api/nrule-engine/models/revisions/{revisionId}/validation` | 完整校验草稿，返回对象路径及错误 |
| `POST /api/nrule-engine/models/revisions/simulate` | 按指定草稿、场景上下文和输入试算，不修改线上状态 |
| `POST/DELETE /api/nrule-engine/models/revisions/{revisionId}/gate-cases` | 维护草稿发布门禁回归用例；删除路径另带 `{gateCaseId}` |
| `POST /api/nrule-engine/models/revisions/{revisionId}/publish` | 完整校验、门禁与跨模型绑定检查后发布草稿 |
| `POST /api/nrule-engine/models/{modelId}/rollback`、`GET /api/nrule-engine/models/{modelId}/releases` | 回滚至历史发布修订及查询发布记录 |
| `POST /api/nrule-engine/execute` | 在全部活动发布模型中按绑定优先级选出唯一修订并执行；四种确定性状态均返回 HTTP 200 |
| `GET /api/nrule-engine/catalog/apps/{appCode}/condition-scenarios` | 查询只含输入的条件判定场景契约 |
| `POST /api/nrule-engine/condition-sets`、`GET .../{setId}/revisions` | 创建条件集及首个草稿，读取修订 |
| `PUT /api/nrule-engine/condition-sets/revisions/{revisionId}/condition` | 编制唯一布尔条件及优先级 |
| `POST .../revisions/{revisionId}/publish`、`POST .../{setId}/rollback` | 用正反样本发布条件集，或回滚历史修订 |
| `POST /api/nrule-engine/condition-sets/evaluate` | 按明确候选编码作 `AllMatches` / `FirstMatch` 布尔判定 |

赋值型规则编制及门禁维护接口要求 `NRuleEngine.Manage` 权限；发布、回滚及其历史查询要求 `NRuleEngine.Publish`；执行接口要求 `NRuleEngine.Execute`。条件集编制和历史查询使用 Manage，发布与回滚使用 Publish，候选判定使用 Execute。当前表达式输入为版本化 AST JSON，详见[编制 AST v1](docs/architecture/authoring-ast-v1.md)。运行请求的 `Context` 和 `Inputs` 使用原生 JSON 值，可选 `ContractVersion` 只考虑对应版本的活动修订；结果提供 `ExecutionId`、状态、稳定错误码及原生 JSON 出口。宿主可通过 `RuleExecutionOptions.Timeout` 配置执行预算，默认两秒。宿主数据库迁移和 ODS 接入尚未执行。

发布与回滚新增 `NreReleaseGateCases`、`NreReleaseRecords`、`NreReleaseScopes` 表及模型的 `NextReleaseNo` 字段。条件集另有 `NreConditionSets`、`NreConditionSetRevisions`、`NreConditionInputDefinitions` 和 `NreConditionSetReleaseRecords`。宿主须生成并执行相应迁移后再启用接口。发布服务在独立数据库事务中先写入场景协调行，再查询其他活动模型；并发首次创建同一协调行时，数据库唯一约束会使其中一个请求失败，调用方应刷新并重试。执行服务每次从数据库批量读取活动修订并核对内容哈希；当前组件不缓存运行时快照。

## 代码职责与 C# 引用

`RuleModels` 保留模型、修订和变量聚合；`Rules` 管理规则与赋值，`Bindings` 管理场景绑定，`Validation` 和 `Simulation` 分别负责整份校验与试算；`Release` 管理发布门禁、快照哈希、发布记录和活动修订切换；`Runtime` 管理活动修订选择与运行结果。修订仍是内容一致性边界。

直接引用组件程序集的调用方需要更新 C# 命名空间和注入接口：规则编制使用 `NRuleEngine.Application.Contracts.Rules.IRuleAuthoringAppService`，绑定使用 `...Bindings.IRuleBindingAppService`，校验与试算分别使用 `...Validation.IRuleValidationAppService`、`...Simulation.IRuleSimulationAppService`。对应 DTO 和领域规则、绑定类型也迁入同名目录与命名空间；模型与修订接口仍在 `RuleModels`。

## 已收敛的编码前决策

1. 发布修订锁定准确的场景契约版本，见 ADR-0015。
2. Revision 是草稿内容一致性边界，发布事务及数据库级并发协调见 ADR-0016。
3. 首版表达式边界与规则顺序见 ADR-0017。
4. 首版不设绑定生效期，见 ADR-0018。
5. 独立条件集与明确候选判定，见 ADR-0019。
6. App/场景数据库目录投影仍待对应能力实现前决定。

## 维护约定

- 产品行为以产品设计草案为准。
- 技术结构和不变量以系统设计草案及已接受 ADR 为准。
- ODS 文档只描述接入映射，不反向定义 NRuleEngine 核心能力。
- 运行错误等专项技术契约放在 `docs/architecture/`，由系统设计引用，避免在两处维护完整定义。
- 评审文档是特定版本的历史快照，问题关闭后不回写原评审结论。
- 高成本、难以撤销的技术选择写入 ADR；普通首版范围裁剪留在产品文档。
- 新增主题时先确定所属分类，并在本文档地图补入口；移动文档时同步检查相对链接。
