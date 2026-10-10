# 草稿编制 AST v1

> 状态：当前实现格式；更新于 2026-10-10

规则条件与表达式赋值通过 `ConditionAstJson`、`ExpressionAstJson` 提交 JSON 文本。`ConditionSource`、`ExpressionSource` 仅供展示；AST 是类型检查、引用分析和试算的权威内容。首版不解析自由文本表达式。节点引用变量时只传 `StableKey`，不使用可改名的 `Code`。

```json
{
  "version": 1,
  "root": {
    "op": "gt",
    "args": [
      { "op": "variable", "stableKey": "11111111-1111-1111-1111-111111111111" },
      { "op": "constant", "kind": "Decimal", "value": 100 }
    ],
    "start": 0,
    "length": 12
  }
}
```

`start` 和 `length` 是可选的源码字符位置，校验或试算出错时尽可能随问题返回。`constant` 需要 `kind`（Decimal、String、Boolean、Enum）和类型匹配的 JSON `value`；空值使用 JSON `null`。未知版本、操作符、函数、变量或类型不匹配会被拒绝。数值计算使用 `decimal`，变量的精度与小数位在赋值和输入时校验。

| 节点 `op` | `args` | 结果 |
|---|---|---|
| `constant`、`variable` | 无 | 声明类型或变量类型 |
| `and`、`or`、`not` | 2、2、1 | Boolean；`and`/`or` 短路 |
| `eq`、`ne`、`lt`、`lte`、`gt`、`gte` | 2 | Boolean；顺序比较只支持 Decimal/String |
| `add`、`sub`、`mul`、`div` | 2 | Decimal |
| `isNull` | 1 | Boolean |
| `in` | 值 + 至少一个同类型候选 | Boolean |
| `function` | 由 `function` 指定 | 白名单见下文 |

函数白名单：`Abs(decimal)`、`Min(decimal, decimal)`、`Max(decimal, decimal)`、`Round(decimal, decimal位数)`、`Coalesce(同类型, 同类型)`。节点不执行任意方法、反射、网络、文件或数据库操作。

赋值使用 `ValueSourceKind` 选择 `Constant` 或 `Expression`：常量提供 `ConstantValueJson`，表达式提供 `ExpressionAstJson`。绑定的 `TypedValueJson` 对 `Equals` 等单值操作符是 JSON 标量，对 `In` 是非空 JSON 数组；同一绑定中的维度条件按 AND 组合。试算请求的 `ContextJson` 和 `InputsJson` 是编码到 JSON 值文本的字典，例如 `"inputWidth": "120"`、`"productKind": "\"A\""`。

整份校验覆盖必需输出、同级顺序、循环依赖、重复写入和同修订内可判定的绑定歧义。发布和回滚会结合其他活动修订检查跨模型绑定冲突。试算只针对指定草稿，返回绑定、规则命中、实际赋值和出口，不持久化 Trace。当前已实现发布、回滚和活动修订执行的组件逻辑及 HTTP API；宿主数据库迁移、鉴权和 HTTP 联调尚未执行。
