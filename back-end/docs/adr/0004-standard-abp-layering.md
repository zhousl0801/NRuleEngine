# ADR-0004：采用标准 ABP 分层与模块依赖方式

- **状态**：已接受
- **日期**：2026-09-21
- **关联**：系统设计草案 §3 仓库结构、§4 ABP 模块依赖、§11 技术验收标准

> 2026-09-23 补充：标准 ABP 分层的决策继续有效。原草案一次创建 17 个项目的范围已由 [ADR-0014](0014-minimal-module-skeleton.md) 调整为分阶段建立项目；以下上下文保留当时的设计记录。

## 上下文

仓库按 ABP 标准分层组织为 11 个 `src` 项目和 6 个 `test` 项目（系统设计 §3）。其中 `Expressions` 与 `Runtime` 各拆成一对 `Abstractions` + 实现项目，其余为常规 ABP 分层。

模块依赖是一条单向链（系统设计 §4）：

```
Domain.Shared → Domain → Application.Contracts → Application
Domain → EntityFrameworkCore
Application.Contracts → HttpApi → HttpApi.Client
```

宿主按需引用模块：Domain 引用 `NRuleEngineDomainModule`、Application 引用 `NRuleEngineApplicationModule`、EntityFrameworkCore 引用 `NRuleEngineEntityFrameworkCoreModule` 并实现组件 DbContext 接口、HttpApi 引用 `NRuleEngineHttpApiModule`（系统设计 §4）。

技术验收标准要求「所有层以 ABP Module 暴露，示例测试宿主可完成模块装配」（系统设计 §11）。

> **待补**：
> - `Expressions` 和 `Runtime` 单独拆成 `Abstractions` + 实现，是要让这两块可替换（换表达式引擎、换执行器）吗？如果只是分层习惯，为什么其他模块没有同样的 `Abstractions`？
> - `HttpApi.Client` 是否作为默认包发布，还是等出现远程调用需求再加？——系统设计 §12 第 6 条，与本决策直接相关。

## 决策

采用标准 ABP 分层和模块依赖方式。

## 考虑过的替代方案

> **待补**：是否评估过以下方向，以及为什么没选：
> - 按功能垂直切分（feature folders），减少项目数量；
> - 只保留 Domain / Application / EntityFrameworkCore 三层，去掉 HttpApi 与 Contracts 的拆分；
> - 以 NuGet 包而非源码项目形式交付各层。

## 后果

- 分层与依赖方向固定，宿主按 ABP Module 装配，与现有 QMS / ODS 的装配方式一致。
- 项目数量较多（17 个），构建与版本管理成本相应上升。
- `Expressions` / `Runtime` 的 `Abstractions` 边界一旦对外暴露，就成为需要维护的兼容性契约。

> **待补**：`Abstractions` 项目是否承诺对外稳定（是否允许破坏性变更）。
