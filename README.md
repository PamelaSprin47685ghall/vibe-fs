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
- review loop、todo folding、wiki/bookkeeper、nudge
- OpenCode、Mimocode、Mux 三条宿主接线

但真正困难的不是“把工具挂出来”，而是同时满足下面这些约束：

1. 同一套领域规则要在两个宿主里复用，而两个宿主的工具协议、schema、wrapper、事件模型都不相同。
2. LLM 接口天然弱类型，消息/工具参数/宿主对象结构都可能漂移。
3. 会话会很长，历史需要折叠；但折叠后又不能丢掉真正重要的工作状态。
4. review、wiki、nudge 这类能力都跨多轮对话，且必须在重启后尽量恢复。
5. 插件运行在 JS/Node 宿主中，外部世界全是副作用；但业务规则又必须保持可测试、可推理、可重放。

如果不先解决这些根问题，后面的“功能实现”都会退化成一堆脆弱 hook 和 if/else。

## 第一性原理

### 1. 真正稳定的资产是领域规则，不是宿主 API

宿主会变，工具 schema 会变，消息对象长相会变；真正值得保留的是：

- review 是一个有限状态机
- nudge 是一个有限状态机
- wiki 是 append / daily rewrite 两种作业
- todo folding 的目标是压缩上下文但保留 durable progress
- tool permission 是一张角色到能力的规则矩阵

所以项目必须把“稳定规则”抽到独立的纯内核里，而把“如何接宿主”放到外围适配层。

这直接推出目录分层：

- `src/Kernel/`：纯领域模型、状态机、提示词拼装、格式化、权限判定
- `src/Shell/`：Node/文件系统/网络/子进程/第三方库边界
- `src/Opencode/`：OpenCode 宿主适配
- `src/Mux/`：Mux 宿主适配

### 2. 历史记录才是事实，内存状态只是投影

review 是否激活、todo backlog 如何折叠、wiki job 是否存在、editplus tag 是否还能解释，这些都不是“当前进程内存”能可靠承诺的。

进程会重启，hook 会打断，background session 会延后返回。既然宿主的消息历史天然存在，那它就应当被视为事实来源；进程内 store 只能是可重建投影。

这条原则直接推出多个实现细节：

- `src/Kernel/LoopMessages.fs` 用 YAML front-matter 写结构化锚点，而不是依赖自然语言文案
- `src/Opencode/MessageTransform.fs` 会从历史重建 review 状态、todo backlog、editplus 状态
- `src/Opencode/WikiRuntimeIO.fs` 提供 `tryResolveJobContext`，从消息历史恢复 wiki job marker
- `src/Kernel/MagicTodo.fs` 强制要求 `completedWorkReport`，因为旧对话可能被折叠，只能靠 durable report 保存关键信息

### 3. 副作用不可避免，但必须被压到边界

文件系统、网络请求、子进程执行、宿主 session 创建、MCP、tree-sitter、fff-node，这些都不是领域规则，它们只是现实世界接口。

所以项目把它们压到：

- `src/Shell/`：Node 侧 IO 能力
- `src/Opencode/*Codec.fs`、`src/Mux/Wrappers.fs`、`src/Opencode/ToolSchema.fs`：对象编解码与宿主胶水

与此同时，真正可推理的部分尽量写成纯函数：

- `src/Kernel/ReviewSession.fs`
- `src/Kernel/Nudge.fs`
- `src/Kernel/Wiki.fs`
- `src/Kernel/WikiMaintenance.fs`
- `src/Kernel/Fuzzy.fs`
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

- `src/Kernel/MagicCore.fs`
- `src/Kernel/MagicProjection.fs`
- `src/Kernel/MagicTodo.fs`
- `src/Opencode/MagicTodo.fs`

### 6. 并发问题的本质是共享可变状态

本项目运行在 JS/Node 宿主里，没有线程级并发，但会有大量异步交错：

- tool hooks 同时触发
- background reviewer / bookkeeper 异步返回
- wiki 写入需要跨进程序列化
- per-session executor 必须按顺序执行

所以策略不是“到处加锁”，而是：

- 单进程内：用 `Promise` 串行队列约束关键路径
- 跨进程：用显式 port lock 保护 wiki 写入
- 按 session/workspace 切分串行域

这直接推出：

- `src/Shell/PromiseQueue.fs`
- `src/Shell/ChildAgentRegistry.fs` 里的 per-session executor actor
- `src/Shell/WikiPortLock.fs`
- `src/Opencode/WikiRuntime.fs` 里的 `commandQueue` / `backgroundJobs` / `writeQueues`

## 从原则演绎到当前架构

### Kernel：纯规则层

`src/Kernel/` 不是“公共工具箱”，而是“真正稳定的系统语义”。

它包含的内容大致分五类：

1. 领域状态机
   - `ReviewSession.fs`
   - `Nudge.fs`
   - `Wiki.fs`
   - `WikiMaintenance.fs`

2. 跨宿主共享格式与协议
   - `PromptFrontMatter.fs`
   - `LoopMessages.fs`
   - `Prompts.fs`
   - `CapsFormat.fs`

3. 工具/权限/意图元数据
   - `ToolCatalog.fs`
   - `Config.fs`
   - `SubagentIntents.fs`
   - `HostTools.fs`

4. 历史折叠与消息语义
   - `Messaging.fs`
   - `MessageDedup.fs`
   - `MagicCore.fs`
   - `MagicProjection.fs`
   - `MagicTodo.fs`

5. 纯算法或纯解析
   - `Fuzzy.fs`
   - `Executor.fs`
   - `TreeSitterKernel.fs`
   - `Domain.fs`
   - `Dyn.fs`

核心判断标准是：如果一个模块去掉 Node/宿主对象后仍然成立，它就应该进 Kernel。

### Shell：现实世界边界

`src/Shell/` 负责把 Kernel 需要的能力从 Node 世界里取回来：

- 文件系统：`FileSys.fs`、`WorkspaceFiles.fs`、`WikiFiles.fs`
- 搜索后端：`FuzzyFinderShell.fs`、`FuzzySearch.fs`
- 执行器：`Executor.fs`、`ExecutorJavascript.fs`
- tree-sitter：`TreeSitterShell.fs`
- 远端 API：`OllamaClient.fs`
- 运行时 store/queue：`PromiseQueue.fs`、`CallStore.fs`、`MagicSessionStore.fs`
- 跨进程锁：`WikiPortLock.fs`

这里的原则不是“把所有 IO 扔一起”，而是让每个 Shell 模块只承担一种外部能力，再把复杂流程留给上层编排。

### Opencode / Mux：两套适配面，四个入口

因为两个宿主家族对插件的暴露方式不同，所以项目没有强行用一层巨型通用抽象吞掉差异，而是保留两个适配面：

- `src/Opencode/`：直接面向 OpenCode / Mimocode 插件 hook、tool schema、消息变换
- `src/Mux/`：面向 Mux 的注册表、wrapper、slash command、delegate API

当前公开入口实际有四个：

- `src/Opencode/Plugin.fs`：OpenCode 插件入口
- `src/Opencode/PluginMimo.fs`：Mimocode 插件入口
- `src/Opencode/PluginMimoTui.fs`：Mimocode TUI 辅助入口（`/subagents`）
- `src/Mux/Plugin.fs`：Mux 注册表入口

二者共享同一个 Kernel，但各自保留宿主差异：

- schema 生成不同
- tool execute 签名不同
- 消息对象形态不同
- subagent 启动路径不同
- task/todowrite 命名与行为不同

这就是为什么项目同时存在：

- `src/Opencode/SubagentTools.fs` 与 `src/Mux/SubagentTools.fs`
- `src/Opencode/ReviewTools.fs` 与 `src/Mux/SubagentTools.fs` 中的 submit-review 逻辑
- `src/Opencode/ToolSchema.fs` 与 `src/Mux/Wrappers.fs`

不是重复劳动，而是“共享语义，不强行共享协议”。

### wiki 不是全局常开，而是目录存在性门控

当前实现里，wiki 能力不是编译期开关，而是 workspace 内存在 `wiki/` 目录时才接线：

- `src/Opencode/PluginCore.fs` 通过 `wikiDirExists` 决定是否注册 `fetch_wiki` / `return_bookkeeper`
- `src/Mux/Plugin.fs` 也用同一条件决定工具目录与 bookkeeper runtime 接线
- `src/Opencode/WikiRuntime.fs`、`src/Mux/WikiTools.fs` 会在运行时再次守卫缺失 `wiki/` 的场景

这意味着 wiki 是 opt-in 的工作区能力，而不是所有会话默认暴露的工具。

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

最终都被统一约束到 Magic Todo 的 backlog 语义上。

### 3. wiki 为什么拆成 `Wiki` / `WikiRuntimeState` / `WikiRuntime` / `WikiRuntimeIO`

因为这里同时存在四种性质完全不同的东西：

1. wiki 数据模型与 NDJSON 协议
2. 纯维护策略：何时 daily rewrite
3. 进程内 runtime 状态
4. 真实 IO：读写文件、拉起 bookkeeper、拿 port lock

如果把它们揉在一起，任何一点修改都会牵一整团。现在的拆法正好对应这四个层次：

- `src/Kernel/Wiki.fs`
- `src/Kernel/WikiMaintenance.fs`
- `src/Kernel/WikiRuntimeState.fs`
- `src/Opencode/WikiRuntime.fs`
- `src/Opencode/WikiRuntimeIO.fs`

### 4. fuzzy search 为什么要“纯格式化/状态”和“Shell finder”分离

因为搜索真正不稳定的是后端 finder；而 query 组装、路径归一化、输出格式、iterator 语义是稳定规则。

所以：

- `src/Kernel/Fuzzy.fs` 负责 query/format/state shape
- `src/Shell/FuzzyFinderShell.fs` 负责第三方 finder 生命周期
- `src/Shell/FuzzySearch.fs` 负责把二者接起来

这也是 typed iterator store 存在的原因：iterator 不是任意字符串，而是“find state / grep state”这两种有限状态之一。

### 5. nudge 为什么要分 kernel decision 和 runtime flow

因为“该不该 nudge”是纯判断，而“如何发 nudge”涉及宿主 API、错误、并发、abort。

因此：

- `src/Kernel/Nudge.fs`：纯决策与 dedup 语义
- `src/Kernel/NudgeState.fs`：事件到状态转移
- `src/Shell/NudgeRuntime.fs`：Mux 侧 runtime
- `src/Opencode/NudgeHook.fs`：OpenCode 侧 hook

尤其 `NudgeHook` 里把纯状态更新和异步 `session.prompt` 分离，是为了避免单一串行队列被 await 卡死。

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
3. `src/Kernel/Wiki.fs`
4. `src/Kernel/MagicCore.fs` + `src/Kernel/MagicProjection.fs`
5. `src/Kernel/Prompts.fs` + `src/Kernel/LoopMessages.fs`
6. `src/Shell/PromiseQueue.fs`
7. `src/Opencode/MessageTransform.fs`
8. `src/Opencode/WikiRuntime.fs`
9. `src/Opencode/PluginCore.fs`
10. `src/Mux/Plugin.fs`
11. `src/Opencode/Plugin.fs` (opencode entry)
12. `src/Opencode/PluginMimo.fs` (mimocode entry)

这个顺序是按“先看不变的规则，再看副作用，再看宿主拼装”排列的。

## 构建与测试

环境：

- .NET SDK（项目目标 `net10.0`）
- `dotnet tool restore` 安装 Fable
- Node.js + `pnpm`

常用命令：

```bash
dotnet tool restore
pnpm install
pnpm build
pnpm test
```

说明：

- `pnpm build` 会执行 `dotnet fable vibe-fs.fsproj --outDir build`，然后把 `build-package.json` 复制成 `build/package.json`
- 构建产物位于 `build/`
- npm 包主导出入口当前是 `build/src/Mux/Plugin.js`
- 测试由同一个 `vibe-fs.fsproj` 编译，再由 `node tests/runner.js` 执行
- 测试集覆盖 Kernel、Shell、Review、Wiki，以及多组集成契约（见 `tests/Tests.fs`）

## 源码入口速览

如果你是从功能入口反推实现，当前最重要的文件是：

1. `src/Mux/Plugin.fs`：Mux 注册、wrapper、event hook、slash command、wiki 接线
2. `src/Opencode/PluginCore.fs`：OpenCode / Mimocode 共用插件装配
3. `src/Opencode/Tools.fs`：OpenCode 工具总表
4. `src/Mux/HostTools.fs`：Mux 内建工具总表
5. `src/Opencode/WikiRuntime.fs` + `src/Mux/WikiTools.fs`：两宿主的 wiki runtime 接面
6. `src/Opencode/MessageTransform.fs` + `src/Mux/MessageTransform.fs`：消息前处理、caps、wiki prelude、review replay

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
