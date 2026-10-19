# 表达式与值结构参考

## 1. AST v1

条件通过 ConditionAstJson，表达式赋值通过 ExpressionAstJson 提交 JSON **文本**。ConditionSource/ExpressionSource 只供展示，服务端不解析自由文本表达式。节点中的变量引用使用 Guid StableKey，不使用可重命名的 Code。

```json
{
  "version": 1,
  "root": {
    "op": "gt",
    "args": [
      {"op": "variable", "stableKey": "11111111-1111-1111-1111-111111111111"},
      {"op": "constant", "kind": "Decimal", "value": 100}
    ],
    "start": 0,
    "length": 12
  }
}
```

start/length 是可选源码字符位置，校验及求值错误尽可能返回。constant 声明 kind 和匹配的 JSON value；空值使用 JSON null。未知版本、操作符、函数、变量或不匹配的类型均被拒绝。

| op | 参数 | 结果与限制 |
| --- | --- | --- |
| constant | kind、value | Decimal、String、Boolean、Enum 单值 |
| variable | stableKey | 引用已声明单值变量 |
| and、or、not | 2、2、1 个 args | Boolean；and/or 短路 |
| eq、ne、lt、lte、gt、gte | 2 个 args | Boolean；顺序比较仅 Decimal/String |
| add、sub、mul、div | 2 个 args | Decimal |
| isNull | 1 个 arg | Boolean |
| in | 值及至少一个同类型候选 | Boolean |
| function | function 名及 args | 固定白名单 |
| range | 两个 Decimal 端点 | 仅区间出口赋值根节点 |
| list | 1–100 个同类型单值 | 仅列表出口赋值根节点 |

函数：Abs(decimal)、Min(decimal, decimal)、Max(decimal, decimal)、Round(decimal, decimal 位数)、Coalesce(同类型, 同类型)。Round 位数必须为 0–28 的整数，使用 AwayFromZero，例如 2.5 保留零位得到 3。Coalesce 按需求值。没有自定义函数贡献者，也不允许任意方法、反射、网络、文件或数据库访问。

## 2. 基础类型与默认值

RuleTypeDescriptor.Kind 为 Decimal、String、Boolean、Enum，另声明 IsNullable、Precision、Scale、EnumCode 和 Shape。数值使用 decimal，输入/赋值检查声明精度与小数位，不隐式转为 double。Enum 的编码是类型身份；当前没有枚举候选目录或自定义类型注册。

缺失输入有默认值则使用默认值，否则报缺失错误；显式 null 按可空性检查。中间变量默认值在规则执行前初始化，不计入重复写入次数。未初始化变量被读取时失败，复合变量不能用于单值公式或条件。

## 3. 赋值与值形状

ValueSourceKind=Constant 时提供 ConstantValueJson，Expression 时提供 ExpressionAstJson。TargetVariableStableKey 指向中间变量或出口；同规则内目标不可重复。排序使用赋值 SortOrder。

| Shape | 可用范围 | JSON 及约束 |
| --- | --- | --- |
| Scalar（默认） | 输入、中间变量、输出 | 符合 Kind 的单值 |
| NumericRange | 仅非空契约输出，Kind=Decimal | `{"min":120,"max":126}`；端点必填且包含两端，min ≤ max，采用相同精度约束 |
| List | 仅非空契约输出 | `["B","A"]`；1–100 个同类型非空元素，Enum 使用相同 EnumCode |

列表保留编制顺序，不排序或去重；空白文本、null、重复候选非法。数值 1 和 1.0 视为重复，文本区分大小写且不自动去空格。空列表不能表示成功。

宿主注册区间及列表的示例：

```csharp
new VariableContract("widthRange", "宽度范围", RuleVariableDirection.Output,
    new RuleTypeDescriptor(RuleValueKind.Decimal, Precision: 12, Scale: 2,
        Shape: RuleValueShape.NumericRange));
new VariableContract("candidateCodes", "候选编码", RuleVariableDirection.Output,
    new RuleTypeDescriptor(RuleValueKind.String, Shape: RuleValueShape.List));
```

这些类型来自 Domain.Shared.Rules。全部为常量时直接将对象或数组编码为 ConstantValueJson。动态端点/元素使用以下 AST；常量和公式可混合：

```json
{
  "version": 1,
  "root": {
    "op": "range",
    "args": [
      {"op": "variable", "stableKey": "11111111-1111-1111-1111-111111111111"},
      {"op": "add", "args": [
        {"op": "variable", "stableKey": "11111111-1111-1111-1111-111111111111"},
        {"op": "constant", "kind": "Decimal", "value": 10}
      ]}
    ]
  }
}
```

```json
{
  "version": 1,
  "root": {
    "op": "list",
    "args": [
      {"op": "constant", "kind": "Decimal", "value": 140},
      {"op": "variable", "stableKey": "11111111-1111-1111-1111-111111111111"}
    ]
  }
}
```

range 的 args 顺序为最小值、最大值，list 的 args 顺序为候选顺序。复合根节点不能嵌入标量公式，区间或列表整体由一条赋值生成，不分别写端点、不追加元素、不混合元素类型。引用分析遍历所有端点及元素表达式。

常量保存时校验完整值；表达式保存时检查根形状、子表达式类型和引用。运行时再次检查实际端点顺序、精度、重复元素及数量。结果无效会使整体执行失败，不交付部分行。

## 4. 执行顺序与结果行

规则按 Priority 降序、SortOrder 升序；同优先级顺序须唯一。赋值按 SortOrder 升序，同规则内顺序须唯一。

- FirstMatch：首条命中后停止，该规则必须覆盖全部出口。
- Sequential：共享上下文，后续条件与公式能读取先前赋值；实际命中路径最终必须补齐出口。ErrorOnMultipleWrite 静态拒绝潜在重复目标，FirstWriteWins 跳过后续赋值及公式，LastWriteWins 求值后覆盖。
- CollectMatches：每条规则从相同输入与中间变量默认值开始，条件不能读取其他规则结果；赋值只可读取初始值及本行已赋值单值。每条命中生成一行完整出口，不合并相同值。

后端和原型预览的三种模式在无规则命中时均为 NotMatched，结果为空；即使存在初始出口值也不生成业务行。已命中但出口缺失仍失败。运行与试算使用统一 Results 行，每行有 SourceRuleIds 和原生 JSON Outputs。一个 List 出口是同一行中的数组，与多条规则形成多行无关。核心不支持任意 Object 或 List<Object>。

## 5. 资源边界

| 对象 | 当前限制 |
| --- | --- |
| 常量 JSON 文本 | 最多 4,096 字符 |
| AST JSON 文本 | 最多 65,536 字符 |
| 单值表达式校验深度 | 最多 64 层；另受 JSON 反序列化深度限制 |
| 列表 | 1–100 项 |
| 运行结果 | 成功最多 100 行；结果数组最多 65,536 UTF-8 字节 |

结果字节数依据 RuleResultRows.Serialize 的确定性序列化，包含来源标识、字段名和原生值，不含 ExecutionId 等顶层元数据，使用 System.Text.Json 默认转义。超限报 ResultLimitExceeded，不截断。正式 decimal、类型与绑定语义以服务端为准，原型 JavaScript 计算不构成等价实现。

实现入口：[RuleExpressionEngine](../../src/NRuleEngine.Domain/Expressions/RuleExpressionEngine.cs)、[RuleValueEngine](../../src/NRuleEngine.Domain/Expressions/RuleValueEngine.cs)、[RuleSimulator](../../src/NRuleEngine.Domain/Simulation/RuleSimulator.cs)、[RuleResultRows](../../src/NRuleEngine.Domain/Simulation/RuleResultRows.cs)。
