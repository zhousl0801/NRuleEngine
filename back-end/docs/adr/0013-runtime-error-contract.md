# ADR-0013：运行 API 结构化结果、严格输入与取消协议

- **状态**：已接受
- **日期**：2026-09-21
- **关联**：运行错误与异常模型；产品设计草案 §8.2；系统设计草案 §9.4

## 上下文

NRuleEngine 同时被宿主内部服务和 HTTP API 使用。无匹配、输入错误和规则计算失败都是调用方需要稳定识别的确定性结果；数据库不可用和程序缺陷则属于系统异常。接口还需要避免调用方拼错输入编码后静默运行，并能同时响应宿主取消和引擎自身的资源预算。

## 决策

1. `Succeeded / NotMatched / Rejected / Failed` 均通过 `RuleExecutionResult` 返回；运行 HTTP API 对这些确定性结果统一返回 HTTP 200。
2. 场景契约未声明的额外输入返回 `NRuleEngine:Input:UnknownInput`，不静默忽略。
3. 执行同时监听调用方 `CancellationToken` 和 NRuleEngine 内部执行时间预算。
4. 调用方取消继续传播 `OperationCanceledException`；只有内部时间预算耗尽才转换为 `NRuleEngine:System:Timeout`。

## 考虑过的替代方案

- 将 `Rejected / Failed` 映射为 HTTP 4xx：符合资源 API 惯例，但内部调用与 HTTP 调用会产生两套处理协议，且执行失败详情容易被 ABP 异常包装稀释。
- 忽略额外输入：兼容性更宽松，但变量编码拼错时可能静默使用缺省路径。
- 只响应调用方 CancellationToken：实现简单，但无法由组件约束异常复杂规则的执行时间。
- 只使用组件超时：无法及时响应宿主请求终止和应用关闭。

## 后果

- ODS 必须读取 `RuleExecutionResult.Status`，不能只根据 HTTP 200 判断成功。
- HTTP 监控需要按结果状态统计业务失败，不能只依赖非 2xx 比例。
- 输入契约更加严格，新增输入必须先升级场景契约。
- 引擎需要 linked cancellation token，并准确区分调用方取消与内部超时。
- 具体默认执行预算仍需在性能测试后确定。

