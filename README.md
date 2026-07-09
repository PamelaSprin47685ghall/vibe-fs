# 万象术

`wanxiangshu` = F# 经 Fable 编译为 JS 的多代理插件运行时。

把同一套多代理能力落到四套宿主适配面（共享 `Kernel` + `Shell`）：

- OpenCode / Mimocode 风格插件：`src/Opencode/`
- Mux 风格注册表：`src/Mux/`
- oh-my-pi（`@oh-my-pi`）Pi 扩展：`src/Omp/`（入口 `Plugin.fs` → `wanxiangshuExtension`；npm 子路径 `wanxiangshu/omp`）

## 项目要解决的真实问题

表面提供：子代理委派、文件读写、模糊搜索、执行器、审查循环、nudge、54 个结构化方法论笔记本工具（`methodology_*`）、OpenCode / Mimocode / Mux / OMP 四宿主接线。

真正困难的不是"把工具挂出来"，而是同时满足：

1. 同一套领域规则要在四套宿主里复用，而各宿主的工具协议、schema、wrapper、事件模型都不同。
2. LLM 接口天然弱类型，消息/工具参数/宿主对象结构都可能漂移。
3. 宿主会 compaction，对话历史不可靠；durable 工作状态须落在 `.wanxiangshu.ndjson`（`PRD/02-event-sourcing.md`）。
4. review、nudge 跨多轮且必须在重启后靠事件重放恢复。
5. 插件运行在 JS/Node 中，外部世界全是副作用；但业务规则必须可测试、可推理、可重放。

不先解决根问题，后面的功能实现都会退化成脆弱 hook 和 if/else。

## 第一性原理

### 1. 稳定资产是领域规则，不是宿主 API

宿主会变，schema 会变，消息对象会变；真正稳定的是：

- review = 有限状态机
- nudge = 有限状态机
- todo folding = 压缩上下文保留 durable progress
- tool permission = 角色到能力的规则矩阵

把稳定规则抽到独立纯内核，宿主适配放到外围。

### 2. 事件流才是事实，内存状态只是积分

review/todo/nudge durable 状态不能靠进程内存或宿主 `session.messages`（compaction 会裁掉上下文）。**`[workspace]/.wanxiangshu.ndjson`**：意图不落盘，校验通过的事实才 append；每行含 `session`；文件锁保证串行追加。进程内 `ReviewStore`、backlog 投影等 = 对该 session 事件行的纯 fold。进程重启 → 先重放 NDJSON，再服务 hook。

### 3. 副作用不可避免，但必须被压到边界

文件系统、网络、子进程、宿主 session、MCP、tree-sitter、第三方库是现实接口不是领域规则。压到 `src/Shell/` 与各 codec；Kernel 写成纯函数。

### 4. LLM 边界必须尽快强类型化

LLM 参数与宿主消息是动态对象。如果动态对象直接在业务核心里流动，错误会在深处爆炸。策略：

- 宿主边界先读 `obj`
- 尽快转成 Kernel 里的 DU/record
- 纯逻辑只消费强类型
- 最后编码回宿主对象

### 5. 宿主上下文可折叠，万象术进度靠事件

宿主侧：compaction 压缩 LLM 窗口即可。万象术侧：每次 `todowrite`/`task` 成功提交 → `work_backlog_committed` 事件携带全量 `todos` 与五份 `completedWorkReport` 字段；**不再**依赖 compaction 后注入 anchor prompt 从历史 fold backlog。

### 6. 并发的本质是共享可变状态

JS/Node 无线程级并发，但有大量异步交错。策略不是到处加锁：

- 单进程：Promise 串行队列约束关键路径
- 按 session/workspace 切分串行域

### 7. 测试必须时间无关

测试决不依赖系统当前时钟、随机数、外部服务或不可控的执行延迟（如物理文件 IO 时延的硬编码等待）。为时间无关测试让路，依赖注入与自适应事件/计数器轮询是最好的武器。

## 架构

### Kernel：纯规则层

不是"公共工具箱"，是"真正稳定的系统语义"。90 个 `.fs` 文件，按子目录+顶层分六类：

1. 领域状态机（子目录）：
   - `ReviewSession/`：`Types`/`StateMachine`/`Registry`/`Query`/`Effects`/`Facade`
   - `Nudge/`：`Types`/`NudgeDerivation`/`Registry`/`RetryProgress`/`SubmitReviewHooks`/`TodoStatus`
   - `EventLog/`：`Types`/`Fold`（事件 DU + 纯 fold：`foldReviewTask`/`foldWorkBacklogSnapshot`/`foldNudgeDedup`）
   - `FallbackKernel/`：`Types`/`Decision`/`Recovery`/`StateMachine`（完全平方数启发式模型降级纯状态机）
2. 跨宿主共享格式与协议：`PromptFrontMatter`/`PromptFragments`/`LoopMessages`/`ReviewPrompts/`（`Commands`/`Format`/`Instructions`/`OmpVariant`/`Registry`/`Submission`）/`SearchPrompts`/`SubagentPrompts`/`OmpPrompts`/`CapsFormat`/`Yaml`/`PatchParser`
3. 工具/权限/意图元数据：`ToolCatalog/`（`ToolSpec`/`Registry`/`Classification`/`FileIO`/`Web`/`Search`/`Review`/`Subagent`/`Executor`）/`ToolCatalogParams`/`Config`/`Subagent`/`SubagentIntents`/`SubagentToolPolicy`/`HostTools`（含 `Omp`）/`OmpSessionTools`/`ToolPermission`/`ToolCopy`/`ToolResult`/`ToolArgs`/`ToolContext`/`ToolOutputInfo`(+`Types`+`Parse`)/`WebFetchGuard`/`ReviewVerdict`/`WarnTdd`
4. 历史折叠与消息语义：`Messaging`/`Message`/`MessageDedup`/`Dedup`/`BacklogProjectionCore`/`BacklogProjection`/`WorkBacklog`/`MessageTransformPolicy`/`ReviewReplayPolicy`
5. 纯算法或纯解析：`FuzzyQuery`/`FuzzyPath`/`FuzzyFormat`/`Executor`(+`ExecutorStrip`)/`TreeSitterKernel`/`Domain`/`Methodology`(+`MethodologyCatalog`)
6. 会话前导与宝典：`CapsPrelude`/`CapsFormat`/`CapsSynthPolicy`/`PromptFrontMatter` 等（宝典/铁律文本 SSOT 在 `CapsPrelude`）

判断标准：去掉 Node/宿主对象后仍成立就进 Kernel。

### Shell：现实世界边界

把 Kernel 需要的能力从 Node 取回，137 个 `.fs` 文件按功能分簇：

- 文件系统：`FileSys`/`WorkspaceFiles`
- 搜索后端：`FuzzyFinderShell`/`FuzzySearch`(+`Find`/`Grep`/`Helpers`)/`FuzzyIteratorStore`
- 语义搜索注入：`SembleMcp`（MCP client v1.x best-effort spawn/connect/callTool）/`SembleSearch`（investigator 断点检测 + context 提取 + read 对注入）
- 执行器：`Executor`/`ExecutorJavascript`/`ExecutorSpawn`/`ExecutorSpawnRunners`/`SessionExecutor`
- tree-sitter：`TreeSitterShell`/`TreeSitterPlatform`
- 远端 API：`WebSearchApi`/`WebFetch`/`TitleFetchGuardCommon`
- 动态类型与错误分类：`Dyn`/`DynField`/`ErrorClassify`/`JsArrayMutate`/`PromiseStr`
- OMP 专属边界能力：`OmpCaps`/`RunnerBackground`/`CapsPrelude`（Shell 侧 caps 组装）/`OmpHostBindings`/`MuxHostBindings`
- 基础设施：`PromiseQueue`/`RuntimeScope`/`Clock`/`LivelockGuard`/`SerialStateHolder`/`CoordinatorLifecycle`
- 子代理与 session：`ChildAgentRegistry`/`SessionProjectionStore`/`SubagentSpawn`/`SubagentIo`/`SubagentToolExecute`/`MuxSubagentToolExecute`/`SessionIoSpawn`
- 编解码边界（`*Codec`/`*Decode`/`*Encode`/`*Wire`）：
  - 工具：`ToolArgsDecode`/`ToolExecute`/`ToolRuntimeContext`/`ToolContextCodec`/`FileToolsCodec`/`WebToolsCodec`/`WebSearchCodec`/`ExecutorToolsCodec`/`FuzzyToolsCodec`/`PatchToolsCodec`/`DelegateToolsCodec`/`WorkBacklogToolsCodec`/`ReviewToolsCodec`/`JsonSchemaBuilders`/`MuxJsonSchema`
  - 子代理：`SubagentIntentsCodec`/`SubagentSimpleArgsCodec`/`SubagentPromptBuild`
   - 消息变换：`MessageTransformPipeline`/`MessageTransformCore`/`MessageTransformHostEntry`/`MessageTransformHostHooks`/`MessageTransformCommon`/`MessagingEncodeHelpers`/`MessagingPartCodec`/`HostMessagePartCodec`/`ChatHookOutputCodec`/`ChatTransformOutputCodec`
  - Opencode 宿主：`OpencodeHookInputCodec`/`OpencodeSessionPromptCodec`/`OpencodeSessionSpawnCodec`/`OpencodeAgentConfigCodec`/`OpencodeAgentConfigWire`/`OpencodeClientCodec`/`OpencodeContextCodec`/`OpencodeSessionEventCodec`(+`Common`/`Nudge`)
  - Mux 宿主：`MuxHookInputCodec`/`MuxWorkspaceCodec`/`MuxAiSettingsCodec`/`DelegatedAiSettings`
  - 去重：`ReadDedupOpenCode`/`ReadDedupMuxPlugin`
  - caps 缓存：`CapsFileCache`/`CapsSynthCommon`
- review/nudge runtime：`ReviewRuntime`/`ReviewReplaySync`/`NudgeRuntime`
- 事件溯源：`EventLogCodec`/`EventLogFiles`/`EventLogRuntime`（NDJSON append/lock/fold + ReviewStore 同步）
- Fallback：`FallbackConfigCodec`/`FallbackRuntimeState`/`FallbackMessageCodec`/`FallbackEventBridge`
- backlog：`BacklogSessionCodec`/`WorkBacklogSchema`

每个 Shell 模块只承担一种外部能力，复杂流程留给上层编排。

### 宿主适配：四套接线，五个公开入口

不强行用巨型通用抽象吞差异，保留四个适配目录：

- `src/Opencode/`：OpenCode / Mimocode 插件 hook、Zod tool schema、消息变换
- `src/Mux/`：Mux 注册表、wrapper、slash command、delegate API
- `src/Omp/`：oh-my-pi `wanxiangshuExtension`、Pi 事件钩子、`ChildSession` 子 workspace、工具 schema（`OmpToolSchema`）

公开 JS 入口五个（见根 `package.json` `exports`）：

- `src/Opencode/Plugin.fs`：OpenCode 插件入口
- `src/Opencode/PluginMimo.fs`：Mimocode 插件入口
- `src/Opencode/PluginMimoTui.fs`：Mimocode TUI 辅助（`/subagents` + `task`→sidebar todo 回填）
- `src/Mux/Plugin.fs`：Mux 注册表入口（包默认 `"."` → `build/src/Mux/Plugin.js`）
- `src/Omp/Plugin.fs`：OMP 扩展入口（`"./omp"` → `build/src/Omp/Plugin.js`）

共享同一个 Kernel；差异留在各层：schema 生成、tool execute 签名、消息对象形态、子代理启动（OpenCode `ChildAgentRegistry` / Mux delegate / OMP `taskService`+子 workspace）、`task`/`todowrite` 命名（`HostTools` 按 `Opencode|Mimocode|Mux|Omp` 分支）。`src/Omp/` 仅依赖 Kernel+Shell，禁止 `open` / 引用 `Wanxiangshu.Opencode`、`Wanxiangshu.Mux`。

### Methodology：数据驱动 schema，注册为工具

`src/Methodology/`：`SchemaCommon` + `Args` + `Catalog1`-`Catalog4`（`buildSchema` 数据）+ `Catalog`（聚合）+ `Registry.allSchemas`，经 `Methodology.OpencodeTools` / `Methodology.MuxTools` / `Methodology.OmpTools` 注册为 `methodology_<id>` 工具（共 54 个）。`Kernel.Methodology` 维护 `select_methodology` 枚举与 `todowrite`/`task` 必填字段文案；`Registry.enumValues` 从 `allSchemas` 单一派生消除三重数据复制；OMP 经 `Methodology.OmpTools.registerMethodologyTools`（TypeBox schema，经 `OmpToolSchema.methodologyParameters`）注册同一批 `methodology_<id>` 工具，并在 `todowrite` schema 与 `tool_result` 后处理侧消费同一 `select_methodology` 枚举。

### review loop = DU + 事件 fold

review 仅少数合法状态 → DU 状态机。`/loop` 与 verdict 等事实 → append `loop_*` / `review_verdict` 事件；跨重启从 `.wanxiangshu.ndjson` fold 当前 `task`，再 `syncReviewProjection`。消息上的 YAML front-matter 仅作 LLM 可读展示，**不是** SSOT。

更硬的纪律：`reviewStore` 等只是事件积分的缓存。loop/nudge 判断一律先由 **事件重放** 得到 `SessionSnapshot`，再进 `Kernel.Nudge`/`NudgeDerivation`；`ReviewReplaySync.syncReviewFromTexts`（基于 `inferReviewTaskFromTexts`）仍存在作为宿主文本 fallback，事件重放（`EventLogRuntime.syncReviewFromEventLog`）为首选路径。

### todo 必须有 `completedWorkReport`

五份交接报告随 `work_backlog_committed` 事件落盘；全量 `todos` 同事件携带。OpenCode `todowrite` 与 Mimocode `task` 校验通过后 append，再由 fold 驱动 caps/transform 中的 backlog **展示**（非从历史 tool 结果 fold SSOT）。

### fuzzy search 纯/壳分离

query 拼装、路径归一化、输出格式、iterator 语义是稳定规则 → Kernel；后端 finder 生命周期 → Shell。iterator 是有限状态（find state / grep state）而非任意字符串，挂在 `RuntimeScope.IteratorStore`。

### nudge kernel decision 与 runtime flow 分

"该不该 nudge"是纯判断；"如何发 nudge"涉及宿主 API、错误、并发、abort。纯状态更新与异步 `session.prompt` 分离，避免单一串行队列被 await 卡死。

补充铁律：nudge 的输入必须是 **事件 fold** 出的 `SessionSnapshot`；loop 是否活跃由 NDJSON 重放，不靠 compaction 后的消息切片或内存布尔捷径。

### 提示词 YAML front-matter

顶部结构化字段程序精确识别，下方 prose 模型友好。重启后 replay 只读 scalar 字段，不必全文 NLP。

### `multi-frontmatter`（展示层）

`PromptFrontMatter` 仍解析多块 YAML（工具输出、用户可读锚点）。**不再**把 multi-frontmatter 或 compaction-anchor prompt 当作 review/todo 的真相源；durable 语义见 `PRD/02-event-sourcing.md`。

## 读代码推荐路径

1. `src/Kernel/ReviewSession/StateMachine.fs` + `Types.fs`
2. `src/Kernel/Nudge/Transitions.fs` + `Decision.fs` + `src/Kernel/Nudge.fs`
3. `src/Kernel/BacklogProjectionCore.fs` + `BacklogProjection.fs` + `WorkBacklog.fs`
4. `src/Kernel/PromptFragments.fs` + `LoopMessages.fs`
5. `src/Shell/PromiseQueue.fs`
6. `src/Shell/MessageTransformPipeline.fs` + `MessageTransformCore.fs`（共享管线；宿主入口在 `Opencode`/`Mux`/`Omp` 各 `MessageTransform.fs`）
7. `src/Opencode/PluginCore.fs` + `src/Omp/PluginCore.fs`
8. `src/Mux/Plugin.fs`
9. `src/Opencode/Plugin.fs` (OpenCode 入口)
10. `src/Opencode/PluginMimo.fs` (Mimocode 入口)
11. `src/Omp/Plugin.fs` (OMP 入口)
12. `src/Methodology/Registry.fs` + `Methodology/OpencodeTools.fs` / `MuxTools.fs`

按"先看不变规则，再看副作用，再看宿主拼装"。

## 架构边界纪律

架构测试在 `tests/` 下以 `ArchitectureTests*` 系列 fs 文件覆盖：

- `Kernel/` 不直接 `Dyn.*`，所有宿主 `obj` 必经 Shell codec
- 每个工具族必在 `Kernel.ToolCatalog` + 对应 Shell codec 维护 SSOT
- Opencode / Mux 执行链路经 `Shell.ToolArgsDecode` + `Shell.ToolExecute` + `Shell.SubagentToolExecute` / `Shell.MuxSubagentToolExecute`；OMP 在 `src/Omp/` 仅依赖 Kernel+Shell
- 可变状态只能在 `Shell.RuntimeScope` 派生的实例内；`SessionExecutor` 模块级仅保留 `activeRuns` 登记（无模块级 session 队列）
- Hook output 写路径经 `OpencodeHookInputCodec` / `MuxHookInputCodec`，禁裸 `setKey output args|error|parts` / `Dyn.get output args`
- Tool `description` / 必填键 / 拒绝语 / `mergeConfigObj` / 子代理 schema shape 全部归 `Kernel.ToolCatalog` + `Kernel.ToolCopy`
- 权限判定经 `ToolPermission.classifyTool → ToolSemantic` + `canUseForHost`，以 Set 精确匹配为主
- `src/Omp/` 禁止 `open` / 引用 `Wanxiangshu.Opencode`、`Wanxiangshu.Mux`、`engine/`
- `NudgeEffect` / `NudgeSnapshot` / `Omp.NudgeHooks` 等 nudge 状态机入口禁止直接读任何内存活跃态查询；loop/backlog 事实必须经 **`.wanxiangshu.ndjson` 事件 fold**（实现：`Kernel/EventLog` + `Shell/EventLogFiles`）

## 构建与测试

环境：.NET SDK（项目目标 `net10.0`）、`dotnet tool restore` 安装 Fable、Node.js + npm。

```bash
npm run build-and-test
```

完整管线：

1. `npm run build`：`wanxiangshu-core` + `wanxiangshu-omp` + `wanxiangshu-wanxiangzhen` → `build/`
2. 清理 `build/fable_modules/.gitignore` + 拷贝 `build-package.json` 为 `build/package.json`
3. `node tests/runner.js`：全部测试

无独立编译/watch/子测试集命令。

npm 包主导出入口：`build/src/Mux/Plugin.js`（`"."`）；OMP：`build/src/Omp/Plugin.js`（`"./omp"`）。测试入口：`tests/runner.js` 加载 `build/tests/Tests.js`。`TargetFramework` 在根 `Directory.Build.props`（`net10.0`）。中间产物（MSBuild `bin/`/`obj/`）统一落到根目录 `artifacts/`；清理直接 `rm -rf build artifacts`。

## 源码入口速览

从功能入口反推实现：

1. `src/Mux/Plugin.fs`：Mux 注册、wrapper、event hook、slash command
2. `src/Omp/Plugin.fs` + `PluginCore.fs`：OMP `wanxiangshuExtension`、Pi 事件、子 workspace、Magic/review 接线
3. `src/Opencode/PluginCore.fs`：OpenCode / Mimocode 共用插件装配
4. `src/Opencode/Tools.fs`：OpenCode 工具总表（含 `registerMethodologyTools`）
5. `src/Mux/HostTools.fs` + `src/Mux/Plugin.fs`：Mux 内建工具与 `methodology_*` 注册
6. `src/Opencode/MessageTransform.fs` + `src/Mux/MessageTransform.fs` + `src/Omp/MessageTransform.fs`：caps prelude、Magic todo、review replay
7. `src/Shell/OpencodeSessionEventCodec.fs` + `OpencodeSessionEventNudge.fs` + `src/Opencode/NudgeEffect.fs`：Opencode session event payload 边界编解码

## 目录速览

```text
src/
  Kernel/       纯领域规则、状态机、格式协议、共享提示词（90 .fs，含 EventLog/ FallbackKernel/ Nudge/ ReviewPrompts/ ReviewSession/ ToolCatalog/ 子目录）
  Shell/        Node/文件系统/网络/第三方库/串行队列 + 全部宿主 obj 边界 codec（137 .fs）
  Methodology/  54 个方法论 schema + Registry + Opencode/Mux/Omp 工具注册（11 .fs，数据驱动 Catalog1-4）
  Opencode/     OpenCode / Mimocode 插件适配层与 TUI 扩展（49 .fs）
  Mux/          Mux 注册与 wrapper 适配层（21 .fs）
  Omp/          oh-my-pi 扩展适配层（仅 Kernel+Shell，33 .fs）
tests/          纯内核 + Shell + 集成 + 插件契约 + 架构边界探针（317 .fs，含 23 个 ArchitectureTests*）
e2e/            端到端插件测试（13 .fs + 3 .js：harness/mock-llm/stealth-mcp-fixture）
build/          Fable 编译后的 JS 产物
```

## 一句话总结

这个项目的核心不是"F# 写插件"，而是：

> 先把多代理系统里真正稳定的语义抽成纯内核，再把宿主、IO、消息对象、schema、并发与持久化都压成外围适配问题。

抓住这条主线，当前大多数实现细节都不是偶然选择，而是被这组约束一步步推出来的。
