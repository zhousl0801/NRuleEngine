# NRuleEngine

NRuleEngine 是供 ABP 应用引用的规则编制与运行组件。当前仓库包含后端实现、测试、设计文档和用于联调的本地 Host；前端尚未接入。

后端入口：[back-end/README.md](back-end/README.md)。

## Docker 本地联调

在仓库根目录运行：

```bash
docker compose up --build -d
```

接口地址为 `http://127.0.0.1:5088`，例如 `GET /api/nrule-engine/catalog/apps`。SQLite 数据保存在 Compose 命名卷中，执行 `docker compose down` 后仍会保留。查看日志用 `docker compose logs -f nrule-engine`。Host 使用自动授权的开发身份，端口仅映射到本机回环地址，不要改为对外网开放。

```bash
cd back-end
dotnet restore NRuleEngine.sln
dotnet build NRuleEngine.sln --no-restore
dotnet test NRuleEngine.sln --no-build --no-restore
```

本仓库不包含具体业务系统的生产规则配置、生产数据快照或内部盘点文档。
