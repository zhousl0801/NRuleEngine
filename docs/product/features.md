# NRuleEngine 产品功能说明

本文描述当前仓库的产品能力与交付边界。技术实现见[系统架构](../architecture/overview.md)，调用方式见[API 参考](../reference/api.md)。

## 1. 产品定位与交付范围

NRuleEngine 是供 ABP 应用引用的通用规则编制与运行组件。接入应用定义场景、输入、必需输出和适用维度，维护者通过 API 编制规则并发布，业务流程使用已发布修订进行计算或条件判定。

当前交付包括六个后端模块、本地联调 Host、分层自动化测试，以及独立的[交互原型](../../prototype/README.md)。首版以**后端组件和 API**为范围；原型尚未连接后端，不能作为正式编制台交付或服务端执行凭据。业务系统的模块装配、数据库迁移、数据映射和生产验收由接入方完成。

引擎不包含具体行业的领域对象，不执行任意脚本或外部 I/O，不自动迁移旧规则，也不要求接入方单独部署规则平台。

## 2. 两类规则能力

| 能力 | 规则集（RuleModel） | 条件集（ConditionSet） |
| --- | --- | --- |
| 用途 | 条件命中后计算并产生约定的结果字段 | 判定业务方指定的候选是否满足条件 |
| 内容 | 变量、条件、赋值、场景绑定、发布门禁 | 输入、一个布尔条件、判定优先级、发布样本 |
| 运行选择 | 从同场景全部活动模型的绑定中裁决唯一模型 | 只判断请求给出的候选编码 |
| 结果 | 有序完整结果行，每行包含来源规则和出口值 | 命中候选的稳定编码及修订标识 |
| 多命中 | CollectMatches 在一个已选模型内返回多行 | AllMatches 返回多个命中候选 |

两者使用独立场景契约和编制流程。条件集不会执行赋值或保存业务参数；调用方自行将命中编码映射到业务数据。

## 3. 产品对象与生命周期

规则集层级为 `App → 场景 → 规则集 → 修订 → 规则 → 赋值`。一个场景可以包含多个规则集，每个规则集独立维护和发布。

| 对象 | 含义及约束 |
| --- | --- |
| App | 接入应用的逻辑边界，由稳定的 AppCode 标识；不等于租户或数据权限 |
| 场景 | 宿主代码注册的用途及契约；维护者只读 |
| 规则集/条件集 | 应用内编码唯一，支持归档；归档后停止参与运行，历史保留 |
| 修订 | 一份完整内容快照；草稿可编辑，发布后只读 |
| 活动修订 | 当前运行内容，一个集合只有一个活动指针 |
| 发布记录 | 记录发布或回滚的序号、目标、前一修订、说明和审计信息 |

草稿修改不改变线上内容。回滚重新激活历史发布修订并新增发布记录，不改写历史内容。已发布内容没有产品删除入口；草稿子对象采用物理删除，组件不采用软删除。

## 4. 应用、场景与契约

宿主通过代码注册 App、赋值场景和条件场景。目录 API 返回应用、场景版本、变量类型、绑定维度及允许的操作符。重复注册、无效契约或场景引用未注册应用会在初始化阶段被拒绝。

修订锁定精确的 ContractVersion，复制草稿保留原版本。宿主须保留仍被发布修订使用的契约，运行时不会推断“最新版本”或自动升级。旧规则适配新场景定义属于后续能力，见第 10 节。

当前场景描述不提供名称、用途说明、函数目录、兼容性状态或动态业务候选值；原型中的丰富说明由本地示例数据提供。

## 5. 规则集编制

### 模型与草稿

- 创建模型时自动创建首个草稿，并从契约实例化输入与必需输出。
- 可按应用、场景查询模型，读取修订、活动版本和发布历史。
- 可从指定修订或当前活动修订创建新草稿；首发前也可创建空草稿。
- 可从指定来源修订复制独立模型及首个草稿，继承变量稳定键、执行设置和门禁内容；复制不会自动发布。
- 归档前可查询活动修订影响，归档保留历史。

### 变量与赋值

| 变量 | 可维护内容 | 固定边界 |
| --- | --- | --- |
| 契约输入 | 默认值 | 编码、名称、方向、类型及精度由契约决定；无单位维护入口 |
| 用户中间变量 | 新增、改名、默认值、单位、Decimal 精度、安全删除 | 只能是单值；StableKey 不随改名变化 |
| 契约输出 | 在规则中配置赋值 | 不允许新增契约外出口或修改其定义 |

基础类型为 Decimal、String、Boolean、Enum。输出可为单值、数值区间或同类型候选列表；复合类型仅用于契约输出。区间包含两端且 min ≤ max；列表保留顺序，包含 1–100 个非空、互不重复的同类型元素。

每条规则维护名称、条件、优先级、同级顺序和启用状态。每条赋值独立选择常量或表达式，可写中间变量或出口，不能写入口。同一规则不能重复写同一目标。

表达式以版本化 AST 保存，变量引用使用 StableKey；展示文本不参与执行。支持比较、逻辑、四则运算、空值判断、集合包含和固定白名单函数，详见[表达式与值结构](../reference/expressions.md)。

缺失入口或尚未写入的中间变量使用已配置默认值；显式 JSON null 仍按可空性检查。默认值不计入规则赋值次数。未知入口严格拒绝。

### 场景绑定与影响分析

绑定由优先级和多个按 AND 组合的维度条件组成，字段、操作符和值类型必须符合场景契约。绑定随发布修订激活，不包含生效期。

运行时比较同场景全部活动模型的命中绑定，唯一最高优先级绑定所属模型被执行；并列最高返回歧义错误，即使这些绑定属于同一模型也不隐式选取。发布和回滚提前检查跨模型可能存在的同优先级重叠。

变量引用查询返回条件、表达式、赋值目标及门禁的相关影响和定位路径。有引用的草稿中间变量禁止删除；删除规则、赋值及归档模型的影响仅作提示。写入时重新检查并发戳，不能依赖过时的影响预览。

## 6. 校验、试算与发布

```mermaid
flowchart LR
    A[选择应用与场景] --> B[创建或复制草稿]
    B --> C[维护变量、规则、赋值和绑定]
    C --> D[完整校验]
    D --> E[试算与门禁用例]
    E --> F[发布并切换活动修订]
    F --> G[业务执行]
```

| 阶段 | 行为 |
| --- | --- |
| 保存 | 检查当前对象的基本格式、类型、引用和并发戳；允许尚未完整的草稿 |
| 完整校验 | 检查规则/绑定存在性、必需出口、类型、依赖环、重复写入和修订内绑定歧义 |
| 试算 | 对指定修订执行，返回绑定、逐规则条件、实际赋值、结果行和错误位置；不切换活动版本、不存 Trace |
| 发布 | 在事务内校验修订、跨模型绑定及门禁，冻结内容并切换活动指针 |
| 回滚 | 校验历史发布内容、当前指针、并发戳及现有绑定冲突后重新激活 |

规则集门禁逐条保存场景上下文、输入、ExpectedStatus 和有序出口数组 ExpectedResultsJson。只接受成功或正常未匹配预期；成功至少一行，未匹配为 `[]`，每个修订至少有一条成功样本。比较完整结果结构和行顺序，不比较复制后会变化的来源规则 ID。

每次草稿写入携带当前修订并发戳，成功后使用新戳；发布另检查模型并发戳。过期写入被拒绝，不提供自动合并或现成的版本差异结果。

## 7. 运行语义

| 场景结果要求 | 执行模式 | 行为 |
| --- | --- | --- |
| Single | FirstMatch | 按优先级降序、同级顺序升序判断，首条命中后停止，生成一行 |
| Single | Sequential | 按顺序执行，共享赋值上下文，全部命中规则共同形成一行 |
| Many | CollectMatches | 每条规则从相同输入及默认值独立计算，每条命中生成一行完整出口 |

执行模式受场景结果基数约束。FirstMatch 与 CollectMatches 每条启用规则均须独立覆盖必需出口；Sequential 可分步写入，但实际命中路径最终仍须产出全部出口。

Sequential 的重复写入策略为：ErrorOnMultipleWrite 拒绝潜在重复写入；FirstWriteWins 保留首次写入并跳过后续公式；LastWriteWins 用当前上下文求值并覆盖。CollectMatches 固定使用 ErrorOnMultipleWrite 配置，不在行间裁决覆盖。

运行和试算均返回 Results，每行有 SourceRuleIds 和原生 JSON Outputs。多行保留顺序、重复字段值及字段对应关系，不自动去重或合并。单次结果最多 100 行，序列化结果数组最多 65,536 UTF-8 字节；超限整体失败。

| 状态 | 含义 |
| --- | --- |
| Succeeded | 产生全部必需出口 |
| NotMatched | 无绑定命中，或已选模型无规则命中 |
| Rejected | 契约、输入、模型或绑定选择不满足执行前提 |
| Failed | 条件、赋值、重复写入、出口或结果限额检查失败 |

上述四种确定性运行结果统一返回 HTTP 200；非成功结果不交付部分业务行。系统故障使用异常通道。引擎不会自动忽略失败规则、回退旧版本或执行宿主的兜底业务。

调用方在进入执行前或执行中取消，均原样传播 OperationCanceledException，不包装为系统故障；内部预算超时返回 System:Timeout 异常。两者均已取消时优先按调用方取消处理。

## 8. 独立条件集

条件场景只声明输入。维护者创建条件集和草稿，读取输入 StableKey 后编制一个返回 Boolean 的条件及优先级。条件集支持读取、复制草稿、归档、发布、回滚和历史查询。

发布需 2–100 条样本，至少一条预期命中、一条预期不命中；样本使用完整契约输入，全部通过后冻结修订。样本保存在发布历史，宜使用非敏感测试数据。

判定请求必须指定精确契约版本和 1–100 个互不重复的候选编码：

- AllMatches：按优先级降序、编码升序返回全部命中。
- FirstMatch：裁决唯一最高优先级命中；同优先级多命中返回 AmbiguousMatch，赢家确定后不计算更低优先级候选。

正常无命中为 `Succeeded=true, Matched=false, Matches=[]`。输入或候选无效、表达式失败使用独立的条件判定错误协议，不能套用规则集运行状态。

## 9. 权限与宿主职责

组件定义 NRuleEngine.Manage、NRuleEngine.Publish、NRuleEngine.Execute。宿主决定角色授权、应用数据范围、身份认证及审计展示；AppCode 查询条件本身不是访问控制。本地 Host 自动授权，仅用于本机联调。

宿主负责注册业务契约、准备输入和绑定上下文、消费结果行或候选编码、处理业务兜底，以及管理实际数据库迁移。组件没有 ODS 集成包、正式认证系统、生产迁移或完整前端。

## 10. 未实现项与后续边界

当前已实现核心编制与运行闭环，但不能把旧草案的全部描述视为已交付。以下能力在当前代码中尚无完整入口，整理文档不代表取消需求或补齐实现：

| 未实现能力 | 当前证据与影响 |
| --- | --- |
| 动态绑定候选值 Provider 与查询 | 目录仅返回维度定义；没有 IRuleBindingValueProvider 或候选查询 API，业务资源下拉列表需后续实现或由宿主提供 |
| 宿主注册自定义类型、函数 | 类型枚举及函数分派固定；当前扩展点仅注册应用与两类场景，不能按旧草案承诺注入新函数/类型 |
| 条件集按应用/场景列表查询 | 只有按 ID 读取及明确候选判定，正式条件集列表尚缺查询接口；也没有跨条件集复制能力 |
| 场景名称、说明与兼容性展示 | 场景描述仅含代码、版本和结构契约；原型说明及兼容差异没有对应完整后端能力 |
| 草稿废弃入口与保留期清理 | 有 Abandoned 状态和领域方法，没有管理 API 或自动清理任务；当前没有自动历史清理策略 |
| 中英文本地化资源 | 当前错误说明采用内置中文字符串，没有草案所述的双语资源体系 |

首版以外的后续能力：

- **旧规则适配新场景定义**：已明确列为后续。由宿主明确目标定义，在新草稿中展示变量、绑定与门禁差异；未变化变量保留 StableKey，新变量生成新键，破坏性变化形成阻断清单。不得静默删除引用或改变旧发布修订；新草稿通过完整校验和门禁后才切换线上。业务界面无需维护者选择版本；是否保留显式 ContractVersion 及如何标识当前定义，实施前另行确定。
- 草稿与发布版本差异、规则包跨环境导入导出、审批流、绑定生效期、增强诊断、持久化执行 Trace、变量敏感标记及自动脱敏。
- 正式嵌入式编制台与后端联调。原型已覆盖主要交互，但不代表后端新增/保存/发布，也不验证正式鉴权、并发恢复或数值精度。

## 11. 实现与验证入口

| 功能 | 实现入口 | 主要自动化证据 |
| --- | --- | --- |
| 目录与契约 | [RuleCatalog](../../src/NRuleEngine.Domain/Scenarios/RuleCatalog.cs) | [RuleCatalogTests](../../test/NRuleEngine.Domain.Tests/RuleCatalogTests.cs) |
| 编制、默认值、复制与影响 | [RuleModelAppService](../../src/NRuleEngine.Application/RuleModels/RuleModelAppService.cs)、[RuleModelRevision](../../src/NRuleEngine.Domain/RuleModels/RuleModelRevision.cs) | [RuleModelAppServiceTests](../../test/NRuleEngine.Application.Tests/RuleModelAppServiceTests.cs)、[RuleMaintenanceTests](../../test/NRuleEngine.Domain.Tests/RuleMaintenanceTests.cs) |
| 校验、表达式与试算 | [RuleRevisionValidator](../../src/NRuleEngine.Domain/Validation/RuleRevisionValidator.cs)、[RuleSimulator](../../src/NRuleEngine.Domain/Simulation/RuleSimulator.cs) | [RuleAuthoringTests](../../test/NRuleEngine.Domain.Tests/RuleAuthoringTests.cs) |
| 发布、回滚与执行 | [RuleReleaseAppService](../../src/NRuleEngine.Application/Release/RuleReleaseAppService.cs)、[RuleExecutionAppService](../../src/NRuleEngine.Application/Runtime/RuleExecutionAppService.cs) | [发布测试](../../test/NRuleEngine.Application.Tests/RuleReleaseAppServiceTests.cs)、[运行测试](../../test/NRuleEngine.Application.Tests/RuleExecutionAppServiceTests.cs) |
| 多行与复合出口 | [RuleValueEngine](../../src/NRuleEngine.Domain/Expressions/RuleValueEngine.cs) | [CollectMatchesTests](../../test/NRuleEngine.Domain.Tests/CollectMatchesTests.cs)、[CompositeOutputAppServiceTests](../../test/NRuleEngine.Application.Tests/CompositeOutputAppServiceTests.cs) |
| 条件集与存储 | [ConditionSetAppService](../../src/NRuleEngine.Application/Conditions/ConditionSetAppService.cs)、[EF 映射](../../src/NRuleEngine.EntityFrameworkCore/NRuleEngineModelBuilderExtensions.cs) | [条件集应用测试](../../test/NRuleEngine.Application.Tests/ConditionSetAppServiceTests.cs)、[EF 测试](../../test/NRuleEngine.EntityFrameworkCore.Tests/) |

这些入口证明组件级实现与覆盖范围，不代表已完成真实业务系统接入、生产数据库迁移、生产鉴权或订单结果对照。运行检查命令见[后端指南](../../BACKEND.md)。
