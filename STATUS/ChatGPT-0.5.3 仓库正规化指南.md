# 0.5.3 仓库正规化指南

**User:** Anonymous  
**Created:** 8/4/2026 6:17:27  
**Updated:** 8/4/2026 6:33:08  
**Exported:** 8/4/2026 6:33:31  
**Link:** [https://chatgpt.com/c/6a7112b8-9c04-83eb-b6f6-5aa46c8fcd18](https://chatgpt.com/c/6a7112b8-9c04-83eb-b6f6-5aa46c8fcd18)  

## Prompt:
8/4/2026, 6:17:24 AM

接下来的 0.5.3 应该是一个文件夹/文件命名和结构完全 KISS 和轻量化，正规化的提交，涉及所有代码和非代码。现在的 evidence 和 package.json 各种目标和流程，有大量形式主义 yagni，代码有很多脚手架级别的问题。请你把项目整理成教科书级别的开源项目水平。给出 0.5.3 指南。

[Attachment: repomix-output(148).xml]

## Response:
8/4/2026, 6:17:27 AM

我会先把现有目录、`package.json`、验证脚本和 0.5.2 证据链对齐，重点识别哪些是发布必需、哪些只是形式主义，再给出可直接执行的 0.5.3 收敛指南。

已经确认一个核心问题：开发清单、发布清单和审计清单被压进了同一个 `package.json`，而发布包又维护第二份 package 模板，版本、依赖和入口存在双真值。下一步我会把“应保留的最小公共命令面”和“应下沉或删除的内部检查”分开。

> **撰写并编写 v0.5.3 发布与使用指南**
>
> 
>
> **检查并定位 XML 项目文件**
>
>
> **检查 XML 项目文件及指定章节并提取关键文件**
>
> 
>
> **统计仓库结构与关键脚本**
>
>
> **梳理依赖与脚本**
>
> 我会核对依赖是否确实被源码使用，再决定哪些只是候选移除项；同时注意项目缺少锁文件、安装时自动修改 Git 钩子，以及“lint”脚本实际会修改文件而非仅检查。
>
> **审查项目依赖与脚本配置**
>
>
> **完善项目配置**
>
> 我会继续检查跨生态配置：统一 .NET 与 Node 的版本声明，移除重复依赖和过度宽泛的安全审计关闭设置，并补充锁文件、编辑器规范及更清晰的开源项目文档。
>
> **完善了项目配置**
>
> 
>
> **完成仓库结构、依赖配置及规范化指南审查**
>
> 
>
> 思考了 3m 51s

我把 0.5.3 定义为一次**不改变产品语义的仓库正规化版本**：删除迁移期设施，统一构建、测试、发布与文档真值。当前仓库存在双 `package.json`、28 个 npm scripts、18 个自制脚本约 4300 行、庞大的逐版本 evidence，以及若干迁移遗留命名；以下方案基于完整仓库内容。`repomix-output.xml`

**document: **

# 万象术 0.5.3 仓库正规化指南

## 一、版本定位

0.5.3 的唯一主题：

> 把已经工作的 0.5.2，整理成结构简单、命名准确、构建可复现、对外界面克制的正式项目。

0.5.3 不新增产品能力，不修改 SSOT 语义，不增加 Agent，不设计新的验证框架，不引入新的持久化事实。

允许的变化：

- 文件和目录重命名；
- 删除迁移期、一次性和重复设施；
- 合并脚本、配置和文档真值；
- 拆分明显失控的大文件；
- 调整内部模块位置与名称；
- 简化测试入口和发布流程；
- 保持 `import "wanxiangshu"` 的公开行为不变。

所有 journal fact 名称、序列化格式和运行时目录格式默认冻结。结构整理不得伪装成协议迁移。

---

## 二、0.5.3 的硬原则

### 1. 一个事实只能有一个来源

必须消灭以下双真值：

- 根 `package.json` 与 `packaging/npm-package.template.json`；
- 根 `Directory.Build.props` 与项目内 `Directory.Build.props`；
- README 当前版本、STATUS 当前版本、evidence 当前版本；
- package manifest 中不同的依赖分类和 Node 要求；
- `test:mjs`、`test:unit` 等完全等价的命令别名。

版本只来自根 `package.json`。

依赖、入口、license、engines、files 和 repository 信息也只在根 `package.json` 中维护。

### 2. 公共命令面必须很小

贡献者只需要理解以下命令：

```text
npm run build
npm test
npm run test:e2e
npm run check
npm run check:release
npm run format
```

内部检查不再全部暴露成 `gate:*` npm scripts。

### 3. 检查器必须证明实际风险

保留一个自制 gate，至少要满足：

1. 它防止过真实缺陷；
2. 普通编译器、测试或格式化工具不能代替它；
3. 有一个会失败的最小 fixture；
4. 失败信息能直接告诉维护者如何修复。

不满足以上条件的检查器删除，不进入“暂时保留”区。

### 4. Git 历史不是产品目录

历史验证输出、旧状态、迁移报告和失败过程，不应永久占据主分支目录。

Git tag、CI artifact 和 Release attachment 才是历史保存设施。

### 5. KISS 不是把复杂度藏进一个大文件

合并的是公共入口和重复流程，不是把所有逻辑塞进一个 3000 行脚本。

---

## 三、目标目录

建议 0.5.3 收敛为：

```text
.github/
  workflows/
    ci.yml
    release.yml

docs/
  architecture.md
  development.md
  releasing.md
  rfcs/

spec/
  README.md
  01-*.md
  ...
  coverage.toml

src/
  Wanxiangshu/
    Plugin.fs
    Core/
    Domain/
    Application/
    Adapters/
    Processes/
    prompts/
    Wanxiangshu.fsproj

tests/
  unit/
  integration/
  e2e/
  support/

scripts/
  build.mjs
  check.mjs
  release.mjs
  checks/
    architecture.mjs
    package.mjs
    spec.mjs

.editorconfig
.gitattributes
.gitignore
CHANGELOG.md
CONTRIBUTING.md
Directory.Build.props
LICENSE
README.md
SECURITY.md
dotnet-tools.json
global.json
package-lock.json
package.json
```

### 目录迁移

```text
SSOT/                         → spec/
RFC/                          → docs/rfcs/
tests-mjs/                    → tests/unit/
testkit/opencode/             → tests/e2e/opencode/
testkit 其余公共设施          → tests/support/
src/Wanxiangshu.Next/         → src/Wanxiangshu/
```

以下目录从主分支删除：

```text
PENDING/
STATUS/
docs/evidence/
docs/archive/shock-anneal-2026/
packaging/
```

处理方式：

- `PENDING` 中仍有价值的内容转为 RFC 或 issue；
- `STATUS/conformance.toml` 简化为 `spec/coverage.toml`；
- `STATUS` 中的永久架构知识迁入 `docs/`；
- 原始 evidence 作为 Release/CI 附件保存；
- 历史迁移报告由 tag 和 Git 历史承担，不复制到当前源码树。

---

## 四、规范和 evidence 收敛

当前 conformance 体系记录状态、commit、生成 Markdown 和逐版本证据。对于已经 192/192 conformant 的稳定版本，这套流程不应继续作为常态开发框架。

保留一个最小的 `spec/coverage.toml`：

```toml
[PROMPT-001]
tests = ["tests/unit/prompt/authority.test.mjs"]

[EXEC-009]
tests = [
  "tests/unit/execution/handle.test.mjs",
  "tests/integration/execution/restart-join.test.mjs"
]
```

它只回答两个问题：

- 每个 active clause 是否有测试所有者；
- 被引用的测试文件是否存在。

删除以下状态字段：

```text
conformant
implementing
partial
verified_commit
gate_commit
evidence_path
```

这些字段不是产品语义，而且会在每次提交后立即陈旧。

每个正式版本只保留一个简短发布记录：

```text
docs/releases/0.5.3.md
```

内容最多包括：

- source commit；
- 支持的 Node/.NET 环境；
- 执行过的顶层命令；
- 测试数量和结果；
- npm tarball SHA-256；
- 已知限制。

完整 stdout、环境快照、安装日志和 tarball 放到 CI artifact 或 GitHub Release，不提交到源码仓库。

---

## 五、package.json 和发布包

### 1. 删除第二份 package manifest

删除：

```text
packaging/npm-package.template.json
```

直接从仓库根目录执行 `npm pack`。

构建产物统一写入：

```text
dist/
```

不要再使用：

```text
build/next/
```

把插件公开入口放到源码根部：

```text
src/Wanxiangshu/Plugin.fs
```

使发布入口成为：

```json
{
  "main": "./dist/Plugin.js",
  "exports": {
    ".": "./dist/Plugin.js"
  },
  "files": [
    "dist"
  ]
}
```

这样不需要 postbuild 复制另一份 package.json。

### 2. 删除隐式生命周期副作用

删除：

```text
prepare
postbuild
scripts/setup-git-hooks.mjs
```

安装依赖不应自动修改 `.git/hooks`。

CI 是质量底线，Git hook 只能是贡献者自愿启用的本地便利设施。

`postbuild.mjs` 当前还把整个 `testkit` 复制到 `build/`，而发布模板只打包 `next/`。这类没有进入最终产物的复制流程应直接删除。

### 3. 依赖重新分类

采用以下判据：

- 编译后的 `dist/` 会直接 import：`dependencies`；
- 由 OpenCode Host 提供：`peerDependencies`；
- 仅测试、构建或脚本使用：`devDependencies`；
- 运行时确实支持“存在则启用、缺失仍正常”：才允许 `optionalDependencies`。

优先审计：

```text
@modelcontextprotocol/sdk
opencode-pty
yaml
eventsource
@kreuzberg/tree-sitter-language-pack
es-module-lexer
highlight.js
tsx
```

它们在当前源码中的直接使用证据不足。不能仅因为上游工具可能使用，就声明成项目的直接依赖。

`smol-toml` 当前主要由测试和 testkit 使用，应优先考虑迁入 `devDependencies`。

`@opencode-ai/plugin` 应保持为 peer dependency，不要在开发 manifest 与发布 manifest 中分别定义成不同类别。

### 4. 固定开发环境

- 选择 npm 作为唯一包管理器；
- 提交 `package-lock.json`；
- CI 使用 `npm ci`；
- 删除对不存在的 `bun.lock` 的布局豁免；
- 根 manifest 明确 `engines.node`；
- 添加 `global.json` 固定 .NET SDK；
- `dotnet-tools.json` 继续固定 Fable 和 Fantomas。

---

## 六、npm scripts 收敛

建议最终脚本面：

```json
{
  "scripts": {
    "build": "node scripts/build.mjs",
    "format": "dotnet tool run fantomas src tests",
    "lint": "node scripts/check.mjs",
    "test": "node tests/run-unit.mjs",
    "test:integration": "node tests/run-integration.mjs",
    "test:e2e": "node tests/run-e2e.mjs",
    "check": "npm run lint && npm run build && npm test && npm run test:integration",
    "check:release": "npm run check && npm run test:e2e -- --repeat=3 && npm pack --dry-run"
  }
}
```

其中：

- `format` 只负责修改文件；
- `lint` 和 `check` 必须只读；
- `test` 是默认快速测试；
- `test:e2e` 接受 `--repeat`，不再提供 `p0`、`p0:three` 和每个角色一条 npm alias；
- `check:release` 是唯一发布前入口。

删除无意义别名：

```text
test:mjs
test:unit
test:release
test:e2e:p0
test:e2e:p0:three
test:e2e:companion
test:e2e:manager-companion
test:e2e:reviewer
test:e2e:executor
```

需要单跑某个场景时，直接给 runner 传名称：

```bash
npm run test:e2e -- reviewer
npm run test:e2e -- companion --repeat=3
```

---

## 七、自制脚本处置

### 直接删除

```text
shock-audit.mjs
strip-doc-bold.mjs
conformance-gen.mjs
setup-git-hooks.mjs
```

理由：

- `shock-audit` 是迁移期灭绝扫描，迁移完成后不应成为永久平台；
- 文档是否使用粗体不是产品正确性；
- 不再生成 conformance Markdown；
- 安装过程不得修改 Git hook。

### 合并

```text
clean-fable-output.mjs
postbuild.mjs
```

合并为：

```text
scripts/build.mjs
```

只做：

1. 清空 `dist/`；
2. 调用 Fable；
3. 复制运行时 prompt assets；
4. 验证 `dist/Plugin.js` 存在。

```text
repository-layout-gate.mjs
architecture-gate.mjs
surface-inventory.mjs
role-matrix-gate.mjs
ssot-lint.mjs
conformance-gate.mjs
```

只保留仍有实际价值的部分，归并为三个检查模块：

```text
scripts/checks/architecture.mjs
scripts/checks/package.mjs
scripts/checks/spec.mjs
```

### 降级为普通测试

以下规则优先写成普通测试，而不是源码文本扫描器：

- Agent catalog 一致性；
- 单 writer；
- package 入口可加载；
- prompt route 必须走 Dispatcher；
- generated catalog 与源数据一致；
- runtime budget 边界；
- tool result shape。

测试生产行为比搜索字符串可靠。

### 审慎删除

`budget-gate.mjs` 和 `surface-inventory.mjs` 都已经成为数百行的自制静态分析器。0.5.3 应逐项证明其价值，而不是默认保留。

不能证明普通单元测试无法覆盖的规则，删除。

---

## 八、F# 项目结构和命名

### 1. 去掉迁移期的 `Next`

以下名称统一：

```text
Wanxiangshu.Next              → Wanxiangshu
Wanxiangshu.Next.fsproj       → Wanxiangshu.fsproj
src/Wanxiangshu.Next/         → src/Wanxiangshu/
```

只要 npm 公开入口不变，这属于内部正规化，不构成对用户的 API 破坏。

### 2. 文件夹已经表达上下文时，文件名不要复读

当前形式：

```text
Application/Orchestration/Orchestrator.Types.fs
Application/Orchestration/Orchestrator.Prompts.fs
Infrastructure/Git/Orchestrator.GitOperations.fs
Infrastructure/OpenCode/Host/OrchestratorHost.Types.fs
```

目标形式：

```text
Application/Orchestration/Types.fs
Application/Orchestration/Prompts.fs
Application/Orchestration/Program.fs
Adapters/Git/Orchestration/GitOperations.fs
Adapters/OpenCode/Orchestration/HostTypes.fs
```

禁止继续产生：

```text
HostForkRuntimeFork.fs
OrchestratorHostReview.fs
OrchestratorManagerJob.fs
```

文件名应描述一个责任，而不是把调用链上的所有名词拼起来。

### 3. 不使用点号模拟子目录

除生成文件外，不再新增：

```text
Foo.Types.fs
Foo.Prompts.fs
Foo.ManagerJob.fs
```

使用真正的目录和普通文件名。

### 4. 消除同名但不同语义的模块

优先处理：

```text
Domain/PromptAuthority.fs
Application/Prompting/PromptAuthority.fs

Journal/CompanionProjection.fs
Session/CompanionProjection.fs

Tools/
Infrastructure/OpenCode/Tools/
```

建议按职责改名：

```text
PromptAuthority.fs           领域规则
PromptService.fs             应用编排

CompanionState.fs            journal 投影状态
CompanionView.fs             session 查询视图

PluginTools/                 对模型公开的工具
Support/                     内部文件和 prompt 辅助设施
```

### 5. 大文件按责任拆，不按行数机械拆

0.5.3 的首要热点：

```text
Session/EnforcerHost.fs      约 1560 行
Journal/Fold.fs              约 900 行
Kernel/Fact.fs               约 500 行
Domain/PromptAuthority.fs    约 470 行
Session/BloggerCoordinator.fs 约 450 行
```

处理原则：

- `Fold.fs` 应成为薄分发器，各 feature 自己 fold 自己的事实；
- `Fact.fs` 可以按 feature 提取类型，但不得改变持久化名称和编码；
- `EnforcerHost.fs` 先识别 active、RFC 和历史兼容代码，再按运行责任拆分；
- 不创建 `Helpers.fs`、`Common.fs`、`Utils.fs` 作为垃圾抽屉；
- 只有被至少两个明确调用点共享的纯逻辑才提取公共模块。

### 6. 保留一个 F# 项目

0.5.3 不拆 solution，不拆多个 class library，不建立插件式内部平台。

一个 `.fsproj` 足够。

F# 的显式编译顺序可以继续作为架构索引，不要为了自动生成 fsproj 再引入一套 generator。

---

## 九、MSBuild 配置

当前根和项目内各有一份 `Directory.Build.props`，而且存在不同 RootNamespace、重复 Fable 引用和机器环境相关配置。

0.5.3 只保留根目录一份：

```text
Directory.Build.props
```

它只设置全仓通用内容：

- TargetFramework；
- TreatWarningsAsErrors；
- ArtifactsDir；
- 必要且有解释的 warning 配置。

项目依赖只写在 `.fsproj`。

删除：

- 根 props 中陈旧的 `VibeFs` RootNamespace；
- 重复的 `Fable.Core`；
- 未证明仍使用的 `Fable.Promise`；
- `LangVersion=preview`，除非源码确实依赖 preview 特性；
- 因单台机器缓存问题而全局设置的 `NuGetAudit=false`。

本机环境问题应由本地配置解决，不能永久关闭整个项目的依赖审计。

---

## 十、测试结构

目录名表达测试层级，不表达实现语言：

```text
tests/unit/
tests/integration/
tests/e2e/
tests/support/
```

测试文件继续使用 `.test.mjs`，不需要在目录名中重复 `mjs`。

建议定义：

- unit：纯领域、projection、codec；
- integration：文件系统、journal、进程、Git、pack/import；
- e2e：真实 OpenCode scenario；
- support：mock provider、scenario runner、fixtures、watchdog。

默认 `npm test` 不启动外部 Host，不占端口，不依赖 Git 全局配置。

发布检查必须包含：

1. unit；
2. integration；
3. e2e 三轮；
4. `npm pack --dry-run`；
5. 从 tarball 隔离安装；
6. import 包入口；
7. 最小插件注册 smoke test。

自制 runner 只保留 Node 内置 test runner 无法提供的能力，例如：

- Fable 构建 freshness；
- 资源泄漏检测；
- per-test deadline；
- e2e 隔离环境。

能交给 `node:test` 的部分不要重复实现。

---

## 十一、README 和项目文档

README 改成用户优先：

1. 项目是什么；
2. 安装；
3. 最小配置；
4. 最小使用示例；
5. 支持环境；
6. 常用开发命令；
7. 文档链接；
8. license。

以下内容移出 README：

- shock/anneal 迁移历史；
- 192/192 conformance 状态；
- 具体 commit；
- evidence 路径；
- 大段内部 clause 说明；
- “当前阶段”“下一步”等易陈旧状态。

架构说明放入：

```text
docs/architecture.md
```

开发流程放入：

```text
docs/development.md
```

发布流程放入：

```text
docs/releasing.md
```

CHANGELOG 只写用户可感知变化。0.5.3 条目应明确：

```text
No runtime behavior changes.
```

---

## 十二、开源身份必须诚实

当前 LICENSE 是 proprietary commercial license，package 也是 `private: true`。因此当前项目法律意义上不是开源项目。

0.5.3 必须明确选择：

### 方案 A：真正开源

- 换用明确的 SPDX license，例如 MIT 或 Apache-2.0；
- 删除 `private: true`；
- 公开发布时设置正确的 `publishConfig.access`；
- 增加 `CONTRIBUTING.md` 和 `SECURITY.md`；
- 修正 package repository、bugs 和 homepage；
- 确认仓库名称与 `wanxiangshu` 一致。

### 方案 B：保持商业闭源

- 保留当前 license 和 private；
- 文案使用“开源项目级工程质量”或“source-available repository”；
- 不对外宣称它是开源软件。

工程质量可以达到开源标杆，但许可性质不能模糊。

---

## 十三、推荐提交顺序

每个提交必须独立绿，不做一个无法 review 的“大扫除提交”。

```text
1. chore(repo): define 0.5.3 normalization scope
2. chore(layout): normalize spec docs and test directories
3. build(package): use one manifest and one dist pipeline
4. chore(checks): replace gate forest with one check command
5. refactor(src): normalize project namespace and file names
6. refactor(src): split fold and enforcer hotspots
7. test: mirror the normalized source layout
8. docs: rewrite user and contributor documentation
9. ci: add clean-clone and package verification
10. chore(release): release 0.5.3
```

禁止在最后一个 release commit 才修改代码、测试或构建逻辑。

---

## 十四、完成判据

0.5.3 只有同时满足以下条件才算完成：

### 仓库

- 不存在 `PENDING/`；
- 不存在 active `STATUS/`；
- 不存在提交进 Git 的逐次运行日志；
- 不存在第二份 package manifest；
- 不存在重复的 `Directory.Build.props`；
- 不存在 `Wanxiangshu.Next` 和旧目录路径残留；
- 工作树在验证完成后保持 clean。

### 命令

新贡献者只需：

```bash
npm ci
dotnet tool restore
npm run check:release
```

不得要求手动安装 hook、复制 package 模板或运行隐藏 gate。

### 构建

- `dist/Plugin.js` 存在；
- prompt assets 完整；
- 所有发射模块可加载；
- 构建两次得到相同文件集合；
- 构建不会产生未跟踪文件。

### 测试

- 现有 0.5.2 行为测试全部保留；
- journal wire compatibility 全绿；
- 单 writer、prompt dispatch、Git publish 和 PTY ownership 不变量全绿；
- e2e 三轮全绿；
- clean clone 上通过。

### 包

- tarball 只包含实际运行所需文件；
- tarball 可隔离安装；
- 根 import 成功；
- 最小 Host smoke test 成功；
- package metadata 只有一个来源；
- 所有 direct dependency 都有可证明的运行时 import；
- package 体积无无法解释的增长。

### 文档

- README 不含瞬时 commit 和测试计数；
- CHANGELOG 只记录版本变化；
- architecture、development 和 releasing 各有唯一文档；
- spec clause 与测试映射无缺失；
- license 状态与项目宣传完全一致。

---

## 十五、0.5.3 明确不做

不要在本轮：

- 新增 Strength、Student/Teacher 或 Enforcer 功能；
- 重写 journal 协议；
- 修改 fallback、review 或 orchestrator 业务规则；
- 拆成 monorepo；
- 拆多个 F# 项目；
- 引入 Nx、Turborepo、Bazel 或自制任务平台；
- 为了“以后可能需要”保留 migration gate；
- 建立新的 evidence schema；
- 为每个测试场景创建 npm script；
- 用 generated README 代替简单文档；
- 添加没有现实消费者的抽象层。

0.5.3 的成功不是“拥有更完整的平台”，而是：

> 删除平台化冲动以后，项目仍然可以由一个新贡献者在干净环境中读懂、构建、测试、打包和验证。

需要时，可再把这份指南压缩成逐 commit 的执行 checklist 和删除/重命名清单。

## Prompt:
8/4/2026, 6:24:26 AM

我来抛砖引玉
1 src/ 里面混入 artifacts，生成产物没有固定章法
2 doc/ SSOT/ STATUS/ RFC/ PENDING/ 到底谁是谁
3 testkit/ tests-mjs/ 到底谁是谁，而且一听就英语很烂
4 根目录垃圾文件也多 gitignore 也不全
5 postbuild 实现的笨拙：resources 能否不要暴力复制管理，直接参与打包即可，要有正规目录
...

## Response:
8/4/2026, 6:24:28 AM

这几条比我上一版更接近真正的“仓库卫生”核心。我会按“源码、文档、测试、根目录、资源打包”五条边界重新收敛，不再先从 gate 和流程抽象讲起。

> **分析0.5.3整理问题并完善构建产物治理配置**
>
> 
>
> **检查项目构建与清理配置**
>
>
> **梳理目录职责**
>
> 我会把这些观点整理成顶层设计原则，并补充一个关键区分：生成源文件、运行时资源和构建产物不应混为一类。`resources/` 可作为正式目录，随 npm 打包进入发布物，避免脚本暴力复制；同时明确仓库契约与各目录边界。
>
> **梳理了目录职责**
>
> 
>
> **检查提示资源路径与包根目录解析**
>
>
> **完善目录边界**
>
> 我会进一步区分三类内容：手写源代码保留在 `src/`，运行时资源统一放入 `resources/`，生成文件集中到 `artifacts/generated/`。这样既保持构建依赖清晰，也避免误把生成文件当作源码维护。
>
> **厘清仓库垃圾与构建打包问题**
>
>
> **检查项目构建配置并核实0.5.2版本包内容**
>
> 
>
> 思考了 3m 21s

对，这五条应该上升为 **0.5.3 的第一原则**。我上一版从 gate、流程和命令面切入得太早；真正的顺序应当是：

> 先定义每类文件应该住在哪里，再谈构建、测试和检查。

## 先建立唯一的仓库分类法

| 目录 | 只允许放什么 | 是否跟踪 | 是否发布 |
|---|---|---:|---:|
| `src/` | 人工维护的生产源码 | 是 | 编译后发布 |
| `resources/` | 人工维护的运行时静态资源 | 是 | 是 |
| `spec/` | 当前有效、具约束力的规范 | 是 | 否 |
| `docs/` | 说明、教程、架构解释、历史决策 | 是 | 否 |
| `tests/` | 测试、场景、fixture、测试支撑代码 | 是 | 否 |
| `artifacts/` | 中间产物、生成源码、日志、临时证据 | 否 | 否 |
| `dist/` | 可直接打包的最终产物 | 否 | 是 |

关键规则只有一句：

> 一个文件不能同时以“源码、资源、规范、提案、状态或构建产物”中的两个身份存在。

---

## 1. `src/` 现在不是单纯混入了编译产物，而是混入了三种性质

从当前仓库看，没有发现已编译的 JS/DLL 被提交到 `src/`。但它确实同时包含：

- 手写 F# 源码；
- `EnforcerCatalog.gen.fs` 这种生成源码；
- `prompts/*.md` 这种运行时资源；
- 项目内第二份 `Directory.Build.props`。

所以你的判断方向完全正确，只是精确地说，这是**源码树的职责污染**。

其中最典型的是：

```text
RFC/enforcer-nudge.md
        ↓ generator
src/Wanxiangshu.Next/Domain/EnforcerCatalog.gen.fs
```

生产代码居然从一个 RFC 文档生成。RFC 按语义是“提案”，却同时成为生产构建输入和事实来源，这是目录职责崩塌，不只是生成文件位置不好。当前 prompt 资源也放在 `src/.../prompts`，然后由 `postbuild` 复制到编译目录。`repomix-output.xml`

### 0.5.3 规则

`src/` 中只保留人工维护的 `.fs` 和 `.fsproj`。

建议改成：

```text
src/
  Wanxiangshu/
    ...
    Wanxiangshu.fsproj

resources/
  prompts/
    manager.md
    reviewer.md
    ...
  enforcer/
    catalog.toml
```

### `EnforcerCatalog.gen.fs` 有两个可接受方案

首选 KISS 方案：

```text
resources/enforcer/catalog.toml
```

生产代码直接读取并验证这份静态数据，删除：

```text
scripts/enforcer-catalog-gen.mjs
src/.../EnforcerCatalog.gen.fs
```

如果确实必须编译期生成，则输出到：

```text
artifacts/generated/EnforcerCatalog.fs
```

并由 `.fsproj` 显式引用：

```xml
<Compile
  Include="../../artifacts/generated/EnforcerCatalog.fs"
  Link="Domain/EnforcerCatalog.fs" />
```

生成步骤进入 `build`，但生成文件永不提交。

不能再出现：

```text
src/**/*.gen.fs
```

### 输出目录也要只有两种

```text
artifacts/
  generated/
  dotnet/
  test-results/
  diagnostics/

dist/
  Infrastructure/
  Domain/
  fable_modules/
  ...
```

- `artifacts/` 是内部中间结果；
- `dist/` 是最终 npm package 内容；
- 不再并存 `build/`、`build-next/`、`build-tests/`、`artifacts/bin/` 等多套语言。

---

## 2. `docs/`、`SSOT/`、`STATUS/`、`RFC/`、`PENDING/` 确实没有形成语义体系

当前目录实际上表达了五套互相重叠的“文本真值”：

```text
docs/      长期文档、历史文档、证据
SSOT/      当前规范
STATUS/    工作计划、规范状态、问题记录、conformance
RFC/       提案，但部分已经成为生产输入
PENDING/   尚未分类的附件
```

这会产生几个严重问题：

- `SSOT` 是一个宣传词，不是文件类型；
- `RFC` 可能已实现，也可能仍在讨论；
- `STATUS` 中既有临时任务，又有永久映射；
- `PENDING` 是收件箱，不是软件架构；
- `docs/archive` 在用源码仓库模拟文档管理系统；
- evidence、status 和 spec 都可能声称自己是当前事实。

当前仓库中确实同时存在逐版本 evidence、历史退火记录、conformance 表、工作计划、blockers、RFC 和待处理附件。`repomix-output.xml`

### 正确语义

#### `spec/`：现在必须遵守什么

把 `SSOT/` 改为：

```text
spec/
  README.md
  prompt.md
  fallback.md
  review.md
  persistence.md
  ...
  coverage.toml
```

`spec/` 只包含：

- 当前有效规则；
- 稳定 clause ID；
- 对外和内部可观察行为；
- 必要的不变量；
- clause 到测试的映射。

“SSOT”应该是 `spec/` 的性质，而不是目录名。

#### `docs/`：如何理解和使用

```text
docs/
  architecture.md
  development.md
  releasing.md
  testing.md
  decisions/
  rfcs/
```

`docs/` 不定义运行时规则，只解释规则。

#### `docs/rfcs/`：尚未接受的方案

RFC 必须有明确状态：

```text
Status: proposed
```

RFC 不能：

- 被生产 generator 读取；
- 被测试当作正式 clause；
- 与现有生产行为同时作为真值。

接受 RFC 后：

- 行为进入 `spec/`；
- 设计原因精简进入 `docs/decisions/`；
- RFC 可以删除或标记 superseded。

#### `STATUS/`：整体删除

其中内容分别处理：

| 当前内容 | 去向 |
|---|---|
| `conformance.toml` | 简化为 `spec/coverage.toml` |
| 永久架构结论 | `docs/architecture.md` |
| 已接受设计原因 | `docs/decisions/` |
| blocker | GitHub issue |
| 当前工作计划 | GitHub issue / milestone |
| convergence 报告 | 删除，由 Git 历史承担 |
| 临时调查 | PR 描述或 issue |

源码仓库不应保存“现在某人正在做什么”。

#### `PENDING/`：无条件删除

处理规则只有两个：

- 已经是项目知识：正式归档到 `docs/`、`spec/` 或 `resources/`；
- 还没有形成项目知识：放到 issue、discussion 或本地笔记。

不允许保留“以后再分类”的永久目录。

---

## 3. `testkit/` 和 `tests-mjs/` 的问题不只是英语，而是分类维度错了

`tests-mjs` 按实现语言命名，等于叫：

```text
tests-written-in-one-specific-file-extension/
```

文件本身已经有 `.mjs`，目录不应该再重复语言信息。

`testkit` 这个词本身在英语中并非错误，但它通常指“可复用的测试工具库”。当前 `testkit/` 实际混合了：

- E2E 场景定义；
- E2E 测试；
- mock provider；
- HTTP/SSE 模拟；
- process/reaper/watchdog；
- diagnostics；
- harness 自身测试；
- canary runner；
- 测试 fixture。

所以这个名字没有准确描述内容。

更明显的是：

```text
testkit/opencode/scripts/*.toml
```

这些 TOML 并不是 scripts，而是 scenarios。

`*-canary.mjs` 也不准确。Canary 通常描述生产环境中的小流量发布探测；这里实际是本地 E2E 或 smoke test。现有两棵测试树的具体内容也证明了这种混杂。`repomix-output.xml`

### 建议统一为一棵测试树

```text
tests/
  unit/
    domain/
    journal/
    prompt/
    review/

  integration/
    filesystem/
    git/
    package/
    process/

  e2e/
    scenarios/
      companion.toml
      fallback.toml
      host-restart.toml
      reviewer-verdict.toml
    companion.test.mjs
    fallback.test.mjs
    host-restart.test.mjs
    reviewer.test.mjs

  support/
    fake-provider/
    scenario-runner/
    process/
    diagnostics/
    fixtures/

  run-unit.mjs
  run-integration.mjs
  run-e2e.mjs
```

### 具体迁移

```text
tests-mjs/Context/              → tests/unit/context/
tests-mjs/Domain/               → tests/unit/domain/
tests-mjs/Journal/boot.test     → tests/integration/journal/
tests-mjs/Plugin/manager-*      → tests/integration/plugin/

testkit/opencode/scripts/       → tests/e2e/scenarios/
testkit/opencode/*canary.mjs    → tests/e2e/*.test.mjs
testkit/opencode/strict-mock-*  → tests/support/fake-provider/
testkit/process-lifecycle.js    → tests/support/process/
testkit/reaper.mjs              → tests/support/process/
```

`gate-testkit.mjs` 这种名字也应删除。测试 harness 自身的测试就是：

```text
tests/support/scenario-runner/runner.test.mjs
```

不用再发明 gate 概念。

---

## 4. 根目录和 `.gitignore` 需要的是“减法”，不只是补几个条目

当前 `.gitignore` 有几个相反的问题：

### 过时

还包含已经不存在或不再正确的目录：

```text
build-kernel/
build-shell/
build-tests/
build_verify/
e2e/opencode/specs/
```

### 过宽

```text
*.dll
*.exe
*.pdb
*.tmp
*.bak
*.cache
```

项目已经可以按输出目录忽略，就不应全仓库屏蔽这些扩展名。否则未来合法 fixture 也会被静默忽略。

### 个人工具污染项目配置

当前有：

```text
.cortexkit/
.slim/
.omo/
.codewhale/
.tokensave
```

这些不是项目产物，应进入开发者的 global gitignore，而不是公共仓库配置。

### 看似覆盖但实际漏掉

当前只有：

```text
repomix-output.xml
*.repomix.xml
```

因此这次文件名：

```text
repomix-output(148).xml
```

并不会被第一条覆盖。

真正的解决方式不是继续添加：

```text
repomix-output*.xml
```

而是要求所有此类输出进入：

```text
artifacts/repomix/
```

然后统一忽略 `/artifacts/`。

### 当前 layout gate 也没有真正解决问题

当前 root allowlist 包含：

```text
AGENTS.md
bun.lock
repomix-output.xml
```

即使这些文件未必属于项目；同时它对大部分隐藏文件直接跳过，所以并没有真正约束根目录。`repomix-output.xml`

根目录是否整洁不值得维护一套复杂 gate。目录本身足够简单时，人一眼就能判断。

### 建议根目录最终只保留

```text
.config/
.github/
docs/
resources/
scripts/
spec/
src/
tests/

.editorconfig
.gitattributes
.gitignore
CHANGELOG.md
CONTRIBUTING.md
Directory.Build.props
LICENSE
README.md
SECURITY.md
global.json
package.json
package-lock.json
```

其中：

```text
dotnet-tools.json → .config/dotnet-tools.json
```

`.ignore` 删除。若某个本地工具需要它，让开发者在本机维护，不成为产品仓库契约。

### 建议的最小 `.gitignore`

```gitignore
# Dependencies
/node_modules/

# Generated output
/artifacts/
/dist/
/coverage/

# Package output
/*.tgz

# Local environment
/.env
/.env.*
!/.env.example

# Logs
*.log
npm-debug.log*

# Editors and OS
/.idea/
/.vscode/
.DS_Store
Thumbs.db

# Current runtime state
/.wanxiangshu.ndjson
/.wanxiangshu.ndjson.lock
/.wanxiangshu-recovery/
/.sdd/
```

如果能把运行时状态统一到：

```text
.wanxiangshu/
```

未来还可以继续简化成一条。但如果这会改变兼容行为，可以不在 0.5.3 做。

---

## 5. `postbuild` 应该整体消失，resources 直接参与 npm 打包

当前 `postbuild.mjs` 同时承担：

1. 把第二份 package manifest 复制到 `build/package.json`；
2. 复制 LICENSE；
3. 递归复制 `testkit` 中所有非 `.fs` 文件；
4. 复制 prompts；
5. 删除 Fable 输出中的 `.gitignore`。

这其实是一个手写的 package staging system，而且是根据扩展名递归复制：

```js
else if (!entry.name.endsWith('.fs')) {
  fs.copyFileSync(srcPath, dstPath)
}
```

这意味着任何意外进入目录的非 F# 文件都会被复制。它既不声明资源清单，也不区分运行资源和测试资源。

0.5.2 的正式 tarball 甚至要求：

```text
cd build && npm pack
```

说明 `build/` 已经被当作第二个临时项目根目录。根 package 与 staging package 各有一份 manifest，打包行为取决于当前工作目录。`repomix-output.xml`

### 正确方案：仓库根就是唯一 package root

```json
{
  "main": "./dist/Infrastructure/OpenCode/Plugin/Plugin.js",
  "exports": {
    ".": "./dist/Infrastructure/OpenCode/Plugin/Plugin.js"
  },
  "files": [
    "dist/",
    "resources/"
  ]
}
```

npm 会自动包含：

- `package.json`；
- README；
- LICENSE。

因此完全不需要复制。

### prompts 直接作为正式资源

```text
resources/
  prompts/
    blogger.md
    browser.md
    coder.md
    ...
```

`PromptAssets` 从编译后模块的位置定位 package 根资源，例如其编译文件若位于：

```text
dist/Tools/PromptAssets.js
```

则可以解析：

```text
../../resources/prompts
```

具体实现仍然集中在一个 `ResourcePaths` 或 `PromptAssets` 模块内，并通过 `import.meta.url` 定位。不要依赖 `process.cwd()`。

### 构建流程变成

```text
generate required intermediates
        ↓
Fable compile → dist/
        ↓
verify dist entry and resources
```

只需要一个很薄的 `scripts/build.mjs`：

```text
1. 删除 dist/
2. 创建 artifacts/generated/
3. 必要时生成 F# 中间源码
4. 调用 Fable 输出到 dist/
5. 检查 package entry 存在
```

它不复制资源，不复制 manifest，不复制 LICENSE，不复制测试目录。

### 打包流程变成

```bash
npm run build
npm pack --dry-run
npm pack
```

永远从仓库根执行。

### 必须增加真正的 package 测试

```text
tests/integration/package/
  contents.test.mjs
  install.test.mjs
  import.test.mjs
  resources.test.mjs
```

至少断言：

- tarball 不包含 `tests/`；
- tarball 不包含 `spec/`；
- tarball 不包含 `scripts/`；
- tarball 不包含 `artifacts/`；
- 所有 prompt 都存在；
- 隔离安装后 package import 成功；
- `PromptAssets` 能读取全部资源；
- manifest 版本与根 package 一致，因为已经不存在第二份 manifest。

---

## 修订后的目标结构

```text
.config/
  dotnet-tools.json

.github/
  workflows/
    ci.yml
    release.yml

docs/
  architecture.md
  development.md
  releasing.md
  testing.md
  decisions/
  rfcs/

resources/
  prompts/
  enforcer/
    catalog.toml

scripts/
  build.mjs
  check.mjs

spec/
  README.md
  clauses/
  coverage.toml

src/
  Wanxiangshu/
    Agent/
    Application/
    Domain/
    Infrastructure/
    Journal/
    Kernel/
    Process/
    Session/
    Tools/
    AssemblyInfo.fs
    Wanxiangshu.fsproj

tests/
  unit/
  integration/
  e2e/
    scenarios/
  support/
  run-unit.mjs
  run-integration.mjs
  run-e2e.mjs

.editorconfig
.gitattributes
.gitignore
CHANGELOG.md
CONTRIBUTING.md
Directory.Build.props
LICENSE
README.md
SECURITY.md
global.json
package-lock.json
package.json
```

生成后本地可以出现：

```text
artifacts/
dist/
node_modules/
```

但 Git 树中永远看不到它们。

## 我会据此修改 0.5.3 的主线定义

0.5.3 的优先级应当改成：

1. **确定文件分类法和目标目录；**
2. **建立一个源码根、一个资源根、一个测试根；**
3. **建立一个中间产物根和一个发布产物根；**
4. **删除第二 package root 和 postbuild staging；**
5. **消灭 STATUS、PENDING 和 RFC 作为生产输入；**
6. **最后才整理 scripts、gates 和 npm commands。**

也就是说，gate 森林是结果，不是病根。病根是：

> 当前仓库没有明确回答“这个文件为什么在这里、谁维护它、它是否生成、它是否发布”。

## Prompt:
8/4/2026, 6:31:24 AM

EnforcerCatalog.gen.fs 不再走编译生成的路子，改为数据驱动。本项目是未来两年左右后最终会开源的闭源商业项目。最后重写 README, 分为对用户和对贡献者两个板块。

## Response:
8/4/2026, 6:31:25 AM

这三点会作为 0.5.3 的确定决策，而不是备选项：Enforcer 改成运行时数据、许可身份保持商业闭源、README 最后统一重写。先把它们落实成可验收的结构和迁移顺序。

> **规划 README 重写**
>
> 我会把 README 重构为“使用说明 + API 文档 + 贡献指南”的分层结构，明确生成器、模板、配置与插件边界。
> 
> 同时，我会去掉当前混杂的发布叙事，保留暂时准确的内部项目定位，并明确未来开放源码时再补充许可证、贡献流程和安全说明。
>
> **规划 README 重写**
>
> 
>
> **重构 Enforcer 数据驱动设计 and README**
>
> 
>
> 思考了 49s

这三项定为 **0.5.3 不再讨论的设计结论**。

## 一、Enforcer Catalog：彻底取消代码生成

删除：

```text
src/Wanxiangshu.Next/Domain/EnforcerCatalog.gen.fs
scripts/enforcer-catalog-gen.mjs
gate:generated
```

当前实现把规则目录从规范/RFC 抽取成 F# 源码，再编译成 JavaScript；这让文档、生成器和生成源码共同参与生产真值，复杂度没有实际收益。现有 `.fsproj` 也显式编译了 `EnforcerCatalog.gen.fs`。`repomix-output.xml`

### 目标结构

```text
resources/
  enforcer/
    catalog.json

src/Wanxiangshu/
  Domain/
    EnforcerCatalog.fs
  Infrastructure/
    Resources/
      EnforcerCatalogResource.fs
```

建议直接采用 JSON，而不是 TOML：

- Node 原生支持 `JSON.parse`；
- F# 侧已有 JSON codec 基础；
- 不为一个静态目录增加 TOML 运行时解析依赖；
- 数据结构固定，JSON 的严格性反而是优点；
- 不需要格式化器、生成器和同步检查器。

数据形状：

```json
{
  "schemaVersion": 1,
  "rules": [
    {
      "id": "ENFORCER-001",
      "field": "example_field",
      "scoreWhen": "Condition shown to the provider",
      "nudge": "Fixed nudge text"
    }
  ]
}
```

### 职责边界

`Domain/EnforcerCatalog.fs` 只负责：

- `EnforcerRule` 类型；
- catalog validation；
- 按 `id` 或 `field` 查询；
- 重复 ID、重复字段、空文本等纯规则。

`Infrastructure/Resources/EnforcerCatalogResource.fs` 只负责：

- 通过 `import.meta.url` 定位 package 中的资源；
- 读取 `resources/enforcer/catalog.json`；
- JSON 解码；
- 把解码结果交给领域验证器。

插件启动时加载一次，然后把已验证的 catalog 显式传给 Enforcer，不在领域函数内部偷偷读文件。

### 失败策略

Catalog 是产品自带的受控资源，因此：

- 文件不存在：启动失败；
- JSON 无效：启动失败；
- schema version 不支持：启动失败；
- ID 或字段重复：启动失败；
- 必填文本为空：启动失败；
- 不允许静默跳过坏规则；
- 不允许回退到编译内置副本。

### 真值关系

```text
spec/enforcer.md
```

只定义：

- catalog schema；
- 字段语义；
- 排序和唯一性规则；
- 加载失败策略；
- Enforcer 如何消费规则。

```text
resources/enforcer/catalog.json
```

保存具体规则实例。

规范中不再复制完整规则表，否则又会形成双真值。原 `RFC/enforcer-nudge.md` 中已经接受的规则实例迁入 JSON；仍属于设计解释的部分迁入 `docs/decisions/`。当前生成文件宣称 RFC/规范中的 Rule Catalog 是生成来源，正是这次需要切断的耦合。`repomix-output.xml`

### 必需测试

```text
tests/
  unit/
    enforcer/
      catalog-validation.test.mjs

  integration/
    resources/
      enforcer-catalog.test.mjs

    package/
      enforcer-resource.test.mjs
```

至少覆盖：

- 正式 catalog 可成功加载；
- 所有 ID 唯一；
- 所有 provider 字段唯一；
- 所有规则字段完整；
- 未知 schema version 拒绝；
- 缺失资源拒绝；
- 隔离安装 tarball 后仍能读取 catalog；
- package 中不存在 `EnforcerCatalog.gen.fs`；
- build 前后不产生任何 catalog 源码。

---

## 二、项目身份：商业闭源，但按未来开源标准建设

当前定位应明确写成：

> Wanxiangshu is proprietary commercial software. The source repository is private and is not currently open source.

保持：

```json
{
  "private": true,
  "license": "SEE LICENSE IN LICENSE"
}
```

继续使用当前商业许可证，不改 MIT、Apache-2.0 或其他开源许可证。当前仓库历史也明确采用私有交付和商业授权模式。`repomix-output.xml`

### “未来约两年开源”如何处理

它是产品路线，不是当前法律承诺。

因此不要在以下位置写“即将开源”：

- `package.json`；
- LICENSE；
- npm metadata；
- README 顶部宣传；
- 代码文件头；
- 对外安装说明。

README 的许可证部分只陈述当前事实：

```text
This project is proprietary commercial software.
Use, copying, modification, and distribution are governed by LICENSE.
```

是否以及何时开源，可以保存在内部 roadmap 或商业计划中。未来真正切换时，再作为单独版本完成：

- 许可证审计；
- 依赖许可证审计；
- 历史提交和秘密扫描；
- 商标与品牌规则；
- 行为准则；
- 外部贡献协议；
- 公共 issue/security 流程；
- 开源版 release。

### 现在就采用的“未来开源友好”工程实践

即使当前闭源，也应该现在具备：

- 清楚的仓库结构；
- 单一构建入口；
- 可复现安装与测试；
- 不依赖某位开发者机器；
- 不提交证据日志和本地状态；
- 不在源码中保存密钥、客户信息或机器路径；
- 清晰的贡献流程；
- 清晰的安全报告方式；
- 依赖许可证可追踪；
- 用户文档与内部实现文档分离。

也就是说：

> 工程上按开源项目建设，法律上始终诚实地保持闭源。

---

## 三、README 放在最后重写

这个顺序很重要。README 依赖最终目录、命令、资源路径和打包方式；如果现在重写，后续重命名会让它立即过期。

在以下内容稳定前，不做 README 大修：

1. `src/`、`resources/`、`tests/`、`spec/`、`docs/` 定名；
2. `dist/` 和 `artifacts/` 职责确定；
3. Enforcer catalog 数据化完成；
4. `postbuild` 删除；
5. 根 `package.json` 成为唯一 package manifest；
6. npm scripts 收敛；
7. 最终安装、配置和测试命令稳定。

README 是 0.5.3 最后一个文档提交。

## README 最终结构

README 只有两个一级主体板块：

```text
# Wanxiangshu

项目一句话说明
当前商业闭源声明
支持状态与必要警告

## 用户指南
...

## 贡献者指南
...
```

### 第一部分：用户指南

面向安装、配置和运行产品的人，而不是实现者。

建议目录：

```text
## 用户指南

### 产品简介
### 系统要求
### 获取软件
### 安装
### 配置
### 快速开始
### Agent 与角色
### 运行时数据
### 升级与兼容性
### 故障排查
### 商业许可与支持
```

用户指南应回答：

- 它解决什么问题；
- 用户需要什么版本的 Node/OpenCode；
- 如何从私有 registry 或交付 tarball 安装；
- 如何配置 Managed Agents；
- 最小可运行示例是什么；
- journal 和运行时文件在哪里；
- 版本升级有哪些兼容限制；
- 出问题首先执行什么；
- 如何获得商业支持。

不要放：

- SSOT clause 数量；
- 当前测试数量；
- 某次迁移 commit；
- shock/anneal 历史；
- gate 名单；
- 内部测试 harness 原理；
- 开发中的 blocker；
- “目前正在进行……”之类瞬时状态。

### 第二部分：贡献者指南

当前的“贡献者”首先是内部开发者，未来可以自然演化为外部贡献者。

建议目录：

```text
## 贡献者指南

### 仓库结构
### 开发环境
### 首次设置
### 常用命令
### 构建模型
### 测试分层
### 规范与设计文档
### 运行时资源
### 代码组织原则
### 提交要求
### 发布流程
### 安全与保密
### 许可证
```

其中必须明确仓库分类：

```text
src/        人工维护的生产源码
resources/  随产品交付的运行时数据
spec/       当前有效的行为规范
docs/       解释性文档和设计记录
tests/      全部测试及测试支撑代码
scripts/    极少量仓库自动化入口
artifacts/  未跟踪的中间产物
dist/       未跟踪的最终发布产物
```

README 中只保留最常用命令：

```bash
npm ci
dotnet tool restore
npm run build
npm test
npm run test:e2e
npm run check
npm run check:release
```

复杂测试选择、场景格式和发布细节链接到：

```text
docs/testing.md
docs/development.md
docs/releasing.md
```

README 不应成为第二份架构规范。

---

## 四、因此更新后的 0.5.3 执行顺序

```text
1. chore(layout): establish repository file taxonomy
2. refactor(spec): replace SSOT STATUS RFC PENDING overlap
3. refactor(resources): move prompts into resources
4. refactor(enforcer): replace generated catalog with runtime data
5. refactor(tests): merge testkit and tests-mjs under tests
6. build(package): remove staging package and postbuild copying
7. build(output): standardize artifacts and dist
8. chore(root): clean root files and gitignore
9. chore(scripts): reduce scripts and package commands
10. test(package): verify isolated install and all resources
11. docs(readme): rewrite user and contributor guides
12. chore(release): release 0.5.3
```

其中第 4 步完成后必须满足：

```text
EnforcerCatalog.gen.fs       不存在
enforcer-catalog-gen.mjs     不存在
gate:generated               不存在
resources/enforcer/catalog.json 存在
```

第 11 步之前不允许 README 声称新的目录或命令已经可用。

## 最终目标目录补充

```text
resources/
  prompts/
    blogger.md
    browser.md
    coder.md
    devops.md
    executor.md
    inspector.md
    manager.md
    meditator.md
    orchestrator.md
    reviewer.md

  enforcer/
    catalog.json
```

`resources/` 整体通过根 `package.json` 的 `files` 直接参与打包，不经过复制：

```json
{
  "files": [
    "dist/",
    "resources/"
  ]
}
```

这样 prompts 和 Enforcer catalog 使用完全相同的正规资源模型：

> 手工维护、版本控制、随包发布、启动时验证，不生成、不复制、不隐藏。



---
Powered by [ChatGPT Exporter](https://www.chatgptexporter.com)