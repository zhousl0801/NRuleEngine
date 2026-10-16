# 区间与候选列表输出

> 当前实现；2026-10-14。项目处于开发阶段，尚无系统集成，本次直接更新类型、存储和内容哈希，不提供历史数据兼容分支。

## 契约

基础类型继续使用 `RuleValueKind`：Decimal、String、Boolean、Enum。`RuleTypeDescriptor.Shape` 定义值结构，默认 Scalar，可声明 NumericRange 或 List。复合结构仅用于场景契约出口，入口、条件场景输入和用户中间变量仍为单值。维护者不能改变契约出口的类型和形状。

- NumericRange：Kind 必须为 Decimal；值只能包含数值 min 和 max，包含两端，满足 min <= max。两个端点必填、可相等、可为负值，采用相同的精度和小数位约束。
- List：Kind 声明所有候选项的基础类型；Enum 使用同一个 EnumCode。按编制顺序返回 1–100 项，禁止 null、空白文本和重复值。数值 1 与 1.0 视为重复；文本比较区分大小写，不自动去空格或排序。
- 复合出口不可声明为可空；空列表不表示成功。
- 常量 JSON 最多 4096 字符；表达式 JSON 最多 65536 字符，单值子表达式深度最多 64。列表是一个完整赋值，不自动收集多条规则的结果。

注册示例：

```csharp
new VariableContract("widthRange", "宽度范围", RuleVariableDirection.Output,
    new RuleTypeDescriptor(RuleValueKind.Decimal, Precision: 12, Scale: 2,
        Shape: RuleValueShape.NumericRange));
new VariableContract("candidateCodes", "候选编码", RuleVariableDirection.Output,
    new RuleTypeDescriptor(RuleValueKind.String, Shape: RuleValueShape.List));
```

## 常量和计算型赋值

沿用 ValueSourceKind=Constant/Expression、ConstantValueJson、ExpressionAstJson。全部为常量时直接提交对象或数组：

```json
{"min": 120, "max": 126}
```

```json
["B", "A"]
```

端点或候选项需要计算时使用 AST v1 的赋值根节点 range 或 list。每个参数是现有单值表达式，可组合类型化常量、稳定键变量和白名单算术函数，因此常量和公式可以混合。range.args 按最小值、最大值排列；list.args 顺序就是候选顺序。节点类型由目标出口契约决定。以下 key 仅为示例：

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

复合根节点只能用于对应出口赋值，不能嵌套到单值公式或布尔条件中；单值 AST 不能引用复合出口。区间或列表整体由一条赋值生成，不支持分别写入端点、追加元素或混合类型列表。变量引用和删除影响分析遍历所有子表达式。

## 保存、发布和运行

常量保存时检查完整结果；表达式保存时检查根结构、子表达式类型及引用。运行时再次检查实际端点顺序、数值精度、空值、重复候选和数量；依赖输入的问题通过试算和发布门禁验证。每条 FirstMatch 或 CollectMatches 规则均需生成全部必需出口。

发布门禁保存完整预期 JSON，要求值和结构一致、列表顺序一致。出口形状参与内容哈希，修订复制保留形状与 StableKey。运行结果返回原生 JSON；草稿试算使用相同 Results 行结构：

```json
{
  "results": [{
    "sourceRuleIds": ["11111111-1111-1111-1111-111111111111"],
    "outputs": {
      "widthRange": {"min": 120, "max": 130},
      "candidateWidths": [130, 120],
      "candidateCodes": ["B", "A"]
    }
  }]
}
```

执行方式增加 CollectMatches，各条命中的完整出口形成独立结果行；列表类型本身不引入追加或聚合语义。多行执行与门禁 ExpectedResultsJson 格式见[执行结果契约](execution-results.md)。输出校验失败沿用 AssignmentFailed → ExpressionEvaluationFailed，响应包含赋值路径及可用的源码位置。

## 本地接入和验证

Host 注册 DEMO/size-candidates/1.0：输入 inputWidth；出口 widthRange、candidateWidths、candidateCodes；绑定维度 productKind。可用现有模型、赋值、绑定、门禁、发布和执行接口完成联调。

EF 在 NreVariableDefinitions 中保存 Shape。当前本地 Host 使用 EnsureCreated，已有开发库不会自动增加列；可使用新建临时数据库验证，或由开发者明确重建开发库。本次验证涵盖非法区间、列表类型和精度、数量边界、运行时重复值、子表达式引用、修订复制、门禁顺序及发布执行。
