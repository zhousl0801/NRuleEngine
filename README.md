# NRuleEngine

供 ABP 应用引用的通用规则编制与运行组件，采用 .NET 8 / ABP 8.0.4。当前实现规则集和独立条件集的草稿编制、校验、门禁发布、回滚与执行；提供本地联调 Host 和独立交互原型。

个人仓库是功能开发源；集成仓库通过单向 Git subtree 引入已提交的版本。开发与同步约定见[迭代开发](docs/development.md)。

## 文档入口

| 文档 | 内容 |
| --- | --- |
| [产品功能说明](docs/product/features.md) | 产品对象、功能流程、执行语义、已知缺口及后续范围 |
| [系统架构说明](docs/architecture/overview.md) | 模块、领域模型、调用链、事务并发、存储与宿主边界 |
| [API 参考](docs/reference/api.md) | 当前路由、权限、请求格式、结果及错误协议 |
| [表达式与值结构](docs/reference/expressions.md) | AST、类型、函数、区间与列表及资源限制 |
| [后端开发与接入](BACKEND.md) | 构建、测试、本地启动、演示契约及集成步骤 |
| [交互原型](prototype/README.md) | 预览方法、交互说明和服务端差异 |

首版范围是后端组件与 API。原型尚未连接后端，正式编制台和业务系统接入尚未完成；当前未实现项集中列于产品说明，不能将原型操作视为服务端写入。

## 快速验证

```bash
dotnet restore NRuleEngine.sln
dotnet build NRuleEngine.sln --no-restore
dotnet test NRuleEngine.sln --no-build --no-restore
```

本机启动后端：

```bash
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet run --project src/NRuleEngine.Host
```

访问 `http://127.0.0.1:5088/api/nrule-engine/catalog/apps`。也可在本项目根目录（作为模块引入时为该模块目录）运行 `docker compose up --build -d`。Host 自动授予开发身份和权限，只用于本机联调；Compose 端口仅映射到回环地址，SQLite 保存在命名卷中，执行 `docker compose down` 后仍保留。

仓库不包含业务系统生产规则、生产数据快照或内部盘点资料。

## NuGet 打包

在本项目根目录执行（作为模块引入时为该模块目录）：

```bash
dotnet restore NRuleEngine.sln
dotnet build NRuleEngine.sln -c Release --no-restore
dotnet test NRuleEngine.sln -c Release --no-build --no-restore
dotnet pack NRuleEngine.sln -c Release --no-build --no-restore -o artifacts/packages
```

产物为六个 `NRuleEngine.*` 分层组件包，Host 和测试项目不打包。通用包版本和元数据由 `common.props` 维护，可用 `-p:PackageVersion=版本号` 覆盖。集成仓库专用参数使用外部配置，NuGet 认证配置由对应开发环境或 CI 提供，不纳入本项目。
