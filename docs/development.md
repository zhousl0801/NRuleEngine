# 迭代开发与单向同步

## 开发入口

个人 GitHub 仓库是唯一功能开发源。源码、测试、文档、原型和公共构建配置在本仓库修改并验证，然后提交并推送个人仓库。集成仓库使用 Git subtree 将整个已提交版本引入其模块目录；不要在集成仓库的模块副本中单独修改功能。

当前目录为 `src/`、`test/`、`docs/`、`prototype/`，构建入口为根目录 `NRuleEngine.sln`。原 `back-end/` 层已移除；GitHub Actions、Compose、Dockerfile 和文档均使用当前目录。Git 历史保留，Git 可根据内容识别移动。

## 验证与版本

在项目根目录运行 README 中的还原、Release 构建、测试和打包命令。版本在 `common.props` 中维护；发布前更新版本，或由集成流水线指定 `PackageVersion`。不要将本机数据库、构建产物、NuGet 认证配置或公司凭据提交到个人仓库。

## 集成仓库边界

`Directory.Build.props` 自动加载模块目录外的 `../../build/nrule-engine/Company.props`（存在时）；也可通过 MSBuild 的 `NRuleEngineCompanyProps` 属性指定路径。该文件由集成仓库维护，用于覆盖包作者、仓库地址、公司版本等元数据，不属于 subtree 内容。NuGet 配置由环境或 CI 提供，同步流程不生成或复制 NuGet 配置。

第一次建立 subtree 关系需要已提交的统一目录版本，且公司模块副本必须与该版本的文件树完全一致。集成仓库应先提交目录及外部配置准备，再使用其同步工具初始化；初始化和后续 subtree 更新都会创建本地 Git 提交，推送另行执行。仅复制文件不会建立 subtree 关系。

后续按“个人项目开发、验证、提交、推送 → 集成仓库拉取 subtree → 检查差异与包元数据 → 推送集成仓库”迭代。同步前工作区必须干净；公司配置留在模块目录之外。同步工具只支持单向引入，禁止通过 subtree push 将公司仓库内容反向推到个人仓库。
