# vibe-fs

`vibe-fs` 是一个用 F# 编写、经 Fable 编译为 JavaScript 的插件/工具运行时。

它不是单纯的工具集合，而是把一套多代理工作流能力落到两套宿主适配面上：

- OpenCode / Mimocode 风格插件：`src/Opencode/`
- Mux 风格注册表：`src/Mux/`

这份 README 不主讲 API 清单，而是反推本项目的第一性原理、演绎过程，以及这些原则为什么必然长成现在的实现细节。

## 项目要解决的真实问题

表面上，本项目提供的是：

- 子代理委派：`coder`、`investigator`、`browser`、`meditator`、`executor`
- 搜索 / 读取 / 执行 / 审查 / 问答 / 写入工具
- review loop、todo folding、knowledge graph / bookkeeper、nudge、methodology probe
- OpenCode、Mimocode、Mux 三条宿主接线

但真正困难的不是“把工具挂出来”，而是同时满足下面这些约束：

1. 同一套领域规则要在两个宿主里复用，而两个宿主的工具协议、schema、wrapper、事件模型都不相同。
2. LLM 接口天然弱类型，消息/工具参数/宿主对象结构都可能漂移。
3. 会话会很长，历史需要折叠；但折叠后又不能丢掉真正重要的工作状态。
4. review、knowledge graph、nudge 这类能力都跨多轮对话，且必须在重启后尽量恢复。
5. 插件运行在 JS/Node 宿主中，外部世界全是副作用；但业务规则又必须保持可测试、可推理、可重放。

如果不先解决这些根问题，后面的“功能实现”都会退化成一堆脆弱 hook 和 if/else。

## 第一性原理

### 1. 真正稳定的资产是领域规则，不是宿主 API

宿主会变，工具 schema 会变，消息对象长相会变；真正值得保留的是：

- review 是一个有限状态机
- nudge 是一个有限状态机
- knowledge graph 是 append / daily rewrite 两种作业
- todo folding 的目标是压缩上下文但保留 durable progress
- tool permission 是一张角色到能力的规则矩阵

所以项目必须把“稳定规则”抽到独立的纯内核里，而把“如何接宿主”放到外围适配层。

这直接推出目录分层：

- `src/Kernel/`：纯领域模型、状态机、提示词拼装、格式化、权限判定
- `src/Shell/`：Node/文件系统/网络/子进程/第三方库边界
- `src/Opencode/`：OpenCode 宿主适配
- `src/Mux/`：Mux 宿主适配

### 2. 历史记录才是事实，内存状态只是投影

review 是否激活、todo backlog 如何折叠、knowledge graph job 是否存在、editplus tag 是否还能解释，这些都不是“当前进程内存”能可靠承诺的。

进程会重启，hook 会打断，background session 会延后返回。既然宿主的消息历史天然存在，那它就应当被视为事实来源；进程内 store 只能是可重建投影。

这条原则直接推出多个实现细节：

- `src/Kernel/LoopMessages.fs` 用 YAML front-matter 写结构化锚点，而不是依赖自然语言文案
- `src/Opencode/MessageTransform.fs` 会从历史重建 review 状态、todo backlog、editplus 状态
- `src/Opencode/KnowledgeGraphRuntimeIO.fs` 提供 `tryResolveJobContext`，从消息历史恢复 knowledge graph job marker
- `src/Kernel/WorkBacklog.fs` 强制要求 `completedWorkReport`，因为旧对话可能被折叠，只能靠 durable report 保存关键信息

### 3. 副作用不可避免，但必须被压到边界

文件系统、网络请求、子进程执行、宿主 session 创建、MCP、tree-sitter、fff-node，这些都不是领域规则，它们只是现实世界接口。

所以项目把它们压到：

- `src/Shell/`：Node 侧 IO 能力
- `src/Opencode/*Codec.fs`、`src/Mux/Wrappers.fs`、`src/Opencode/ToolSchema.fs`：对象编解码与宿主胶水

与此同时，真正可推理的部分尽量写成纯函数：

- `src/Kernel/ReviewSession.fs`
- `src/Kernel/Nudge.fs`
- `src/Kernel/KnowledgeGraph.fs`
- `src/Kernel/KnowledgeGraphMaintenance.fs`
- `src/Kernel/FuzzyQuery.fs` / `FuzzyPath.fs` / `FuzzyFormat.fs`
- `src/Kernel/Executor.fs`

### 4. LLM 边界必须尽快强类型化

LLM 发来的参数是动态对象；宿主消息也是动态对象。如果动态对象直接在业务核心里流动，错误会在系统深处爆炸。

所以本项目的策略是：

- 宿主边界先读 `obj`
- 尽快转成 Kernel 里的 DU/record
- 纯逻辑只消费强类型
- 最后再编码回宿主对象

这就是为什么会有：

- `src/Opencode/MessagingCodec.fs`
- `src/Opencode/CapsCodec.fs`
- `src/Kernel/SubagentIntents.fs`
- `src/Kernel/Messaging.fs`
- `src/Kernel/Domain.fs`

### 5. 长会话一定会折叠，所以进度必须可压缩

多代理 coding session 很长。如果把所有原始消息都硬塞进上下文，系统会被自己的历史淹死。

但简单删历史又会丢掉真正关键的工作事实。于是项目选择的不是“保留全部”，也不是“只保留最新”，而是：

- 把旧工作压缩成 backlog projection
- 用结构化 `completedWorkReport` 保存 durable knowledge
- 把最近一段原始上下文保留给当前回合

因此才有：

- `src/Kernel/BacklogProjectionCore.fs`
- `src/Kernel/BacklogProjection.fs`
- `src/Kernel/WorkBacklog.fs`
- `src/Opencode/BacklogSession.fs`

### 6. 并发问题的本质是共享可变状态

本项目运行在 JS/Node 宿主里，没有线程级并发，但会有大量异步交错：

- tool hooks 同时触发
- background reviewer / bookkeeper 异步返回
- knowledge graph 写入需要跨进程序列化
- per-session executor 必须按顺序执行

所以策略不是“到处加锁”，而是：

- 单进程内：用 `Promise` 串行队列约束关键路径
- 跨进程：用显式 port lock 保护 knowledge graph 写入
- 按 session/workspace 切分串行域

这直接推出：

- `src/Shell/PromiseQueue.fs`
- `src/Shell/ChildAgentRegistry.fs` 里的 per-session executor actor
- `src/Shell/KnowledgeGraphPortLock.fs`
- `src/Opencode/KnowledgeGraphRuntime.fs` 里的 `commandQueue` / `backgroundJobs` / `writeQueues`

## 从原则演绎到当前架构

### Kernel：纯规则层

`src/Kernel/` 不是“公共工具箱”，而是“真正稳定的系统语义”。

它包含的内容大致分五类：

1. 领域状态机
   - `ReviewSession.fs` + `ReviewVerdict.fs`
   - `Nudge.fs` + `NudgeState.fs`
   - `KnowledgeGraph.fs` + `KnowledgeGraphMaintenance.fs`

2. 跨宿主共享格式与协议
   - `PromptFrontMatter.fs` + `PromptFragments.fs`
   - `LoopMessages.fs`
   - `ReviewPrompts.fs` / `SearchPrompts.fs` / `SubagentPrompts.fs` / `KnowledgeGraphPrompts.fs`
   - `CapsFormat.fs`

3. 工具/权限/意图元数据
   - `ToolCatalog.fs`
   - `Config.fs`
   - `Subagent.fs` + `SubagentIntents.fs`
   - `HostTools.fs`

4. 历史折叠与消息语义
   - `Messaging.fs` + `Message.fs`
   - `MessageDedup.fs` + `Dedup.fs`
   - `BacklogProjectionCore.fs` + `BacklogProjection.fs` + `WorkBacklog.fs`

5. 纯算法或纯解析
   - `FuzzyQuery.fs` / `FuzzyPath.fs` / `FuzzyFormat.fs`
   - `Executor.fs`
   - `TreeSitterKernel.fs`
   - `Domain.fs`
   - `Methodology.fs`

核心判断标准是：如果一个模块去掉 Node/宿主对象后仍然成立，它就应该进 Kernel。

### Shell：现实世界边界

`src/Shell/` 负责把 Kernel 需要的能力从 Node 世界里取回来：

- 文件系统：`FileSys.fs`、`WorkspaceFiles.fs`、`KnowledgeGraphFiles.fs`
- 搜索后端：`FuzzyFinderShell.fs`、`FuzzySearch.fs`、`FuzzyIteratorStore.fs`
- 执行器：`Executor.fs`、`ExecutorJavascript.fs`、`SessionExecutor.fs`
- tree-sitter：`TreeSitterShell.fs`
- 远端 API：`WebSearchApi.fs`
- 动态类型与错误分类：`Dyn.fs`、`ErrorClassify.fs`
- 编解码边界：`SubagentIntentsCodec.fs`、`WebSearchCodec.fs`、`BacklogSessionCodec.fs`、`WorkBacklogSchema.fs`
- 运行时 store/queue：`PromiseQueue.fs`、`SessionProjectionStore.fs`、`RuntimeScope.fs`（含 caps 与 fuzzy iterator）、`ChildAgentRegistry.fs`、`SessionExecutor.fs`
- review/nudge runtime：`ReviewRuntime.fs`、`NudgeRuntime.fs`、`ReviewReplaySync.fs`、`CapsFileCache.fs`、`CapsSynthCommon.fs`
- 跨进程锁：`KnowledgeGraphPortLock.fs`

这里的原则不是“把所有 IO 扔一起”，而是让每个 Shell 模块只承担一种外部能力，再把复杂流程留给上层编排。

### Opencode / Mux：两套适配面，四个入口

因为两个宿主家族对插件的暴露方式不同，所以项目没有强行用一层巨型通用抽象吞掉差异，而是保留两个适配面：

- `src/Opencode/`：直接面向 OpenCode / Mimocode 插件 hook、tool schema、消息变换
- `src/Mux/`：面向 Mux 的注册表、wrapper、slash command、delegate API

当前公开入口实际有四个：

- `src/Opencode/Plugin.fs`：OpenCode 插件入口
- `src/Opencode/PluginMimo.fs`：Mimocode 插件入口
- `src/Opencode/PluginMimoTui.fs`：Mimocode TUI 辅助入口（`/subagents` + `task`→sidebar todo 回填）
- `src/Mux/Plugin.fs`：Mux 注册表入口

二者共享同一个 Kernel，但各自保留宿主差异：

- schema 生成不同
- tool execute 签名不同
- 消息对象形态不同
- subagent 启动路径不同
- task/todowrite 命名与行为不同

这就是为什么项目同时存在：

- `src/Opencode/SubagentTools.fs` 与 `src/Mux/SubagentTools.fs`
- `src/Opencode/ReviewTools.fs` 与 `src/Mux/ReviewToolsMux.fs` 中的 submit-review 逻辑
- `src/Opencode/ToolSchema.fs` 与 `src/Mux/Wrappers.fs`

不是重复劳动，而是“共享语义，不强行共享协议”。

### knowledge graph 不是全局常开，而是目录存在性门控

当前实现里，knowledge graph 能力不是编译期开关，而是 workspace 内存在 `kg/` 目录时才接线：

- `src/Opencode/PluginCore.fs` 通过 `knowledgeGraphDirExists` 决定是否注册 `knowledge_graph_fetch` / `return_bookkeeper`
- `src/Mux/Plugin.fs` 也用同一条件决定工具目录与 bookkeeper runtime 接线
- `src/Opencode/KnowledgeGraphRuntime.fs`、`src/Mux/KnowledgeGraphTools.fs` 会在运行时再次守卫缺失 `kg/` 的场景

这意味着 knowledge graph 是 opt-in 的工作区能力，而不是所有会话默认暴露的工具。

## 关键实现细节为什么会这样写

### 1. review loop 为什么是 DU + front-matter + replay

因为 review 只有少数合法状态，所以 `src/Kernel/ReviewSession.fs` 用显式状态机表达。

因为 review 可能跨重启，所以 `src/Kernel/LoopMessages.fs` 不依赖自然语言，而把 `task` / `verdict` 写进 YAML front-matter。

因为历史才是事实，所以 `src/Opencode/MessageTransform.fs` 会在 store 为空时从历史重建 review 状态。

### 2. todo 为什么要求 `completedWorkReport`

因为上下文会折叠，旧回合的原始消息不一定还在；如果不把“完成了什么、改了哪些文件、踩了哪些坑”写成 durable report，后续任何 projection 都只能瞎猜。

所以：

- OpenCode 的 `todowrite`
- Mimocode 的 `task`

最终都被统一约束到 WorkBacklog 的 backlog 语义上。

### 3. knowledge graph 为什么拆成 `KnowledgeGraph` / `KnowledgeGraphRuntimeState` / `KnowledgeGraphRuntime` / `KnowledgeGraphRuntimeIO`

因为这里同时存在四种性质完全不同的东西：

1. knowledge graph 数据模型与 NDJSON 协议
2. 纯维护策略：何时 daily rewrite
3. 进程内 runtime 状态
4. 真实 IO：读写文件、拉起 bookkeeper、拿 port lock

如果把它们揉在一起，任何一点修改都会牵一整团。现在的拆法正好对应这四个层次：

- `src/Kernel/KnowledgeGraph.fs` + `src/Kernel/KnowledgeGraphCodec.fs`
- `src/Kernel/KnowledgeGraphPrompts.fs`
- `src/Kernel/KnowledgeGraphMaintenance.fs`
- `src/Kernel/KnowledgeGraphRuntimeState.fs`
- `src/Opencode/KnowledgeGraphRuntime.fs`
- `src/Opencode/KnowledgeGraphRuntimeIO.fs`

### 4. fuzzy search 为什么要“纯格式化/状态”和“Shell finder”分离

因为搜索真正不稳定的是后端 finder；而 query 组装、路径归一化、输出格式、iterator 语义是稳定规则。

所以：

- `src/Kernel/FuzzyQuery.fs` / `FuzzyPath.fs` / `FuzzyFormat.fs` 负责 query/format/state shape
- `src/Shell/FuzzyFinderShell.fs` 负责第三方 finder 生命周期
- `src/Shell/FuzzySearch.fs` 负责把二者接起来

这也是 typed iterator store 存在的原因：iterator 不是任意字符串，而是“find state / grep state”这两种有限状态之一；实例挂在 `RuntimeScope.IteratorStore`，由双宿主 fuzzy 工具注入。

### 5. nudge 为什么要分 kernel decision 和 runtime flow

因为“该不该 nudge”是纯判断，而“如何发 nudge”涉及宿主 API、错误、并发、abort。

因此：

- `src/Kernel/Nudge.fs`：纯决策与 dedup 语义
- `src/Kernel/NudgeState.fs`：事件到状态转移
- `src/Shell/NudgeRuntime.fs`：Mux 侧 runtime
- `src/Opencode/SessionLifecycleObserver.fs`：OpenCode 侧 hook

尤其 `SessionLifecycleObserver` 里把纯状态更新和异步 `session.prompt` 分离，是为了避免单一串行队列被 await 卡死。

### 6. 为什么大量提示词都用 YAML front-matter

因为提示词既要给 LLM 看，又要被程序回放/解析。纯 prose 对模型友好，但对恢复逻辑不友好；纯 JSON 对人类和模型都不够自然。

YAML front-matter 刚好处于中间：

- 顶部结构化字段可被程序精确识别
- 下方 prose 仍然适合模型阅读
- 重启后的 replay 只需要读取 scalar 字段，不必做全文 NLP

这就是 `src/Kernel/PromptFrontMatter.fs` 成为底层公共件的原因。

## 读代码时的推荐路径

如果想快速理解项目，不建议从插件入口直接往下扎。更高效的顺序是：

1. `src/Kernel/ReviewSession.fs`
2. `src/Kernel/Nudge.fs`
3. `src/Kernel/KnowledgeGraph.fs`
4. `src/Kernel/BacklogProjectionCore.fs` + `src/Kernel/BacklogProjection.fs` + `src/Kernel/WorkBacklog.fs`
5. `src/Kernel/PromptFragments.fs` + `src/Kernel/LoopMessages.fs`
6. `src/Shell/PromiseQueue.fs`
7. `src/Opencode/MessageTransform.fs`
8. `src/Opencode/KnowledgeGraphRuntime.fs`
9. `src/Opencode/PluginCore.fs`
10. `src/Mux/Plugin.fs`
11. `src/Opencode/Plugin.fs` (opencode entry)
12. `src/Opencode/PluginMimo.fs` (mimocode entry)

这个顺序是按“先看不变的规则，再看副作用，再看宿主拼装”排列的。

## 重构进度（对照保姆级指南）

**with-review / 全量重构任务** = **`TASK.md` + `保姆级重构指南.md` Phase A–G / §7–§8** 全量收口，**严禁**把验收缩窄为「小 bundle PASS」。Epic 行级跟踪与开放项见 [`REFACTOR_ACCEPTANCE.md`](REFACTOR_ACCEPTANCE.md)（非收窄 in-scope 清单）。下文阶段表为静态盘点；**未完成项**以 REFACTOR_ACCEPTANCE epic 表 + 本 README「已知技术债 / 有意边界」为准，不以「仅指南表完成」代替 TASK §2 收口。

对照 `TASK.md` 与 `保姆级重构指南.md` 的静态盘点（以源码与 `kg/` 重构记录为准，非运行时验证）：

| 项 | 状态 | 说明 |
| --- | --- | --- |
| Phase A 命名纠偏 | 完成 | `Magic*`→`WorkBacklog` / `BacklogProjection*` / `SessionProjectionStore`；`OllamaClient`→`WebSearchApi`；`HostReadExec`→`HostFunctionCapture`；`NudgeHook`→`SessionLifecycleObserver`；`TreeSitterKernel` 中补丁路径与工具分类已迁至 `PatchParser` / `ToolCatalog`。 |
| Phase B 边界强类型化 | 大体完成 | `Kernel/` 已基本不直接 `Dyn.*`；`opencodeNoMuxRef`。**切片**：`Kernel.ToolContext.ToolExecutionContext` + `Shell.ToolContextCodec`（`decodeOpencodeToolContext` / `decodeMuxConfig`，后者读 `sessionID`/`sessionId`/`session_id` 填入 `SessionId`）；**Review 双宿主** `ReviewToolsMux` / `ReviewTools` 已 `fromMuxConfig` / `fromOpencode`（`muxReviewUsesFromMuxConfig`、`opencodeReviewUsesFromOpencode`）；**Mux `submit_review` 参数** **`Shell.ReviewToolsCodec.decodeSubmitReviewArgs`**（`muxReviewUsesReviewToolsCodec`）；**Mux `Wrappers`** `applySyntaxCheck` 已 `fromMuxConfig` + `runtime.Execution.Directory`（`muxWrappersSyntaxUsesFromMuxConfig`）；Opencode **`SubagentTools`** spawn 链已 `fromOpencode`（`opencodeSubagentToolsUsesFromOpencode`，禁 `decodeOpencodeToolContext`）；**双宿主 meditator/browser** 主路径 **`Shell.ToolArgsDecode.decodeToolInvocation`** → **`Typed` (Meditator/Browser)**（字段解码由 **`SubagentSimpleArgsCodec`** 在 **`ToolArgsDecode` 内部**复用；双宿主 execute 禁直解，**`opencodeSubagentToolsUsesToolArgsDecode`** / **`muxSubagentToolsUsesToolArgsDecode`**；`muxSubagentToolsUsesSimpleArgsCodec` 探针仅约束 Shell 解码层）；**双宿主 coder/investigator** `intents` 经 **`Shell.ToolArgsDecode`**（边界 **`intentsField`** + **`SubagentIntentsCodec.parse*Intents`** → **`CoderBatch`/`InvestigatorBatch`**；`subagentToolsUseDecodeIntentsField` 要求 **`ToolArgsDecode` 禁 `decodeIntentsField`**、双宿主 **`SubagentTools` 禁直解 intents）；**Mux AI 设置** `Shell.MuxAiSettingsCodec`（`decodeMuxDelegateConfig` / `decodeMuxParentRuntimeEnv` / `decodeAgentAiEntryScalars`，Mux **`AiSettings`** 消费，`muxAiSettingsUsesMuxAiSettingsCodec`）；**Opencode hook 输入** `Shell.OpencodeHookInputCodec`（`opencodeHookExecuteUsesFromOpencode`、`opencodeChatHooksUsesHookInputCodec` 等；**Opencode `MessageTransform`** agent 经 **`resolveMessagesTransformAgent`**，`opencodeMessageTransformUsesResolveMessagesTransformAgent`，与 Mux **`decodeMuxMessagesTransformInput`** 对称）；**消息投影 output** `Shell.ChatTransformOutputCodec`（`messageTransformUsesChatTransformOutputCodec`）；**Mux 子 workspace** `Shell.MuxWorkspaceCodec`（`muxMessageTransformUsesMuxWorkspaceCodec`）；**Mux hook 输入** `Shell.MuxHookInputCodec`（**`Mux MessageTransform`** `decodeMuxMessagesTransformInput`，`muxMessageTransformUsesMuxHookInputCodec`；**Mux `Plugin`** `tool.execute.after` 经 `decodeMuxToolExecuteAfterInput`）；**Opencode `SessionIo`** prompt `model`/`modelString` 已 **`Shell.OpencodeSessionPromptCodec`**（`sessionIoUsesOpencodeSessionPromptCodec`）；子会话 **`create`→`data.id`** 已 **`Shell.OpencodeSessionSpawnCodec`**（`sessionIoUsesOpencodeSessionSpawnCodec`）；**Opencode `PluginCore`** `createCoreServices` directory 已 `fromOpencode`→`Execution.Directory`（`opencodePluginCoreUsesFromOpencode`）；**Opencode `KnowledgeGraphTools`** 已 `fromOpencode` + `runtime.Execution`（`opencodeKnowledgeGraphToolsUsesFromOpencode`）；**Mux `KnowledgeGraphToolDefs`** fetch/return 已 `KnowledgeGraphToolsCodec` + `fromMuxConfig`（`muxKgToolDefsUsesKnowledgeGraphToolsCodec`、`muxKgToolDefsUsesFromMuxConfig`）；**Mux `KnowledgeGraphTools`** `StartBookkeeperAppend` root 已 `fromMuxConfig`，失败时 **`muxConfigDirectoryFallback`**（`muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig`）。**Mux `HostTools` read/write**→**`FileToolsCodec`**；**Wrappers todo**→**`WorkBacklogToolsCodec`**；**Opencode patch hook**→**`PatchToolsCodec`**；**Mux Delegate**→**`DelegateToolsCodec`**；**msg.parts 文本**→**`HostMessagePartCodec`**；**Mux 只读 executor**→**`peekExecutorMode`**（门禁名见本轮已修）**切片**：**`Shell/OpencodeAgentConfigWire`**（`mergeConfigObj` / `applyAgentConfigFor` / `disableMimoMemoryAndCheckpoint`，**`agentConfigUsesOpencodeAgentConfigWire`**；**`Opencode/AgentConfig`** 禁 **`Dyn.keys`** / **`Dyn.get cfg`**，user-agent scalars 经 **`OpencodeAgentConfigCodec.decodeUserAgentScalars`**）；**`Opencode/SessionIo`** **`startSubagentSession`** → **`Promise<Result<string, DomainError>>`**（**`OpencodeSessionSpawnCodec`**）。**有意边界**：双宿主 **`MessagingCodec`** 消息级 wire 分叉已 **`MESSAGING_WIRE.md`** 文档化（part 已 **`MessagingPartCodec`** SSOT）；**`OpencodeAgentConfigCodec`** 读侧 **`PermissionOverrides`/`ToolsOverrides`/`Mcps`** 已为 **`Map`/数组**，写回宿主仍经 **`permissionMapToObj`/`toolsMapToObj`/`mergeConfigObj`**；部分 Chat/Command hook 与 Opencode **`SubagentTools` schema（Zod DSL）** 仍宿主侧 `Dyn`，属渐进收口。 |
| Phase C 抽公共内核 | 完成 | caps / 审查回放 / `SubagentToolPolicy` 等同上。**切片**：`Shell.MessageTransformCore`（`applyBacklogProjection`、**`backlogSessionOpsFrom`**，`messageTransformUsesBacklogSessionOpsFrom`）、`Shell.ReadDedupOpenCode`（Opencode read 去重）、**`Shell.ReadDedupMuxPlugin`**（Mux dynamic-tool/`msg.parts` 去重，`muxMessageTransformUsesReadDedupMuxPlugin`；`Mux.ReadDedup` 仅 model 路径转发）、**`Shell.HostMessagePartCodec`**（`getMessageParts` / `decodeDynamicToolReadOutput` / `extractTextLinesFromParts`，`MessageTransformCommon`+`ReadDedupMuxPlugin` 消费）、`Shell.MessageTransformCommon`、`Shell.JsonSchemaBuilders` + `Shell.MuxJsonSchema`、`Kernel.HostTools.muxSpawnToolUniverse`；**`Shell.SubagentPromptBuild`**（`parallelPromptsFromIntents` / `meditatorPromptFromFiles` / `buildMeditatorSections`，双宿主 `SubagentTools` 消费，`subagentToolsUseKernelPromptHelpers`）；**`Shell.SubagentSpawn`**（`runParallelSpawns` / `runParallelSpawnsWithAbort`，双宿主并行 coder/investigator/`bindParallel` 消费，`subagentToolsUseSubagentSpawn`；Opencode 禁内联 `Promise.all`/`joinReports`，Mux 禁内联 `AbortController`/`Promise.all`/`joinReports`）；Mux `MessageTransform` 的 `BacklogSession` 由 `Plugin.createRegistration`+`RuntimeScope` 注入（`muxMessageTransformNoModuleBacklogSession`）。**切片**：**`Shell.MessageTransformPipeline`**（`runMessageTransformPipeline` + `MessageTransformPlan`，双宿主 `messagesTransform` 经 pipeline 注入 dedup/caps 回调，`messageTransformUsesPipeline`；`messageTransformUsesMessageTransformCore` 要求 Mux 不直调 `applyBacklogProjection`，Opencode `compactingHandler` 仍直调）；**`Shell.MessagingEncodeHelpers`**（`replacePartsOnRawMessage`，双宿主 encode 分支，`dualHostMessagingCodecUsesEncodeHelpers`）。**切片**：**`Shell.MessageTransformHostEntry`**（`ReviewReplayMode` + `runHostMessagesTransform` + `replayReviewForMode`，Opencode `IfStoreEmpty`、Mux `Always`，**`messageTransformUsesHostEntry`**，双宿主禁直调 `ReviewReplaySync`/`runMessageTransformPipeline`）；**`Shell.MessageTransformHostHooks`**（`CapsLoadPolicy` + `loadCapsForScope` + `loadKgPreludeForAgent`，Opencode caps **`AllowEmptyDirectory`**、Mux **`RequireDirectory`**，**`messageTransformUsesCapsKgHostHooks`**）。**有意边界**：Opencode **`ToolSchema.fs` Zod DSL** 与 Mux **`mkSchema`** 仍各宿主一份形状层（描述/必填键已 **`ToolCatalog`** SSOT）；共享执行与提示词已在 **`Shell.SubagentPromptBuild`** / **`SubagentSpawn`** / 双宿主 **`ToolArgsDecode`** 执行链。 |
| Phase D 统一错误模型 | 大体完成 | `DomainError` / `formatDomainError` 已在 web、executor、部分 Mux 工具使用。**切片**：`Shell.WebToolsCodec`（`decodeWebsearchArgs` / `decodeWebfetchArgs` → `Result<_, DomainError>`）；**`Shell.ExecutorToolsCodec`**（双宿主 executor，`opencodeExecutorUsesExecutorToolsCodec`、`muxHostToolsExecutorUsesExecutorToolsCodec`）；**`Shell.ReviewToolsCodec`**（`submit_review` + **`return_reviewer`**，`opencodeReviewUsesReviewToolsCodec`）；**Opencode `ReviewTools`** `submit_review` / `return_reviewer` 参数解码失败经 **`wireDecodeFailure`**，**`getClientFromPluginCtx`** 失败经 **`wireEncodeToolError "OpencodeClient"`**（**`ExecutorTool`** / **`SubagentTools`** / **`ReviewTools`** 对称，**`opencodeToolsUseWireEncodeForClient`**，禁 client 分支 **`formatDomainError`**）；**Opencode `HookExecute`** Mimocode **`apply_patch`** 解码失败经 **`wireEncodeToolError "apply_patch"`**（**`opencodeHookExecuteUsesPatchToolsCodec`**；**`IntegrationToolDefSpecs`** 断言与 **`ToolResult`** SSOT 对齐）；**`Shell.KnowledgeGraphToolsCodec`**（fetch/return/draft，`opencodeKgUsesKnowledgeGraphToolsCodec`、`muxKgToolDefsUsesKnowledgeGraphToolsCodec`）；**Opencode `KnowledgeGraphTools`** `knowledge_graph_fetch` / `return_bookkeeper` 参数解码失败经 **`wireDecodeFailure`**（**`opencodeKgUsesKnowledgeGraphToolsCodec`** 扩展：**`ToolExecute`**、禁 decode 分支 **`formatDomainError`**）；**`Shell.SubagentSimpleArgsCodec`**（meditator/browser）；**coder/investigator intents** 见 **`Shell.ToolArgsDecode`** + **`SubagentIntentsCodec`**（Phase B/G，非 `decodeIntentsField` 主路径）；**`Shell.FileToolsCodec`** / **`WorkBacklogToolsCodec`** / **`PatchToolsCodec`** / **`DelegateToolsCodec`**（read/write/todo/patch/delegate 参数 → `Result<_, DomainError>`，门禁见 Phase B）；**`ExecutorToolsCodec.peekExecutorMode`**（hook 只读识别，不要求完整 executor 参数）。Mux `websearch` / `webfetch` 先 `match` codec + `fromMuxConfig`，失败 `formatDomainError`，上游 API 失败走 `ToolCopy.webToolFailed`（`webToolsUsesWebToolsCodec`、`webToolsUsesWebfetchCodec`）；**Mux fuzzy** 配置/工作区缺失由 `decodeMuxConfig`（经 `fromMuxConfig`）返回 `InvalidIntent`，**find/grep 参数** **`Shell.FuzzyToolsCodec`**（`dualHostFuzzyUsesFuzzyToolsCodec`、`muxHostToolsFuzzyUsesFuzzyToolsCodec`、`opencodeSearchToolsUsesFuzzyToolsCodec`；单测 **`FuzzyToolsCodecTests`**）。单测：`tests/WebToolsCodecTests.fs`、`FileToolsCodecTests.fs`、`WorkBacklogToolsCodecTests.fs` 等 codec 套件。**切片**：**`Opencode/SessionIo`** **`startSubagentSession`** → **`Promise<Result<string, DomainError>>`**，**`runSubagentCoreResult`** spawn **`Result`** 链（**`sessionIoUsesOpencodeSessionSpawnCodec`** 加强）。**切片**：**`Opencode/SessionIo`** 公开 **`runSubagent`** / **`runSubagentWithCleanup`** → **`Promise<Result<string, DomainError>>`**（`sessionIoRunSubagentReturnsResult`）。**有意边界**：decode / 配置解析失败经 **`wireDecodeFailure`** / **`wireEncodeToolError`**（**`Shell.ToolExecute`** + **`Kernel.ToolResult`**）；非 decode 业务拒绝与上游 API 失败仍可能直返 **`ToolCopy`** / 成功文案；宿主 **wire** 仍为 **`Promise<string>`**；**`SubagentTools` schema** 生成路径与 execute 的 **`Result<_, DomainError>`** 解码链分离。 |
| Phase E 收拢可变状态 | 完成 | `Shell.RuntimeScope`：每 registration 独立 scope；**`CapsFileCache` 仅 `getOrLoadCapsFilesForScope`**（禁 `getOrLoadCapsFiles` / `getDefault`，`capsFileCacheNoGetOrLoadCapsFilesDefault`）；**`TypedIteratorStore`**、**`sessionQueues`** 已挂 scope；**生产路径** Opencode `ExecutorTool` / Mux `HostTools` executor 经注入 `sessionScope.EnqueuePerSession`（`SessionExecutor.createForScope`、`pluginInjectsSessionScopeForExecutor`）；fuzzy 热路径注入 `scope.IteratorStore`，**`FuzzySearch.resolveStore` 未传 `store` 即 `Error`**（`fuzzySearchNoDefaultIteratorStore`）。**BacklogSession** 构造器 **`scope` 必填**，双宿主禁 `defaultArg` / `getDefault`（`backlogSessionNoGetDefaultFallback`）；回放仅 **`BacklogSessionCodec.reportFromFlatPartWithProjection`**，双宿主 **`replayBacklogWith`** 注入 **`scope.Projection`**，与 **`CaptureReport`** 写入同一 **`ProjectionStore`**；**`BacklogSessionCodec`** 已删 **`reportFromFlatPart`** 退路（`backlogSessionCodecNoReportFromFlatPartDefault`）；**Mux `Wrappers`** todo **`captureTodoReport`** 改 **`projection.CaptureReport`**，**`createAllWrappers(scope)`**（`muxWrappersCaptureUsesProjectionNotModuleCapture`）；**已删** `RuntimeScope` 模块级 **`captureReport`/`takeReport`/`tryGetReport`/`storeBacklog`/`tryGetBacklog`**（`runtimeScopeNoModuleProjectionHelpers`）。**`RuntimeScope.getDefault()` / `resetDefaultForTesting` 已删除**（`runtimeScopeNoGetDefault`；生产路径仅 registration 注入 scope）。**切片**：**`RuntimeScope.capsInflight`** + **`GetOrLoadCapsInflight`**（**`capsFileCacheUsesInflight`**，同 key 复用进行中 Promise，**`ClearCapsFiles`** 同时清空 inflight）。**余量**：模块级 `enqueuePerSession` 退路已禁（`sessionExecutorNoModuleMutableQueues`），测试须显式 `createForScope`。 |
| Phase F 拆 KnowledgeGraphRuntime | 完成 | **存储切片** `Shell.KnowledgeGraphStorage`；**编排切片** `Shell.KnowledgeGraphWorkflow`（`BackgroundJobSink`、`trackBackgroundJob` / `recordLaunchResult` / …）；**后台 bookkeeper 启动** `Shell.KnowledgeGraphBookkeeperLaunch`（`queueBackgroundLaunch` + `launchBackgroundSession`；Opencode `KnowledgeGraphRuntime.launchBg` 经 `queueBackgroundLaunch`，`knowledgeGraphBookkeeperLaunchInShell`）；**Mux** `KnowledgeGraphTools.launchBg` 经 **`queueMuxBackgroundLaunch`**（`delegateToSubAgent` 以回调注入，禁 Shell→Mux 硬引用）。**双轨 launch 原语**：Opencode 子会话 `session.create` + `session.prompt` fire-and-forget；Mux `taskService` + `waitForAgentReport` 同步等报告。**维护调度 SSOT** `Shell.KnowledgeGraphMaintenanceRun.runMaintenanceIfDue`（双宿主禁本地 `launchIfDue`，`knowledgeGraphRuntimeNoLocalLaunchIfDue`）。**SessionMessages 切片** `Opencode/KnowledgeGraphSessionMessages.fs`（`fetchSessionMessageArray` / `loadSessionMessages` / `tryResolveJobContext`）；`KnowledgeGraphRuntimeIO` 仅保留存储侧编排（`buildEntries` / `submitForKind` 等），session 消息 IO 经 alias 引用 SessionMessages（`knowledgeGraphSessionMessagesNotInRuntimeIO`）。纯状态在 `Kernel.KnowledgeGraphRuntimeState`；测试在 `*TestHooks` / `KnowledgeGraphWorkflowTests` / `KnowledgeGraphBookkeeperLaunchTests`。 |
| Phase G 工具 DI | 大体完成 | **`Kernel.ToolContext.ToolExecutionContext`** + `Shell.ToolContextCodec`；**`Shell.ToolRuntimeContext`**（`fromMuxConfig` / `fromOpencode`）。**参数切片**：`Shell.WebToolsCodec`、`ExecutorToolsCodec`、`ReviewToolsCodec`、`KnowledgeGraphToolsCodec`、`Shell.FuzzyToolsCodec`（`dualHostFuzzyUsesFuzzyToolsCodec`）、`SubagentSimpleArgsCodec`、`FileToolsCodec`、`WorkBacklogToolsCodec`、`PatchToolsCodec`、`DelegateToolsCodec`（见 Phase D）。**切片**：**`Kernel.ToolArgs`**（Read/Write/Meditator/Browser/Websearch/Webfetch/Executor/TodoWrite/KnowledgeGraphFetch/ReturnBookkeeper/ApplyPatch/SubmitReview；禁 CoderIntents/InvestigatorIntents obj case，`kernelToolArgsExists`）；**`Shell.ToolArgsDecode`**（`decodeToolArgs` / **`decodeToolInvocation`** SSOT：`read`/`write`/子代理 batch/`meditator`/`browser`/`websearch`/`webfetch`/`executor`/`todowrite`/`knowledge_graph_fetch`/`return_bookkeeper`/`apply_patch`/`submit_review`；**`DecodedToolInvocation`**：`Typed` | **`CoderBatch`** | **`InvestigatorBatch`**（禁 `intents:obj`，`decodedToolInvocationNoObj` / `toolArgsDecodeExists` / **`toolArgsDecodeCoversMajorTools`**）；**`FuzzyToolsCodec.validateFuzzyFirstCall`**（首呼 pattern、续页 iterator、`limit`/`context` ≥ 1，非恒 Ok）；**`tests/SubagentToolExecuteTests`**（双宿主 decode 失败不调用 runCore/runMux，`Tests.fs` 登记于 **`ToolArgsDecodeTests`** 之后）；**`Shell.SubagentToolExecute`**（Opencode，`opencodeSubagentToolsUsesToolArgsDecode`）；**`Shell.MuxSubagentToolExecute`** + 瘦 **`Mux.SubagentTools`**（`muxSubagentToolsUsesToolArgsDecode`）。**`ToolExecutionContext`** 已无 **`AbortSignal`**（Kernel 边界无 obj 字段），中止仅 **`IToolRuntimeContext.AbortSignal`**（**`toolRuntimeContextAbortFromShellCodec`**：`fromOpencode`→**`getAbortSignalFromContext`**，`fromMuxConfig`→config **`abortSignal`**）。Mux web 工具已用 **`IToolRuntimeContext.AbortSignal`**；**Mux** `HostTools` **read** / **write**（**`FileToolsCodec`**，`muxHostToolsReadWriteUsesFileToolsCodec`）/ **fuzzy_find** / **fuzzy_grep** / **executor** 已 `fromMuxConfig` + `formatDomainError` + `runtime.Execution.Directory`（fuzzy 参数 **`FuzzyToolsCodec`** + `muxHostToolsFuzzyUsesFuzzyToolsCodec`；上下文/目录 `muxHostToolsFuzzyUsesFromMuxConfig`；executor `muxHostToolsExecutorUsesFromMuxConfig`）；**Mux** `SubagentTools` 四工具统一 **`executeMuxSubagentTool`** + **`decodeToolInvocation`**（`muxSubagentToolsUsesToolArgsDecode`；meditator/browser 经 **`ToolArgsDecode`**，不再 **`SubagentSimpleArgsCodec`** 直解，`muxSubagentToolsUsesSimpleArgsCodec` 探针改向 Shell）；**Mux** **`ReviewToolsMux`** `submit_review`（**`ReviewToolsCodec`**，`muxReviewUsesReviewToolsCodec`）、**`Wrappers`** syntax + **todo**（**`WorkBacklogToolsCodec`**，`muxWrappersTodoUsesWorkBacklogToolsCodec`）、**`KnowledgeGraphTools`** `StartBookkeeperAppend`、**`KnowledgeGraphToolDefs`** 已 `fromMuxConfig`（`muxReviewUsesFromMuxConfig`、`muxWrappersSyntaxUsesFromMuxConfig`、`muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig`、`muxKgToolDefsUsesFromMuxConfig`）；**Mux `Delegate`** 上下文/任务结果经 **`DelegateToolsCodec`**（`muxDelegateUsesDelegateToolsCodec`）；**Opencode `HookExecute`** patch 经 **`PatchToolsCodec`**（`opencodeHookExecuteUsesPatchToolsCodec`）；**Opencode** `SearchTools`（web + fuzzy 会话/目录）、**`ExecutorTool`**、**`ReviewTools`**（含 return_reviewer codec）、**`SubagentTools`**、**`PluginCore`**、**`KnowledgeGraphTools`**、**hook 链**（`OpencodeHookInputCodec`）已 `fromOpencode` + `runtime.Execution`（`opencodeSearchToolsUsesWebToolsCodec`、`opencodeExecutorUsesFromOpencode`、`opencodeReviewUsesFromOpencode`、`opencodeSubagentToolsUsesFromOpencode`、`opencodePluginCoreUsesFromOpencode`、`opencodeKnowledgeGraphToolsUsesFromOpencode`）。**有意边界**：**`ToolArgsDecode.decodeToolInvocation`** 为双宿主主工具 execute SSOT；**`IToolRuntimeContext`** 已覆盖 web/read/write/fuzzy/executor/子代理/KG/review/todo/patch/delegate 等工具热路径；**`HookExecute`** 经 **`PatchToolsCodec`** + **`fromOpencode`**。非工具 hook（chat/command、部分 **`MessageTransform`** 输入）不强制 **`IToolRuntimeContext`**。宿主 **`tools`** 配置树仍为 obj（见技术债）。 |
| TASK §7 文案 SSOT | 大体完成 | `SubagentPrompts`、`ReviewPrompts`、`Methodology` 等已有内核常量；**schema 切片**：`Kernel.ToolCatalog`、`Shell.JsonSchemaBuilders`、`Shell.MuxJsonSchema` + `Mux.Wrappers`。**`submit_review` 双宿主**：Mux `ReviewToolsMux` + Opencode `ReviewTools`/`ToolSchema` 消费 `ToolCatalog` + `ToolCopy`（`muxSubmitReviewRequiresWorkspaceId`、`submitReviewNotNeeded` / `submitReviewInProgress` / `opencodeSubmitReviewInProgress`，`muxReviewUsesToolCopy`）。**Mux read/write**：`HostTools` 消费 `ToolCatalog.description` + `Params`（`muxHostToolsReadWriteUsesToolCatalog`）。**Opencode `ToolSchema`**：工具 **`description`** 经 **`toolDescription`→`ToolCatalog.description`**（coder/investigator/web/KG/review 等），**`return_bookkeeper`** 草稿条目子字段经 **`Params.kgEntryId` / `kgEntryEntity` / `kgEntryFact`**（`opencodeToolSchemaDescriptionsFromCatalog`）；四子代理 **`parameters`** 经 **`subagentZodShape`** + **`subagentRequiredKeys`** 与 Mux **`mkSchema`** 同源必填键（**`subagentToolsUseToolCatalogRequiredKeys`**）。**拒绝语切片**：`Kernel.ToolCopy` — Mux `muxToolRequiresWorkspaceId`；**Mux fuzzy** 工作区/配置错误改 **`fromMuxConfig`** + **`formatDomainError`**（`decodeMuxConfig`），HostTools 禁直引 `muxFuzzyFindRequiresWorkspaceId` / `muxFuzzyGrepRequiresWorkspaceId`（`muxHostToolsFuzzyUsesToolCopy`、`muxHostToolsFuzzyUsesFromMuxConfig`）；web `webSearchRequiredField` / `webFetchRequiredField` / `webToolFailed`；**executor** `executorRequiresSession`、`executorInvalidLanguage`（**Mux** `HostTools` executor，`muxHostToolsExecutorUsesFromMuxConfig`；Opencode `ExecutorTool`，`opencodeExecutorUsesToolCopy`）；Opencode fuzzy 无会话 `toolRequiresActiveSession`（`SearchTools`，`opencodeSearchToolsUsesToolCopy`）；`muxSubagentToolsUsesToolCopy`。**`CommandHooks`**：`/with-review` 等审查文案改引 **`ToolCopy`**（`reviewAlreadyActiveMessage` / `preReviewPassedMessage` / `preReviewCouldNotComplete` / `withReviewPreReviewFeedbackHeader`，`commandHooksUsesToolCopyReviewMessages`）。**有意边界**：Opencode **`ToolSchema.fs` Zod 形状层** 与 Mux JSON schema 双轨保留（描述/参数 SSOT 已 **`ToolCatalog`/`Params`**）；零星 hook/spawn 字段说明可继续收拢，非阻塞。 |
| TASK §8 权限语义化 | 大体完成 | `ToolPermission.classifyTool` → `ToolSemantic` + `canUseForHost` 为双宿主主路径；工具族以 `Set` 精确匹配为主（`knownAgentSet`、`blockedShellTaskGrepSet`、`todoFamilySet`、`writePatchFamilySet` 等），辅以 `return_` 前缀、`stealth-browser` 前缀等有限规则；`ask_user_question` 等与 `websearch`/`submit_review` 等同入 `SubagentWebSkillOrSubmit`；Mux 侧经 `normalizeToolName` / canonical 集合归一后再分类。新增工具须在 Set 与语义分支补项。 |

### TASK.md §2 对照（全量 epic，非缩窄 bundle）

`TASK.md` §2（结构不当）是仓库级问题清单；下表跟踪**全量 refactor epic** 相对 §2 的进度。Phase A–G / §7–§8 细项见上表；行级开放项见 **`REFACTOR_ACCEPTANCE.md` §2**。

| TASK §2 条 | Epic 状态 | 开放项 / 有意边界 |
| --- | --- | --- |
| §2.1 边界类型丢失（`obj` / `Dyn`） | **推进中**：`Kernel/` 禁业务 `Dyn.*`；工具/ hook 参数经 `Shell/*Codec` + **`ToolArgsDecode`**；**`Shell/ChatHookOutputCodec`** 收拢 chat **`message.tools`**（`chatHooksUsesChatHookOutputCodec`）；**`encodeAgentScalarsRecord`** + **`withRoleDefaultsFor`** 降 **`mergeConfigObj(empty,userAgent)+setKey`** 模式。 | 宿主 **chat/command hook 整体 payload** 仍为 `obj`；wire 层 **`tools`** 仍经 **`toolsMapToObj`** 写回 message。 |
| §2.2 Mux/Opencode 平行重复 | **切片完成**：`MessageTransformPipeline` / `SubagentSpawn` / `ToolArgsDecode` 等 Shell+Kernel SSOT；**未**上单体 **`HostAdapter`**。 | 保姆级指南 §5.3：差异未稳定前不抽巨型 adapter；子会话 IO 与 messaging wire 分叉见 **`MESSAGING_WIRE.md`**。 |
| §2.3 领域错误与字符串混用 | **推进中**：codec/spawn **`Result<_, DomainError>`**；**`Kernel.ToolResult`**（**`wireEncodeResult`** / **`wireEncodeToolError`**）为工具失败文案 SSOT；**`ToolCopy`** / **`SubagentToolExecute`** reject 路径消费同一编码；**Opencode `ReviewTools`**（submit/return_reviewer）、**`ExecutorTool`** / **`SubagentTools`**（**`getClientFromPluginCtx`**→**`wireEncodeToolError "OpencodeClient"`**，**`opencodeToolsUseWireEncodeForClient`**）、**`KnowledgeGraphTools`**（**`knowledge_graph_fetch`** / **`return_bookkeeper`** decode → **`wireDecodeFailure`**，**`opencodeKgUsesKnowledgeGraphToolsCodec`**）与 **`HookExecute`**（apply_patch decode）已接入同一 wire 编码。 | 宿主 **wire** 仍为 **`Promise<string>`**；成功路径直返文本，非 `ToolResult` record 序列化；非 decode 路径未全收敛至 **`wireEncode*`**。 |
| §2.6 工具 DI 过于原始 | **大体完成**：**`decodeToolInvocation`** + **`IToolRuntimeContext`** / **`ToolExecutionContext`** 覆盖主工具热路径。 | 未统一为指南 §9.2 的 **`Async<ToolResult>`** 签名；无 Reader/容器式 DI，以显式 context + codec 注入为主。 |

**历史批次**（绿测计数随迭代递增；当前 **6233** 见上文全量口径）。曾验 **6233** 绿（**6233 passed** / **380 tests**）：**Opencode `KnowledgeGraphTools`** `knowledge_graph_fetch` / `return_bookkeeper` 解码失败经 **`wireDecodeFailure`**（**`opencodeKgUsesKnowledgeGraphToolsCodec`** 扩展：**`ToolExecute`**、禁 decode 分支 **`formatDomainError`**）；**`ArchitectureTests.opencodeKgUsesKnowledgeGraphToolsCodec`** 门禁登记 **`Tests.fs`**。曾验 **6229** 绿（**6229 passed** / **379 tests**）：**Opencode `ExecutorTool`** / **`SubagentTools`** **`getClientFromPluginCtx`** 失败改 **`wireEncodeToolError "OpencodeClient"`**（与 **`ReviewTools`** 对称，禁 client 分支 **`formatDomainError`**）；**`ArchitectureTests.opencodeToolsUseWireEncodeForClient`** 门禁登记 **`Tests.fs`**。曾验 **6214** 绿（**6214 passed** / **379 tests**）：**Opencode `ReviewTools`** `submit_review` / `return_reviewer` 与 **`HookExecute`** `apply_patch` 解码失败经 **`wireDecodeFailure`** / **`wireEncodeToolError`**（**`opencodeReviewUsesReviewToolsCodec`** 扩展、`opencodeHookExecuteUsesPatchToolsCodec`；**`IntegrationToolDefSpecs`** 对齐 **`wireEncodeToolError "apply_patch"`**）。曾验 **6200** 绿：**Mux `ReviewToolsMux`** `submit_review` 与 **`Wrappers`** todo decode 失败经 **`wireDecodeFailure`**（与 **`HostTools`** / **`WebTools`** 同 **`Shell.ToolExecute`** 契约）；**`HOST_OBJ_BOUNDARY.md`** §2.1 宿主 `obj` 库存与收敛路线图；**`ArchitectureTests.hostObjBoundaryDocumented`** 门禁。曾验 **6195** 绿：**Mux `HostTools`** / **`WebTools`** decode 与 **`fromMuxConfig`** 失败统一经 **`Shell.ToolExecute`**：**`fromMuxConfig`→`wireEncodeToolError "MuxConfig"`**，参数解码→**`wireDecodeFailure`**（fuzzy_find/fuzzy_grep/read/write、websearch/webfetch），executor→**`wireDomainFailure "Executor"`**（与 Opencode **`ExecutorTool`** / **`SearchTools`** 对称，禁 decode 路径 **`resolveStr(formatDomainError)`**）；**`ArchitectureTests`** 新增 **`muxHostToolsWireDecodeFailures`**、**`muxWebToolsUsesWireDecodeFailure`**；**`muxHostToolsFuzzyUsesFromMuxConfig`** 改要求 **`wireEncodeToolError "MuxConfig"`**（不再要求 **`formatDomainError`**）；**`dualHostFuzzyUsesFuzzyToolsCodec`** 扩展 Mux **`wireDecodeFailure`** fuzzy 检查。曾验 **6173** 绿：**`Shell/ToolExecute.wireDomainFailure`**；Opencode **`ExecutorTool`** / **`SearchTools`** decode 失败改 **`wireDomainFailure`** / **`wireDecodeFailure`**（禁 decode 路径直调 **`formatDomainError`**）；**`opencodeExecutorUsesExecutorToolsCodec`**、**`opencodeSearchToolsUsesFuzzyToolsCodec`**、**`opencodeSearchToolsUsesWebToolsCodec`**；**`ArchitectureTestsSupport`** 仅辅助拆分（**`ArchitectureTests.Foundation`** 子模块因 Fable 单文件单 `module` 约束回退）。曾验 **6170** 绿：**`Shell/ToolExecute`**（`wireDecodeFailure` / `mapDecodeError` / `runDecodedToWire`，子代理 decode 失败经 **`Kernel.ToolResult.wireEncodeToolError`**）；**`tests/ArchitectureTestsSupport`** 自 **`ArchitectureTests`** 拆分 `requireFile` / `nonCommentCode` 等探针辅助；**`ArchitectureTests.toolExecuteWireHelperExists`**，**`opencodeSubagentToolsUsesToolArgsDecode`** / **`muxSubagentToolsUsesToolArgsDecode`** 扩展要求 decode 错误路径含 **`wireDecodeFailure`**；**`vibe-fs.fsproj`** Shell 段 **`ToolArgsDecode`→`ToolExecute`→`SubagentToolExecute`**，Tests 段 **`ArchitectureTestsSupport`** 先于 **`ArchitectureTests`**。曾验 **6148** 绿：**`Kernel.ToolResult`** wire SSOT（**`wireEncodeResult`** / **`wireEncodeToolError`**）；**`Shell/ChatHookOutputCodec`** 收拢 chat **`message.tools`**（**`chatHooksUsesChatHookOutputCodec`**）；**`encodeAgentScalarsRecord`**；§2.1 **ChatHooks** 已移除 chat tools 路径上的 **`Dyn.keys`**。曾验 **6100** 绿：Opencode **`ToolSchema.subagentZodShape`** + **`ToolCatalog.subagentRequiredKeys`** 与 Mux 四子代理 **`mkSchema`** 必填键 SSOT 对称（**`subagentToolsUseToolCatalogRequiredKeys`** 扩展 Opencode **`SubagentTools`**）。曾验 **6050** 绿：**`Shell.MessageTransformHostHooks`**（`CapsLoadPolicy`、`loadCapsForScope`、`loadKgPreludeForAgent` + `Kernel.Config.canUse`）；双宿主 **`MessageTransform`** 接线，**`messageTransformUsesCapsKgHostHooks`**。此前（**6017** 绿）：**`Kernel.ToolCatalog.subagentRequiredKeys`** + Mux 四子代理 **`mkSchema`** 必填键 SSOT（**`subagentToolsUseToolCatalogRequiredKeys`**）；**`ToolArgsDecode.intentsField`** 改 **`SubagentIntentsCodec.intentsRawFromArgs`**；既有探针 **`opencodeHookSchemaUsesIntentsRawFromArgs`**、**`messagingWireForkDocumented`**（**`MESSAGING_WIRE.md`**）。此前（**6003** 绿）：**`Kernel.ToolArgs`** 扩展 web/executor/todo/KG/patch/review；**`Shell.ToolArgsDecode.decodeToolInvocation`** 覆盖上述主工具（**`ToolArgsDecodeTests`** + **`ArchitectureTests.toolArgsDecodeCoversMajorTools`**）；**`FuzzyToolsCodec.validateFuzzyFirstCall`** + 双宿主 fuzzy 接线（`dualHostFuzzyUsesFuzzyToolsCodec`、`FuzzyToolsCodecTests`）；**`tests/SubagentToolExecuteTests`**（`subagentToolExecuteEmptyBatchGuard`）；**`vibe-fs.fsproj`** 登记 **`SubagentToolExecuteTests.fs`**（紧接 **`ToolArgsDecodeTests.fs`**）。此前（**5949** 绿）：**Mux `ReviewToolsMux.submit_review`** → **`ReviewToolsCodec`**（`muxReviewUsesReviewToolsCodec`）；README 与 **`subagentToolsUseDecodeIntentsField`** / **`muxSubagentToolsUsesToolArgsDecode`** 对齐（coder/investigator 主路径 **`ToolArgsDecode`**，非 **`decodeIntentsField`**）。此前（**5790** 绿）：**`DecodedToolInvocation`** 强类型 **`CoderBatch`/`InvestigatorBatch`**（`decodedToolInvocationNoObj`）；**`Shell.MuxSubagentToolExecute`** + Mux 子代理对称解码（`muxSubagentToolsUsesToolArgsDecode`）；**`ToolCopy.subagentToolFailed`**；**`SessionIo.runSubagentCoreResult`** 内外层 **`translateJsError`→`Error`** 禁裸 **`Promise.reject`**；**`OpencodeAgentConfigCodec`** **`PermissionOverrides`/`ToolsOverrides` `Map`** + **`permissionMapToObj`/`toolsMapToObj`**；**`ToolArgsDecodeTests`**。此前批次：**`Kernel.ToolArgs`** + **`Shell.ToolArgsDecode`** + **`Shell.SubagentToolExecute`**（`kernelToolArgsExists` / `toolArgsDecodeExists` / `opencodeSubagentToolsUsesToolArgsDecode`）；**`SessionIo.runSubagent`** / **`runSubagentWithCleanup`** → **`Promise<Result<string, DomainError>>`**（`sessionIoRunSubagentReturnsResult`）；**`OpencodeAgentConfigCodec.UserAgentScalars.Mcps`** → **`string array option`**；**`CommandHooks`** 审查文案 **`ToolCopy`**（`commandHooksUsesToolCopyReviewMessages`）；**`ToolCopy.subagentIntentsMustBeNonEmpty`**（Mux **`bindParallel`** 空 intents）。此前批次（**5705** 绿）：**`Shell/OpencodeAgentConfigWire`**（**`agentConfigUsesOpencodeAgentConfigWire`**）；**`Shell/MessageTransformHostEntry`**（**`messageTransformUsesHostEntry`**）；**`SessionIo.startSubagentSession`** → **`Promise<Result<string,DomainError>>`** + **`runSubagentCoreResult`**；**`RuntimeScope.capsInflight`** / **`GetOrLoadCapsInflight`**（**`capsFileCacheUsesInflight`**）；**`ToolExecutionContext`** 移除 **`AbortSignal`**，**`toolRuntimeContextAbortFromShellCodec`**。此前批次：**Phase B** **`Shell/MessagingPartCodec`** 收拢双宿主消息 part 解码（`decodeTextPart`、`decodePartsFromArray`、`operationActionFromInput`、`decodeOpencodeToolStateBox`、`toolOutputAndErrorFromHostOutput`、`muxPartStateToKernelStatus`、`decodeMuxDynamicToolState`）；**`Opencode/MessagingCodec`** / **`Mux/MessagingCodec`** 变薄并消费 Shell SSOT；**`ArchitectureTests`** 新增 `messagingPartCodecExists`、`opencodeMessagingCodecUsesMessagingPartCodec`、`muxMessagingCodecUsesMessagingPartCodec`（Mux 禁本地 `decodeToolStatus`）；**`MessagingPartCodecTests`** 覆盖 mux 状态映射与 tool output 分支；**Phase B** **`Shell/MuxAiSettingsCodec`** + Mux **`AiSettings`** 接线；**`ArchitectureTests.muxAiSettingsUsesMuxAiSettingsCodec`**；**`MuxAiSettingsCodecTests`** / **`AgentConfigApplyTests`** 登记进 **`tests/Tests.fs`**。**Mux AI settings 审查修复**：**`decodeMuxDelegateConfigLenient`**（缺 `workspaceId` 可解析）、**`coerceThinkingLevel`** SSOT 于 **`MuxAiSettingsCodec`**；**`AiSettings.resolve`** 禁 `Dyn.get config runtime/cwd`；**`MuxAiSettingsIntegrationTests`** + 架构探针扩展。**全量测试 **5705** 绿**（含 **`MuxAiSettingsIntegrationTests`**、lenient/coerce 行为单测；**`OpencodeContextCodecTests`** / **`OpencodeSessionPromptCodecTests`** / **`OpencodeAgentConfigCodecTests`** 等已登记）；**Phase B** **`Shell/OpencodeSessionPromptCodec`**（`tryDecodePromptModelFromPayload` / `tryDecodePromptModelFromModelString`）+ **`SessionIo.buildPromptBody`** 接线；**`ArchitectureTests.sessionIoUsesOpencodeSessionPromptCodec`**；**`SessionIoPromptBodyTests`**；**`Shell/OpencodeSessionSpawnCodec`** + **`SessionIo.startSubagentSession`**；**`OpencodeSessionSpawnCodecTests`** + **`ArchitectureTests.sessionIoUsesOpencodeSessionSpawnCodec`**。**Phase E** 表行与实现一致：删除对已移除 **`resetDefaultForTesting`** 的「清空迭代器与会话队列」表述；**`runtimeScopeNoGetDefault`** 门禁禁止 **`getDefault`/`resetDefaultForTesting`**。**Phase C** **`Shell/MessageTransformPipeline`**（`runMessageTransformPipeline` + `MessageTransformPlan`），双宿主 **`messagesTransform`** 经 pipeline 注入 dedup/caps（**`messageTransformUsesPipeline`**）。**Phase B** **`Shell/OpencodeClientCodec`**（`getClientFromPluginCtx` / `getSessionApiFromClient`）：Opencode 侧 **SearchTools**、**ExecutorTool**、**ReviewTools**、**CommandHooks**、**PluginCore**、**SessionLifecycleObserver**、**NudgeEffect**、**ReviewerLoop**、**KnowledgeGraphSessionMessages** 等改引 codec，消除内联 **`Dyn.get ctx "client"`** / **`Dyn.get client "session"`**；**`opencodeNoDirectClientSessionDyn`** 全目录扫描门禁（**`OpencodeClientCodec.fs`** 豁免）；**`opencodeSubagentToolsUsesOpencodeClientCodec`**、**`sessionIoUsesOpencodeClientCodec`** 登记 **`tests/Tests.fs`**。**Phase B/D/G** 新增 **`FileToolsCodec`** / **`WorkBacklogToolsCodec`** / **`PatchToolsCodec`** / **`DelegateToolsCodec`**，Mux read/write、Wrappers todo、Opencode patch hook、Mux Delegate 接线；**Phase B/C** **`HostMessagePartCodec`** 收拢 `msg.parts` 文本提取；**`ExecutorToolsCodec.peekExecutorMode`** 供 Mux 只读 executor hook；**`ArchitectureTests`** 新增 `muxHostToolsReadWriteUsesFileToolsCodec`、`muxWrappersTodoUsesWorkBacklogToolsCodec`、`opencodeHookExecuteUsesPatchToolsCodec`、`muxDelegateUsesDelegateToolsCodec`、`muxHookInputCodecExecutorReadOnlyUsesCodec`、`messageTransformCommonUsesHostMessagePartCodec`、`readDedupMuxPluginUsesHostMessagePartCodec` 等门禁；**`FileToolsCodecTests`** / **`WorkBacklogToolsCodecTests`** 注册进 `tests/Tests.fs`。**审查驳回修复**：Mux **`HostTools` read** 经 **`FileToolsCodec.readArgsForHost`** 向宿主只暴露已解码 `path`/`offset`/`limit`；**`HookExecute`** Mimocode `apply_patch` 参数解码失败时 **`setKey output "error"`**（`formatDomainError`）；**`WorkBacklogToolsCodec.decodeTodoToolOpts`** 强制 **`toolCallId`** 非空；**`Shell/DynField`** 为 `strField`/`optInt`/`hasField` SSOT，供各 codec 共用；**`PatchToolsCodecTests`** / **`HostMessagePartCodecTests`** / **`MessagingPartCodecTests`** 注册进 `tests/Tests.fs`；**Phase B** **`Opencode/SessionIo`** `extractToolContext` 改经 **`decodeOpencodeToolContext`**（`sessionIoUsesToolContextCodec`）。**Phase B** 新增 **`Shell/OpencodeContextCodec`**（`getAbortSignalFromContext`）、**`Shell/OpencodeAgentConfigCodec`**（`decodeUserAgentScalars`）；**`Opencode/SessionIo`** `getAbortSignal` 委托 **`getAbortSignalFromContext`**（`sessionIoUsesOpencodeContextCodec`）；**`Opencode/AgentConfig`** 经 **`decodeUserAgentScalars`**（`agentConfigUsesOpencodeAgentConfigCodec`）；**`ArchitectureTests`** 登记上述门禁于 `tests/Tests.fs`。**Mux `Wrappers` todo**：`decodeTodoWriteArgs` / `decodeTodoToolOpts` 解码失败时向宿主返回 **`formatDomainError`**（不再静默落空 todos）；**`IntegrationEventTests.todoWriteWrapperDecodeFailureSpec`** 集成断言缺失 `completedWorkReport` / `toolCallId` 时 `success=false` 且输出含 `invalid`/`todowrite`。全量测试 **5220** 绿（全量 **5220** 绿已 `npm run build-and-test` 验证）；`tests/ArchitectureTests.fs` 约 **107** 项架构探针（与 `Tests.fs` 中 `ArchitectureTests.*` 登记一致）。

**已知技术债（诚实标注）**：**`mergeConfigObj`** 仍用于合并用户 agent obj 与编码后的标量记录；宿主 **`message.tools`** / 配置树在 wire 侧仍为 obj。双宿主 messaging wire 分叉见 **`MESSAGING_WIRE.md`**（有意保留，非债务）。Epic 开放项见 **`REFACTOR_ACCEPTANCE.md`**。

**全量口径**：**`npm run build-and-test`**（**6233 passed** / **380 tests** 绿；门禁含 **`messageTransformUsesCapsKgHostHooks`**、**`shellCodecFilesNoLocalStrField`**（Web/Review/KG/Subagent codec 纳入）、**`subagentToolsUseToolCatalogRequiredKeys`**、**`opencodeHookSchemaUsesIntentsRawFromArgs`**、**`messagingWireForkDocumented`**、**`hostObjBoundaryDocumented`**、**`toolArgsDecodeCoversMajorTools`**、**`chatHooksUsesChatHookOutputCodec`**、**`toolExecuteWireHelperExists`**、**`opencodeToolsUseWireEncodeForClient`**、**`opencodeKgUsesKnowledgeGraphToolsCodec`**、**`muxHostToolsWireDecodeFailures`**、**`muxWebToolsUsesWireDecodeFailure`** 等）。

**本轮切片**：**6233** 批次（**Opencode KG tools wire SSOT**：**`KnowledgeGraphTools`** + **`opencodeKgUsesKnowledgeGraphToolsCodec`**）；绿测计数见「全量口径」。

## 构建与测试

环境：

- .NET SDK（项目目标 `net10.0`）
- `dotnet tool restore` 安装 Fable
- Node.js + `npm`

常用命令：

```bash
npm run build-and-test
```

说明：

- `npm run build-and-test` 完整管线：`dotnet fable vibe-fs.fsproj --outDir build`（单工程编译，含 Kernel+Shell+宿主适配+测试）→ 清理 `build/fable_modules/.gitignore` + 拷贝 `build-package.json` 为 `build/package.json`（打包准备）→ `node tests/runner.js`（全部测试）。不设独立编译/watch/子测试集命令。
- npm 包主导出入口仍是 `build/src/Mux/Plugin.js`
- 测试入口由 `tests/runner.js` 加载 `build/tests/Tests.js`
- 测试集覆盖 Kernel、Shell、Review、KnowledgeGraph、Fuzzy、Methodology、Delegate，以及多组集成契约（见 `tests/Tests.fs`）
- 中间产物：所有 MSBuild `bin/`、`obj/` 统一落到根目录 `artifacts/`；清理直接 `rm -rf build artifacts`

## 源码入口速览

如果你是从功能入口反推实现，当前最重要的文件是：

1. `src/Mux/Plugin.fs`：Mux 注册、wrapper、event hook、slash command、knowledge graph 接线
2. `src/Opencode/PluginCore.fs`：OpenCode / Mimocode 共用插件装配
3. `src/Opencode/Tools.fs`：OpenCode 工具总表
4. `src/Mux/HostTools.fs`：Mux 内建工具总表
5. `src/Opencode/KnowledgeGraphRuntime.fs` + `src/Mux/KnowledgeGraphTools.fs`：两宿主的 knowledge graph runtime 接面
6. `src/Opencode/MessageTransform.fs` + `src/Mux/MessageTransform.fs`：消息前处理、caps、knowledge graph prelude、review replay

## 目录速览

```text
src/
  Kernel/    纯领域规则、状态机、格式协议、共享提示词
  Shell/     Node/文件系统/网络/第三方库/串行队列
  Opencode/  OpenCode / Mimocode 插件适配层与 TUI 扩展
  Mux/       Mux 注册与 wrapper 适配层
tests/       纯内核 + Shell + 集成 + 插件契约测试
build/       Fable 编译后的 JS 产物
```

## 一句话总结

这个项目的核心不是“F# 写插件”，而是：

> 先把多代理系统里真正稳定的语义抽成纯内核，再把宿主、IO、消息对象、schema、并发与持久化都压成外围适配问题。

只要抓住这条主线，当前大多数实现细节都不是偶然选择，而是被这组约束一步步推出来的。