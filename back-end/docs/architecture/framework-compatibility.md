# .NET 8 与 ABP 8 兼容基线

> 当前实现；2026-10-16。

组件所有模块、本地 Host 和测试统一目标框架为 net8.0，ABP 包固定为 8.0.4，EF Core SQLite 为 8.0.4。依赖继续集中在 Directory.Packages.props，目标框架由 Directory.Build.props 统一指定。这样可供现有 .NET 8 / ABP 8.0.4 宿主引用；本次不实现 ODS 模块装配、场景贡献者、迁移和数据适配。

global.json 使用 .NET 8.0.400 SDK 基线并允许同主版本最新功能带。解决方案改为 SDK 8 可读取的 NRuleEngine.sln；Docker 构建和运行镜像均为 8.0，CI 使用同一 global.json 和解决方案。C# 使用 SDK 的稳定默认版本，不启用 Preview。

原有 JsonElement.DeepEquals 由 RuleJsonComparer 替代。比较对象时忽略属性顺序，比较数组时保留顺序；数值归一为有效数字和十进制指数后精确比较，支持等值的科学计数法，不因 decimal 转换的舍入或下溢把不同数值判为相等。规则计算仍使用 decimal。门禁仍验证完整结构与预期结果，未降低检查要求。

ABP 8 对仅修改子集合的修订不会自动产生聚合根更新。EfCoreRuleModelRevisionRepository.UpdateAsync 在调用基础仓储后显式标记 ConcurrencyStamp 为修改，让保存过程生成新戳并使用原始值作并发检查。此步骤保证规则、赋值、变量和门禁仍共享修订并发边界；不能以降低过期编辑断言的方式适配框架。

在 back-end 执行：

```bash
dotnet restore NRuleEngine.sln
dotnet build NRuleEngine.sln --no-restore
dotnet test NRuleEngine.sln --no-build --no-restore
```

本地可使用新的临时 SQLite 数据库完成 Host 验证。EnsureCreated 只负责新库，不能升级旧开发库。接入生产宿主时应引用所需的分层模块、装配权限和 EF 配置，并由宿主生成及审核数据库迁移；当前本地 Host 的开发身份、自动授权和 SQLite 配置不属于正式接入方案。

## 本次验证记录（2026-10-16）

- 使用 SDK 8.0.420 还原并构建完整解决方案，0 警告、0 错误。
- net8.0 测试共 85 项通过：Application 23、Domain 59、EntityFrameworkCore 3，无失败或跳过。覆盖收集隔离、排序与关联、无命中、后续计算失败、结果限制、门禁、复制、发布、回滚及修订并发。
- 提交前复核通过回归测试复现 decimal 数值比较的下溢误判（`1e-29` 被视为 `0`），改用精确数值比较后回归通过；补充 9 个数值比较用例及 1 个真实门禁拒绝用例。
- 本机回环地址上的 .NET 8 Host 使用独立临时 SQLite，经真实 HTTP 完成目录查询、规则与赋值编制、两行试算、正负门禁发布、执行及 NotMatched 空结果读回；区间保持原生 JSON 对象。验证后停止 Host，未修改已有开发数据库。
- 浏览器验证原型的多命中两行、未命中空数组和 FirstMatch 单行预览；原型仍是独立演示，不作为服务端数值精度证明。
- 未执行 Docker 镜像构建、CI 远程运行、ODS 模块装配或真实订单对照。
