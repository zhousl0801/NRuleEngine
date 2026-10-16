# 执行模式与统一结果行

> 当前实现；2026-10-16。场景与发布修订仍按精确 ContractVersion 关联。本次不实施场景定义升级计划，也不包含 ODS 适配。

## 场景契约

`RuleScenarioDescriptor.ResultCardinality` 由宿主注册，默认 `Single`。修订保存其快照，参与复制、EF 持久化和内容哈希；校验及运行要求快照与注册契约一致。新建 `Many` 草稿自动使用 `CollectMatches`。

| 结果基数 | 允许模式 | 成功结果 |
| --- | --- | --- |
| Single | FirstMatch、Sequential | 一行完整出口 |
| Many | CollectMatches | 1–100 行完整出口 |

FirstMatch 按 Priority 降序、SortOrder 升序执行，第一条命中后停止。Sequential 共享上下文，保留原有中间变量依赖及多次写入策略，所有已命中规则共同形成一行。CollectMatches 只收集已选模型内的规则结果；跨模型仍由唯一最高优先级绑定裁决。

## CollectMatches

每条已启用规则都从同一份入口及中间变量默认值开始求值。前一条规则的条件和赋值不会改变后一条规则的输入。规则条件只能读取入口或有默认值的中间变量；赋值可读取本行较早赋值的单值变量，编制校验拒绝跨规则依赖和本行未初始化读取。

每条命中规则必须独立生成所有必需出口。行内赋值顺序继续采用 SortOrder；同一规则内重复赋值目标仍被禁止。不同规则写同名出口会产生不同行，不应用 FirstWriteWins 或 LastWriteWins；收集模式仅接受 ErrorOnMultipleWrite 配置。

所有命中行按规则 Priority 降序、SortOrder 升序返回，不自动去重、不做跨行合并。两套规格可以具有相同宽度，仍须保留各自厚度、公差等对应关系。列表出口是一条赋值内的值结构，与跨规则结果行是两个不同概念。

## API 与对象组装

执行和试算都返回 `Results: RuleResultRowDto[]`，不再返回顶层 Outputs 或 OutputsJson。每行包含 SourceRuleIds 和原生 JSON 的 Outputs。FirstMatch 的来源为一条规则，Sequential 的来源包含按执行顺序排列的全部命中规则，CollectMatches 每行来源为一条规则。

```json
{
  "results": [
    {
      "sourceRuleIds": ["11111111-1111-1111-1111-111111111111"],
      "outputs": {
        "inWidth": 1020,
        "widthTolerance": {"min": -2, "max": 3},
        "inThickness": 210,
        "thicknessTolerance": {"min": -1, "max": 1},
        "inLength": 0,
        "lengthTolerance": {"min": 0, "max": 0}
      }
    }
  ]
}
```

上例是宿主可以注册的尺寸契约，不是核心内置的 ODS 类型。每行的公称值使用 Decimal，公差使用 NumericRange，宿主按行组装业务对象。核心不引入任意 Object 或 List<Object>，也不规定长度零值、收得率、首条继续推导和订单成功状态的业务含义。NumericRange 为公差偏差时，宿主自行将其加到公称值，不能误当成绝对尺寸范围。

## 无命中、错误与资源边界

已选中模型而没有规则命中时，运行状态为 NotMatched，错误码为 `NRuleEngine:Evaluation:NoMatchingRule`。试算返回相同 Status，Success=false，内部诊断 ErrorCode 为 NoMatchingRule。没有绑定命中仍用 NoMatchingBinding。

任何条件或赋值计算失败、缺少出口、结果超限或调用取消，都不会返回部分 Results。试算可保留已执行的条件和赋值诊断，它们不是可消费的业务结果。核心不自动忽略失败规则或回退旧修订。

每次成功执行最多 100 行。按 `RuleResultRows.Serialize` 生成的 results 数组最多 65536 UTF-8 字节，包含来源标识、字段名和原生出口值，不包含执行关联 ID 等顶层元数据。此确定性序列化采用 System.Text.Json 默认转义。超过任一边界返回 Failed / `NRuleEngine:Output:ResultLimitExceeded`，不得静默截断。

## 发布门禁

`ExpectedResultsJson` 为有序的出口对象数组，不包含来源规则 ID 或 outputs 包装：

```json
[
  {"inWidth": 1020, "widthTolerance": {"min": -2, "max": 3}},
  {"inWidth": 1040, "widthTolerance": {"min": -1, "max": 2}}
]
```

`ExpectedStatus` 仅允许 Succeeded 或 NotMatched。成功预期必须为 1–100 个对象，未匹配预期必须为 `[]`；修订至少包含一条成功样本。失败状态不能被设置为预期以放行发布。比较状态、行数、行顺序及全部出口，对象字段顺序不影响比较，数值 1 与 1.0 相等，列表顺序仍有意义。重复 JSON 字段会被拒绝。

规则 ID 在复制修订时重建，因此只作执行来源追踪，不写入门禁预期。结果基数、执行模式、规则顺序、出口形状、门禁预期状态和结果均进入内容哈希。新草稿的门禁在本修订重新求值，不能复用旧的通过标记。

## 数据和验证边界

新增 NreRuleModelRevisions.ResultCardinality，NreReleaseGateCases 改用 ExpectedResultsJson 并增加 ExpectedStatus。当前组件未被业务系统集成，不提供旧 JSON、旧哈希或旧表结构兼容分支。已有本地开发库须由开发者明确重建，或使用新的临时库；接入宿主负责按 EF 模型生成迁移，不能依赖 EnsureCreated 更新已有表。

领域测试覆盖行隔离、顺序、对应关系、重复值、缺项、无命中、后续失败与数量/大小限制；应用测试覆盖存储读回、统一 HTTP 序列化、发布、草稿复制、负样本和回滚。真实 ODS 排序、舍入与业务状态对照属于后续适配验收。
