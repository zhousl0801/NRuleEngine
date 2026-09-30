# NRuleEngine

NRuleEngine 是供 ABP 应用引用的规则编制与运行组件。当前仓库包含后端实现、测试和设计文档，尚未提供独立运行的 Host 或前端。

后端入口：[back-end/README.md](back-end/README.md)。

```bash
cd back-end
dotnet restore NRuleEngine.slnx
dotnet build NRuleEngine.slnx --no-restore
dotnet test NRuleEngine.slnx --no-build --no-restore
```

本仓库不包含具体业务系统的生产规则配置、生产数据快照或内部盘点文档。
