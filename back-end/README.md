# NRuleEngine 后端开发与接入

产品和技术正文分别见[产品功能说明](docs/product/features.md)与[系统架构说明](docs/architecture/overview.md)。调用协议见[API 参考](docs/reference/api.md)，AST 与复合出口见[表达式与值结构](docs/reference/expressions.md)。本文只说明开发运行和宿主接入。

## 目录与构建

src 下包含六个分层 ABP 模块及本地 Host；test 下按 Domain、Application、EntityFrameworkCore 分层，TestBase 提供公共测试装配。所有项目使用 net8.0，ABP 与 EF Core SQLite 为 8.0.4；SDK 由本目录 global.json 选择，包版本在 Directory.Packages.props 集中定义。

在 back-end 目录执行：

```bash
dotnet restore NRuleEngine.sln
dotnet build NRuleEngine.sln --no-restore
dotnet test NRuleEngine.sln --no-build --no-restore
```

如本机文件监控影响运行，可设置 `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`。VSTest 需要本机端口通信；沙箱阻止监听时，需在允许测试通信的执行环境重跑，不能将中止视为测试通过或功能失败。

## 本地联调 Host

```bash
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet run --project src/NRuleEngine.Host
```

默认监听 `http://127.0.0.1:5088`，SQLite 路径为工作目录下的 nrule-engine-dev.sqlite。首次启动调用 EnsureCreatedAsync 建表，重启保留数据；EnsureCreated 不更新已有表结构。需要隔离验证时可指定新的临时库，保留已有开发数据：

```bash
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet run --project src/NRuleEngine.Host -- --ConnectionStrings:Default='Data Source=/tmp/nrule-engine-local-check.sqlite'
```

此示例路径若已存在会复用该库，需隔离时自行指定新的文件名。模型变动后选择明确的迁移或新库，不应指望自动加列。

Host 使用固定 local-dev 开发身份和自动授权，**仅供本机联调，不要对外网开放**。默认 CORS 来源为 localhost/127.0.0.1 的 5173、3000 端口，可在 Host 的 appsettings.json 中配置。原型按静态站点独立运行，目前没有 API 请求。

### 演示契约

应用编码均为 DEMO，契约版本均为 1.0；启动只注册契约，不自动建立可发布的业务规则。

| 场景 | 输入 | 必需输出/用途 | 绑定 |
| --- | --- | --- | --- |
| size-expansion | inputWidth | targetWidth，单行计算 | productKind |
| size-candidates | inputWidth | widthRange 数值区间、candidateWidths 数值列表、candidateCodes 文本列表，单行计算 | productKind |
| matched-results | width | targetWidth、tolerance 数值区间，多行 CollectMatches | 无维度，仍需创建绑定 |
| thickness-eligibility（条件场景） | thickness | 布尔资格判定 | 不使用规则集绑定 |

先查询 GET `/api/nrule-engine/catalog/apps`，再读取 DEMO 下的 scenarios 或 condition-scenarios。规则集联调顺序为创建模型、读取草稿稳定键、编制规则/赋值/绑定、完整校验、试算、保存门禁、读取最新并发戳、发布、执行。字段编码和演示数值不是实际业务系统契约。

### Docker

在仓库根目录运行：

```bash
docker compose up --build -d
docker compose logs -f nrule-engine
```

Compose 映射 `127.0.0.1:5088` 到容器 8080，命名卷保存 SQLite。`docker compose down` 停止服务但保留卷。Dockerfile 使用 .NET 8 SDK/ASP.NET 镜像，容器运行的仍是开发授权 Host。

## 宿主接入步骤

1. 按层引用 Domain.Shared、Domain、Application.Contracts、Application、EntityFrameworkCore、HttpApi 模块；HTTP 可按实际调用方式选用。当前表达式与运行能力在 Domain/Application 内部，没有独立运行包。
2. 实现并通过依赖注入注册 IRuleAppContributor、IRuleScenarioContributor、IConditionScenarioContributor，建立实际业务契约。示例见 [Host 场景贡献者](src/NRuleEngine.Host/Scenarios/)。目前没有自定义函数/类型及动态绑定候选值 Provider 扩展。
3. 装配 EF 数据库提供程序和连接，选择使用组件 DbContext，或在宿主整合组件模型与仓储。模型配置入口为 ConfigureNRuleEngine()，上下文接口为 INRuleEngineDbContext；仓储当前以 NRuleEngineDbContext 注册，不能仅调用模型扩展就假定完成上下文替换。
4. 由宿主生成、审阅并执行迁移。SQLite 的组件测试不代表 PostgreSQL 等提供程序和宿主迁移已经验收；检查唯一约束、外键、修订并发及同场景竞争发布。
5. 配置真实认证、Manage/Publish/Execute 授权、AppCode 数据范围、CORS 和异常日志；不沿用本地 Host 的自动授权。进程内直接调用应用服务时也需建立授权边界。
6. 根据业务流程构造 Context/Inputs；规则集按结果行组装业务对象，条件集按命中 Code 查业务参数。明确无命中、错误、重试和兜底，不把 HTTP 200 当成业务成功。
7. 使用实际同输入样本验证绑定裁决、顺序、空值、精度、回滚和业务状态，再完成环境验收。

运行服务预算可通过 `Configure<RuleExecutionOptions>` 设置 Timeout，默认两秒。该预算属于宿主配置，不是单次请求参数。已知运行中调用方取消传播、前端查询及其他缺口见[产品说明](docs/product/features.md#10-未实现项与后续边界)。

本仓库未提供业务系统集成包、生产 DbMigrator 或生产配置；也没有旧表、旧 AST、旧哈希兼容实现。生产接入及历史数据保留需求应在实际宿主中明确验证。
