# NRuleEngine 编码规范

适用范围：本项目 `src`、`test` 中的 C# 代码。新增代码直接遵守；整理存量代码时按功能范围逐步调整，并保持原有行为。业务边界以已接受的 ADR 和设计文档为准。

## 1. 分层与依赖

| 项目 | 职责 |
| --- | --- |
| `NRuleEngine.Domain.Shared` | 跨层共享的常量、枚举和错误码等，不放业务实现。 |
| `NRuleEngine.Domain` | 聚合、实体、值对象、领域服务、仓储接口和业务不变量。 |
| `NRuleEngine.Application.Contracts` | 应用服务接口、输入和输出 DTO、权限定义；不依赖应用实现。 |
| `NRuleEngine.Application` | 实现应用服务，编排用例、调用领域能力并转换 DTO。 |
| `NRuleEngine.EntityFrameworkCore` | DbContext、实体映射和仓储实现。 |
| `NRuleEngine.HttpApi` | HTTP 入口、路由和协议处理；业务逻辑交给应用服务。 |

- 应用服务接口放在 `Application.Contracts`，继承 `IApplicationService`，名称以 `AppService` 结尾；实现放在 `Application`，继承 `ApplicationService`。服务边界使用 DTO，不直接暴露领域实体。
- 应用服务的公开方法使用 `virtual`，内部辅助步骤使用 `protected virtual`。复杂数据查询放在专用仓储中，不在应用服务里直接编写数据库查询或操作 EF DbContext。
- 业务规则优先由领域层表达；控制器和 DTO 不承载业务判断。增加新的跨层引用前检查现有模块依赖，避免领域层依赖应用层或 HTTP 层。
- 通过依赖注入获取服务。缓存、后台任务和额外映射库只在当前用例确有需要时引入。

## 2. 目录、命名空间与命名

- 按业务功能建目录，例如 `RuleModels`、`Scenarios`。命名空间由**项目名称 + 项目内相对目录**组成，不把所有文件都放在 `NRuleEngine` 根命名空间。
- 例如，`NRuleEngine.Application.Contracts/RuleModels/IRuleModelAppService.cs` 使用 `NRuleEngine.Application.Contracts.RuleModels`；`NRuleEngine.Application/RuleModels/RuleModelAppService.cs` 使用 `NRuleEngine.Application.RuleModels`。项目根目录的模块类直接使用项目名称作为命名空间。
- 使用文件级命名空间；`using` 放在命名空间之前，`System` 系列优先。移动目录或类型时同步调整引用，并检查依赖完整类型名的反射、序列化和配置。
- 一个文件只放一个主要公开类型，文件名与类型名一致。接口以 `I` 开头，DTO 以 `Dto` 结尾，应用服务以 `AppService` 结尾。
- 类型、公开成员和常量使用 `PascalCase`；局部变量使用 `camelCase`，私有字段使用 `_camelCase`。使用有业务含义的名称；仅在右侧类型清楚时使用 `var`。

建议结构：

```text
src/
├── NRuleEngine.Application.Contracts/RuleModels/
│   ├── IRuleModelAppService.cs
│   ├── CreateRuleModelDto.cs
│   └── RuleModelDto.cs
├── NRuleEngine.Application/RuleModels/RuleModelAppService.cs
├── NRuleEngine.Domain/RuleModels/RuleModel.cs
└── NRuleEngine.EntityFrameworkCore/RuleModels/RuleModelDbContextModelConfig.cs
```

## 3. 契约层与 DTO

- 每个公开应用服务接口和每个公开 DTO 分别成文件，放在对应业务目录。不要用 `*Contracts.cs` 集中放置多种 DTO 和接口。确有紧密关联的私有辅助类型可留在同一文件。
- 接口只声明服务契约；DTO 只表达传输数据和必要的输入校验，不执行领域业务逻辑。请求和响应类型各自表达清楚语义。
- DTO 成员使用公开的 `get; set;` 属性。输入 DTO 在属性上使用适用的 Data Annotations，例如 `[Required]`、`[StringLength]`；跨字段校验确有需要时可实现 `IValidatableObject`。
- 根据模型需要选择 ABP 的 DTO 基类；有 ID、审计字段或扩展属性需求时复用相应基类。DTO 标记 `[Serializable]`。

```csharp
// Application.Contracts/RuleModels/CreateRuleModelDto.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace NRuleEngine.Application.Contracts.RuleModels;

/// <summary>
/// 创建规则模型的输入参数。
/// </summary>
[Serializable]
public class CreateRuleModelDto
{
    /// <summary>
    /// 所属应用编码。
    /// </summary>
    [Required]
    public string AppCode { get; set; } = string.Empty;
}
```

## 4. 方法、控制流与排版

- 方法使用带 `{ }` 的方法体，包括只有一条返回语句的方法；不使用表达式体方法。LINQ 集合操作、委托和配置回调可以使用 Lambda。
- `if`、`else`、`for`、`foreach`、`while` 等控制流始终使用花括号，即使分支只有一行；花括号独占一行。
- 遵守项目 `.editorconfig`：UTF-8、LF、4 个空格缩进。简短语句、参数列表和条件保持在一行；只有行明显过长、嵌套条件难读或调用链包含多个步骤时才分行，并按语义分组。
- 类、方法和主要逻辑段之间使用必要的空行，不连续插入空行。方法过长时提取有业务含义的步骤，不为减少行数而拆成无语义的小方法。
- 可以使用模式匹配、空条件运算符等 C# 语法，但以表达清楚业务意图为前提。格式修改只覆盖任务涉及的代码，不批量转换无关文件的编码或换行符。

```csharp
public virtual async Task<RuleModelDto> ArchiveAsync(Guid modelId, ArchiveRuleModelDto input)
{
    var model = await _ruleModelRepository.GetAsync(modelId);
    if (model.ConcurrencyStamp != input.ExpectedModelConcurrencyStamp)
    {
        throw new BusinessException(NRuleEngineErrorCodes.ConcurrencyConflict);
    }

    model.Archive();
    await _ruleModelRepository.UpdateAsync(model);
    return ObjectMapper.Map<RuleModel, RuleModelDto>(model);
}
```

## 5. 注释、异常与日志

- 公开接口、DTO、领域对象、应用服务和业务含义不直观的公开方法使用准确的中文 XML 注释，说明用途、约束或返回含义。参数和返回值有特殊含义时补充 `<param>`、`<returns>`。
- 关键业务规则、状态流转、并发前提及非显然的技术取舍，注释说明“为什么”；不逐句翻译自明代码。修改实现时同步更新注释。
- 可预期的业务失败使用项目错误码和 ABP `BusinessException`；异常不用于正常控制流。日志使用结构化字段并保留异常上下文，不记录凭据、连接串或完整敏感数据。

## 6. API、性能与测试

- HTTP 接口使用清晰的路由和状态语义，鉴权沿用 ABP 与接入宿主的现有机制。当前组件不自带 Host；认证协议、全局中间件、CORS、API 版本化和 Swagger 由有需求的宿主配置。
- I/O 使用异步 API；异步方法以 `Async` 结尾。查询注意避免 N+1；大结果集按接口语义分页。缓存应有明确的读取特征和失效规则。
- 行为变更使用现有 xUnit 项目补充必要的单元或集成测试；需要模拟依赖时沿用测试项目现有做法，不为套用规范引入新的测试或 mocking 框架。
- 运行受影响项目的构建和相关测试，并核对最终差异中没有无关格式变化。

## 7. 编码自查

1. 目录、命名空间、类型名和文件名是否一致，跨层引用是否符合模块边界？
2. 应用服务接口与 DTO 是否已拆分到各自文件，输入校验和输出语义是否清楚？
3. 方法是否使用方法体，控制流是否都有花括号，换行是否只发生在较长或复杂的逻辑处？
4. 公开契约和关键业务规则是否有准确注释，错误码、日志和权限是否符合现有约定？
5. 是否完成与改动相称的构建和测试，并检查最终差异？
