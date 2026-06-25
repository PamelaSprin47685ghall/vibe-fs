# vibe-fs

`vibe-fs` = F# 经 Fable 编译为 JS 的多代理插件运行时。

把同一套多代理能力落到两套宿主适配面：

- OpenCode / Mimocode 风格插件：`src/Opencode/`
- Mux 风格注册表：`src/Mux/`

## 项目要解决的真实问题

表面提供：子代理委派、文件读写、模糊搜索、执行器、审查循环、knowledge graph、nudge、methodology probe、OpenCode/Mimocode/Mux 三宿主接线。

真正困难的不是"把工具挂出来"，而是同时满足：

1. 同一套领域规则要在两套宿主里复用，而两套宿主的工具协议、schema、wrapper、事件模型都不同。
2. LLM 接口天然弱类型，消息/工具参数/宿主对象结构都可能漂移。
3. 会话长，历史需要折叠；但折叠后不能丢真正重要的工作状态。
4. review、knowledge graph、nudge 跨多轮且必须在重启后尽量恢复。
5. 插件运行在 JS/Node 中，外部世界全是副作用；但业务规则必须可测试、可推理、可重放。

不先解决根问题，后面的功能实现都会退化成脆弱 hook 和 if/else。

## 第一性原理

### 1. 稳定资产是领域规则，不是宿主 API

宿主会变，schema 会变，消息对象会变；真正稳定的是：

- review = 有限状态机
- nudge = 有限状态机
- knowledge graph = append + daily rewrite
- todo folding = 压缩上下文保留 durable progress
- tool permission = 角色到能力的规则矩阵

把稳定规则抽到独立纯内核，宿主适配放到外围。

### 2. 历史才是事实，内存状态只是投影

review/todo/knowledge graph 状态都不是"当前进程内存"能可靠承诺的。进程会重启、hook 会打断、background session 延后返回。宿主的消息历史天然存在，应被视为事实来源；进程内 store 只能可重建投影。

### 3. 副作用不可避免，但必须被压到边界

文件系统、网络、子进程、宿主 session、MCP、tree-sitter、第三方库是现实接口不是领域规则。压到 `src/Shell/` 与各 codec；Kernel 写成纯函数。

### 4. LLM 边界必须尽快强类型化

LLM 参数与宿主消息是动态对象。如果动态对象直接在业务核心里流动，错误会在深处爆炸。策略：

- 宿主边界先读 `obj`
- 尽快转成 Kernel 里的 DU/record
- 纯逻辑只消费强类型
- 最后编码回宿主对象

### 5. 长会话必然折叠，进度必须可压缩

不"保留全部"也不"只保留最新"：旧工作压缩成 backlog projection，结构化 `completedWorkReport` 保存 durable knowledge，最近一段原始上下文保留给当前回合。

### 6. 并发的本质是共享可变状态

JS/Node 无线程级并发，但有大量异步交错。策略不是到处加锁：

- 单进程：Promise 串行队列约束关键路径
- 跨进程：显式 port lock 保护 knowledge graph 写入
- 按 session/workspace 切分串行域

## 架构

### Kernel：纯规则层

不是"公共工具箱"，是"真正稳定的系统语义"。五类：

1. 领域状态机：`ReviewSession`/`ReviewVerdict`/`Nudge`/`NudgeState`/`KnowledgeGraph`/`KnowledgeGraphMaintenance`
2. 跨宿主共享格式与协议：`PromptFrontMatter`/`PromptFragments`/`LoopMessages`/`ReviewPrompts`/`SearchPrompts`/`SubagentPrompts`/`KnowledgeGraphPrompts`/`CapsFormat`
3. 工具/权限/意图元数据：`ToolCatalog`/`Config`/`Subagent`/`SubagentIntents`/`HostTools`/`ToolPermission`/`ToolCopy`/`ToolResult`/`ToolArgs`/`ToolContext`
4. 历史折叠与消息语义：`Messaging`/`Message`/`MessageDedup`/`Dedup`/`BacklogProjectionCore`/`BacklogProjection`/`WorkBacklog`
5. 纯算法或纯解析：`FuzzyQuery`/`FuzzyPath`/`FuzzyFormat`/`Executor`/`TreeSitterKernel`/`Domain`/`Methodology`

判断标准：去掉 Node/宿主对象后仍成立就进 Kernel。

### Shell：现实世界边界

把 Kernel 需要的能力从 Node 取回：

- 文件系统：`FileSys`/`WorkspaceFiles`/`KnowledgeGraphFiles`
- 搜索后端：`FuzzyFinderShell`/`FuzzySearch`/`FuzzyIteratorStore`
- 执行器：`Executor`/`ExecutorJavascript`/`SessionExecutor`
- tree-sitter：`TreeSitterShell`
- 远端 API：`WebSearchApi`
- 动态类型与错误分类：`Dyn`/`ErrorClassify`
- 编解码边界：`SubagentIntentsCodec`/`WebSearchCodec`/`BacklogSessionCodec`/`WorkBacklogSchema`/`WebToolsCodec`/`ExecutorToolsCodec`/`ReviewToolsCodec`/`KnowledgeGraphToolsCodec`/`FileToolsCodec`/`WorkBacklogToolsCodec`/`PatchToolsCodec`/`DelegateToolsCodec`/`SubagentSimpleArgsCodec`/`ToolContextCodec`/`ToolExecute`/`ToolArgsDecode`/`ToolRuntimeContext`/`OpencodeHookInputCodec`/`OpencodeSessionPromptCodec`/`OpencodeSessionSpawnCodec`/`OpencodeAgentConfigCodec`/`OpencodeAgentConfigWire`/`OpencodeClientCodec`/`OpencodeSessionEventCodec`/`OpencodeContextCodec`/`MuxHookInputCodec`/`MuxWorkspaceCodec`/`MuxAiSettingsCodec`/`FuzzyToolsCodec`/`HostMessagePartCodec`/`MessagingPartCodec`/`MessagingEncodeHelpers`/`ChatHookOutputCodec`/`JsonSchemaBuilders`/`MuxJsonSchema`/`SubagentPromptBuild`/`SubagentSpawn`/`MessageTransformCore`/`MessageTransformPipeline`/`MessageTransformHostEntry`/`MessageTransformHostHooks`/`MessageTransformCommon`/`ReadDedupOpenCode`/`ReadDedupMuxPlugin`/`RuntimeScope`/`PromiseQueue`/`ChildAgentRegistry`/`SessionProjectionStore`/`CapsFileCache`/`CapsSynthCommon`/`ReviewRuntime`/`NudgeRuntime`/`ReviewReplaySync`/`KnowledgeGraphStorage`/`KnowledgeGraphWorkflow`/`KnowledgeGraphBookkeeperLaunch`/`KnowledgeGraphMaintenanceRun`/`KnowledgeGraphPortLock`
- review/nudge runtime

每个 Shell 模块只承担一种外部能力，复杂流程留给上层编排。

### Opencode / Mux：两套适配面，四个入口

不强行用巨型通用抽象吞差异，保留两个适配面：

- `src/Opencode/`：OpenCode / Mimocode 插件 hook、tool schema、消息变换
- `src/Mux/`：Mux 注册表、wrapper、slash command、delegate API

公开入口四个：

- `src/Opencode/Plugin.fs`：OpenCode 插件入口
- `src/Opencode/PluginMimo.fs`：Mimocode 插件入口
- `src/Opencode/PluginMimoTui.fs`：Mimocode TUI 辅助（`/subagents` + `task`→sidebar todo 回填）
- `src/Mux/Plugin.fs`：Mux 注册表入口

二者共享同一个 Kernel，各自保留宿主差异：schema 生成、tool execute 签名、消息对象形态、subagent 启动路径、task/todowrite 命名与行为。

### knowledge graph 不是全局常开，是目录门控

workspace 内存在 `kg/` 目录时才接线，运行时与 plugin 入口都二次守卫缺失场景。knowledge graph = opt-in 工作区能力，非所有会话默认暴露。

## 关键实现选择

### review loop = DU + front-matter + replay

review 仅少数合法状态 → DU 状态机。review 跨重启 → YAML front-matter 锚点（不依赖自然语言）。历史才是事实 → store 为空时从历史重建。

### todo 必须有 `completedWorkReport`

上下文折叠后旧回合原始消息不一定在；"完成了什么、改了哪些文件、踩了哪些坑"必须写成 durable report。OpenCode `todowrite` 与 Mimocode `task` 统一收敛到 `WorkBacklog` 的 backlog 语义。

### knowledge graph 四层拆

数据模型与 NDJSON 协议 / 纯维护策略 / 进程内 runtime 状态 / 真实 IO 四种性质不同，揉在一起任何修改都牵一整团。对应 `KnowledgeGraph` + `KnowledgeGraphMaintenance` + `KnowledgeGraphRuntimeState` + `KnowledgeGraphRuntime`（+ `KnowledgeGraphStorage` / `KnowledgeGraphWorkflow` / `KnowledgeGraphBookkeeperLaunch` / `KnowledgeGraphMaintenanceRun`）。

### fuzzy search 纯/壳分离

query 拼装、路径归一化、输出格式、iterator 语义是稳定规则 → Kernel；后端 finder 生命周期 → Shell。iterator 是有限状态（find state / grep state）而非任意字符串，挂在 `RuntimeScope.IteratorStore`。

### nudge kernel decision 与 runtime flow 分

"该不该 nudge"是纯判断；"如何发 nudge"涉及宿主 API、错误、并发、abort。纯状态更新与异步 `session.prompt` 分离，避免单一串行队列被 await 卡死。

### 提示词 YAML front-matter

顶部结构化字段程序精确识别，下方 prose 模型友好。重启后 replay 只读 scalar 字段，不必全文 NLP。

## 读代码推荐路径

1. `src/Kernel/ReviewSession.fs`
2. `src/Kernel/Nudge.fs`
3. `src/Kernel/KnowledgeGraph.fs`
4. `src/Kernel/BacklogProjectionCore.fs` + `BacklogProjection.fs` + `WorkBacklog.fs`
5. `src/Kernel/PromptFragments.fs` + `LoopMessages.fs`
6. `src/Shell/PromiseQueue.fs`
7. `src/Opencode/MessageTransform.fs`
8. `src/Opencode/KnowledgeGraphRuntime.fs`
9. `src/Opencode/PluginCore.fs`
10. `src/Mux/Plugin.fs`
11. `src/Opencode/Plugin.fs` (OpenCode 入口)
12. `src/Opencode/PluginMimo.fs` (Mimocode 入口)

按"先看不变规则，再看副作用，再看宿主拼装"。

## 架构边界纪律

架构测试在 `tests/` 下以 `ArchitectureTests*` 系列 fs 文件覆盖：

- `Kernel/` 不直接 `Dyn.*`，所有宿主 `obj` 必经 Shell codec
- 每个工具族必在 `Kernel.ToolCatalog` + 对应 Shell codec 维护 SSOT
- 双宿主执行链路（Opencode / Mux）经 `Shell.ToolArgsDecode` + `Shell.ToolExecute` + `Shell.SubagentToolExecute` / `Shell.MuxSubagentToolExecute`
- 可变状态只能在 `Shell.RuntimeScope` 派生的实例内，不允许模块级 `getDefault`
- KnowledgeGraph 维护调度经 `Shell.KnowledgeGraphMaintenanceRun.runMaintenanceIfDue`，双宿主禁本地 `launchIfDue`
- Hook output 写路径经 `OpencodeHookInputCodec` / `MuxHookInputCodec`，禁裸 `setKey output args|error|parts` / `Dyn.get output args`
- Tool `description` / 必填键 / 拒绝语 / `mergeConfigObj` / 子代理 schema shape 全部归 `Kernel.ToolCatalog` + `Kernel.ToolCopy`
- 权限判定经 `ToolPermission.classifyTool → ToolSemantic` + `canUseForHost`，以 Set 精确匹配为主

## 构建与测试

环境：.NET SDK（项目目标 `net10.0`）、`dotnet tool restore` 安装 Fable、Node.js + npm。

```bash
npm run build-and-test
```

完整管线：

1. `dotnet fable vibe-fs.fsproj --outDir build`：单工程编译 Kernel+Shell+宿主适配+测试
2. 清理 `build/fable_modules/.gitignore` + 拷贝 `build-package.json` 为 `build/package.json`
3. `node tests/runner.js`：全部测试

无独立编译/watch/子测试集命令。

npm 包主导出入口：`build/src/Mux/Plugin.js`。测试入口：`tests/runner.js` 加载 `build/tests/Tests.js`。中间产物（MSBuild `bin/`/`obj/`）统一落到根目录 `artifacts/`；清理直接 `rm -rf build artifacts`。

## 源码入口速览

从功能入口反推实现：

1. `src/Mux/Plugin.fs`：Mux 注册、wrapper、event hook、slash command、knowledge graph 接线
2. `src/Opencode/PluginCore.fs`：OpenCode / Mimocode 共用插件装配
3. `src/Opencode/Tools.fs`：OpenCode 工具总表
4. `src/Mux/HostTools.fs`：Mux 内建工具总表
5. `src/Opencode/KnowledgeGraphRuntime.fs` + `src/Mux/KnowledgeGraphTools.fs`：两宿主 knowledge graph runtime 接面
6. `src/Opencode/MessageTransform.fs` + `src/Mux/MessageTransform.fs`：消息前处理、caps、knowledge graph prelude、review replay
7. `src/Shell/OpencodeSessionEventCodec.fs` + `src/Opencode/NudgeEventCodec.fs`（薄 alias）：Opencode session event payload 边界编解码

## 目录速览

```text
src/
  Kernel/    纯领域规则、状态机、格式协议、共享提示词
  Shell/     Node/文件系统/网络/第三方库/串行队列 + 全部宿主 obj 边界 codec
  Opencode/  OpenCode / Mimocode 插件适配层与 TUI 扩展
  Mux/       Mux 注册与 wrapper 适配层
tests/       纯内核 + Shell + 集成 + 插件契约 + 架构边界探针
build/       Fable 编译后的 JS 产物
```

## 一句话总结

这个项目的核心不是"F# 写插件"，而是：

> 先把多代理系统里真正稳定的语义抽成纯内核，再把宿主、IO、消息对象、schema、并发与持久化都压成外围适配问题。

抓住这条主线，当前大多数实现细节都不是偶然选择，而是被这组约束一步步推出来的。