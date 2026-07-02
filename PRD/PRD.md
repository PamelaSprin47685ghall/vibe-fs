# PRD: 万象术 — Multi-Agent Plugin Runtime (F# → JS)

> 本文档为基于 **当前仓库源码** 的保姆级实现规格。凡与 README / 用户口头描述冲突处，以 `src/`、`tests/`、`package.json` 为准；出处以路径标注。对标姊妹项目 `wanxiangzhen/PRD.md` 的章节密度与可验收粒度，描述对象是 **万象术**（`wanxiangshu`），不是万象阵。

## 0. 一句话定义

万象术是一个经 **Fable 编译为 JavaScript** 的多代理插件运行时：同一套 **Kernel（纯领域规则）+ Shell（副作用边界）** 落到 **四套宿主适配面**（OpenCode、Mimocode、Mux、oh-my-pi OMP），向主会话 LLM 暴露子代理委派、文件与模糊搜索、执行器、**With-Review Mode**（`/loop`）、可选 **knowledge graph**（`kg/` 门控）、nudge、**Fallback 模型降级**（完全平方数启发式 + 实际 continue 探测）、**WarnTdd**（修改类工具必须声明 TDD 纪律）、以及 **`Registry.allSchemas` 所列全部** `methodology_*` 结构化笔记本工具（当前源码 **54** 个 methodology id）；长会话通过 **backlog 投影 + YAML front-matter 锚点** 在重启后尽量恢复 review / todo / KG 语义。

---

## 1. 背景与动机

### 1.1 问题

单宿主、单会话的 coding agent 在真实工程里同时撞上五类硬约束：

1. **宿主协议漂移**：OpenCode 的 hook + Zod tool、Mux 的 wrapper + 注册表、OMP 的 Pi 扩展与 TypeBox schema，消息与工具参数的线序形状各不相同。
2. **弱类型边界**：LLM 与宿主传入的 `obj` 若直接进入业务核心，错误在深处以 Dyn 链爆炸。
3. **长会话与折叠**：context compaction / transform 切片不能丢 **可交接的 durable progress**（完成了什么、踩了哪些坑、当前 review 任务是什么）。
4. **跨轮状态**：review、knowledge graph、nudge、子代理作业必须在进程重启后 **可重建**。
5. **副作用无处不在**：文件、网络、子进程、MCP、tree-sitter 是现实接口，但规则层必须 **可单测、可重放、可推理**。

在宿主上堆 hook 与 if/else 不能同时满足以上约束；需要把稳定语义抽到内核，把 IO 压到边界。

### 1.2 解决方案（产品形态）

- **一套内核、四套接线**：`src/Kernel/` + `src/Shell/` + `src/Opencode/` | `src/Mux/` | `src/Omp/` + `src/Methodology/`。
- **历史优先于内存**：review 任务从对话 front-matter 折叠（`inferReviewTaskFromTexts`）；todo 进度从五份交接报告（`ahaMoments`/`changesAndReasons`/`gotchas`/`lessonsAndConventions`/`plan`）回放为 backlog；KG 从 `kg/*.ndjson` 事件流折叠。
- **命令可拒、事实宜追加**：工具参数校验失败 → 强类型 `Error` 分支；KG 与 backlog 倾向 append，日维护对历史天文件做 **受控 rewrite**（头行 `rewritten` 标记）。
- **权限与命名 SSOT**：`ToolCatalog` + `ToolCopy` + `ToolPermission` + `HostTools.normalizeToolName`。

### 1.3 与万象阵的关系

- **万象阵**（`wanxiangzhen`）：多 opencode 进程 + worktree + DAG + coordinator HTTP；可把 slave 初始 prompt 包成万象术识别的 **`task:` / `/loop`** 语义以触发 With-Review（见万象阵 PRD §1.3、§6.2）。
- **万象术**：单进程（每宿主）内的多代理、review、KG、方法论工具；**不** import 万象阵，**不** 感知 coordinator URL。
- 协同仅在 **prompt / slash-command / 工具名** 层；两插件可同时安装，互不引用。

---

## 2. 第一性原理（公理 → 推导）

本节钉死设计公理；后续章节细节应能回溯到这里。

### 2.1 稳定资产是领域规则，不是宿主 API

宿主会变；稳定的是：

| 稳定语义 | 内核落点（示例路径） |
|----------|----------------------|
| With-Review = 有限状态机 + front-matter | `src/Kernel/ReviewSession/*`, `src/Kernel/LoopMessages.fs` |
| Nudge = 纯决策 + 去重状态机 | `src/Kernel/Nudge/*`, `src/Shell/NudgeRuntime.fs` |
| Knowledge graph = append + 日维护 rewrite | `src/Kernel/KnowledgeGraph/*`, `src/Shell/KnowledgeGraphFiles.fs` |
| Todo / backlog = 全量替换 todos + 五份交接报告（ahaMoments 等） | `src/Kernel/WorkBacklog.fs`, `src/Kernel/BacklogProjectionCore.fs` |
| 工具权限 = 角色 × 语义分类 | `src/Kernel/ToolPermission.fs` |
| WarnTdd = 修改类工具 TDD 声明 + 副作用确认 | `src/Kernel/WarnTdd.fs` |
| Fallback = 完全平方数启发式模型降级 | `src/Kernel/FallbackKernel/*` |
| 方法论笔记本 = 每法一 schema → `methodology_<id>` | `src/Methodology/Registry.fs`, `src/Methodology/SchemaCommon.fs` |

**判定**：去掉 Node / 宿主 `obj` 后仍成立的规则 → Kernel；否则 → Shell 或宿主目录。

### 2.2 历史是事实，内存状态是投影

- Review：`inferReviewTaskFromTexts` 明确声明「历史是 SSOT，store 是可重建投影」（`LoopMessages.fs:53-57`）。
- Todo：`replayBacklogWith` 从消息流折叠五份交接报告（`BacklogEntry` record，每条含 `ahaMoments`/`changesAndReasons`/`gotchas`/`lessonsAndConventions`/`plan`）；`WorkBacklog.fs` L24-71。
- KG：NDJSON 日文件 + `projectLatestWins` 折叠；内存 `KnowledgeGraphRuntime` 状态不得替代磁盘事件流。
- OpenCode **存储**消息：compaction 不删存储（万象阵 PRD 核实口径与万象术重放路径一致）；重放走 `session.messages` 全量路径，而非仅 `experimental.chat.messages.transform` 切片。

### 2.3 副作用压到 Shell 边界

Kernel **禁止**直接 `Dyn.*`（架构测试 `ArchitectureTestsFoundation` 等强制）。宿主 `obj` 必经 Shell codec（`OpencodeHookInputCodec`、`MuxHookInputCodec`、`MessagingPartCodec`、`ToolArgsDecode`、`ToolExecute` 等）。

### 2.4 命令可拒，事件/事实宜不可抵赖

- **命令侧**：`squad_update` 式校验在万象术中对应 `ToolArgsDecode` / methodology args / `submit_review` 前置条件；失败返回可见错误，不裸抛未分类异常。
- **事实侧**：`return_bookkeeper` 追加 NDJSON；review 结果与 loop 激活写入带 front-matter 的消息文本，供重放。

### 2.5 并发的本质是共享可变状态

策略不是全局锁，而是 **按域串行**：

- `Shell.PromiseQueue.SerialQueue`：KG 端口锁内写、部分 runtime 命令队列。
- `ReviewStore`：原子 `state <-` 更新（`ReviewRuntime.fs`）。
- `ChildAgentRegistry` / session 作用域：子代理与 KG job 按 session 切分。
- Nudge：**决策在 Kernel**，**`session.prompt` 在 Runtime**；避免单队列被 await 卡死（README §关键实现选择）。

### 2.6 类型消灭不可能态

- `ReviewState`、`ReviewCommand`、`ReviewEvent` 为 `RequireQualifiedAccess` DU（`ReviewSession/Types.fs`）。
- `TaskStatus` 级状态机在万象术侧体现为 review / nudge / KG job 状态 DU + 架构测试守卫非法组合。
- 可预见失败（参数缺失、权限拒绝、KG 目录不存在）→ 工具返回文案或 `Error` 分支，供 LLM 匹配。

---

## 3. 核心概念定义

### 3.1 宿主（Host）

```fsharp
// src/Kernel/HostTools.fs:3
type Host = Opencode | Mimocode | Mux | Omp
```

| Host | 入口模块 | 导出 |
|------|----------|------|
| OpenCode | `src/Opencode/Plugin.fs` | `[<ExportDefault>] plugin` → `pluginFor opencode` |
| Mimocode | `src/Opencode/PluginMimo.fs` | `pluginFor mimocode` |
| Mimocode TUI 辅助 | `src/Opencode/PluginMimoTui.fs` | `/subagents` + `task`→sidebar todo 回填 |
| Mux | `src/Mux/Plugin.fs` + `PluginRegistration` | 包默认 `"."` → `build/src/Mux/Plugin.js` |
| OMP | `src/Omp/Plugin.fs` | `wanxiangshuExtension`；`"./omp"` → `build/src/Omp/Plugin.js` |

**纪律**：`src/Omp/` **禁止** `open` / 引用 `Wanxiangshu.Opencode`、`Wanxiangshu.Mux`、`engine/`（`ArchitectureTestsOmp.fs`、`ArchitectureTestsFoundation.fs`）。

### 3.2 工具命名归一化（权限与 SSOT 的枢纽）

**Todo 族**（`HostTools.fs:10-26`）：

| Host | 列表写入工具名 | Prompt/别名 | 子代理 `task` 工具名 |
|------|----------------|-------------|----------------------|
| Opencode | `todowrite` | `todo_write` | `task` |
| Mimocode | **`task`** | **`task`** | **`actor`** |
| Mux | `todowrite` | `todo_write` | `task` |
| Omp | `todowrite` | `todo_write` | `task` |

**Mux 额外归一化**（`normalizeToolNameForMux`）：`file_edit_*`→`edit`，`web_fetch`→`webfetch`，`todo_read`→`todo`，`ask_user_question`→`question`，等。

### 3.3 With-Review Mode（Loop）

**用户可见命令**（OpenCode / Mimocode 经 `CommandHooks.fs`）：

- `/loop <task>`：激活 review；空参数 → 取消（`loopCancelledMessage`）。
- `/loop-review <task>`：先跑 **pre-review** 子会话（`runReviewerSession`），再视结果激活或注入反馈。

**结构化锚点**（`LoopMessages.fs`）：

| 字段 | 含义 |
|------|------|
| `task` | Worker 激活 With-Review 的任务文本 |
| `original_task` | Reviewer 子会话携带父需求，**不**触发 worker 重激活 |
| `verdict` | `accepted` / `rejected` / `terminated` / `cancelled` |
| `command` | `with-review` / `with-review-precheck`（模板用） |

**结束 verdict**（`isEndVerdict`）：仅 `accepted`、`cancelled` 清除当前 task；`rejected` / `terminated` **保持** With-Review 活跃。

### 3.4 ReviewSession 状态机

**状态**（`ReviewSession/Types.fs:4-9`）：

```
Inactive | Active(task) | Locked(task, reviewerId) | Accepted | Rejected(feedback)
```

**命令 → 事件**（`ReviewSession/StateMachine.fs`）：`Activate`→`Activated`；`Submit`→`Submitted`（状态可仍为 Active）；`Lock`/`Unlock`；`Accept`/`Reject`。

**Shell 存储**：`ReviewStore` 将 `Registry` 与 `SessionEffects` 单次 `state <-` 原子更新，避免 registry/effects 交错。

### 3.5 WorkBacklog / TodoWrite 契约

**每次调用必填**（`WorkBacklog.fs` + `WorkBacklogToolsCodec`）：

- `todos[]`：**完整替换**，禁止部分更新。
- `ahaMoments`：必填；≥1024 字符；高密度中文，记录本工作步骤的关键突破与发现。
- `changesAndReasons`：必填；≥1024 字符；记录哪些文件或逻辑发生了变更以及每个变更的原因。
- `gotchas`：必填；≥1024 字符；记录遇到的陷阱、边界情况和意外发现。
- `lessonsAndConventions`：必填；≥1024 字符；记录后续开发者应遵循的模式、约定或教训。
- `plan`：必填；≥1024 字符；初次规划时附详细计划并明确说明尚未开始实现；持续工作中描述下一步。
- `select_methodology[]`：至少一项，枚举 SSOT 在 `Kernel.Methodology.methodologyEnumValues`（与 `MethodologyCatalog` 文案一致）。

**折叠**：`MessageTransformPipeline` → `applyBacklogProjection` 将历史 todo 结果压入 backlog 文本（YAML front-matter + `completed_work[]`，每项含 `aha_moments`/`changes_and_reasons`/`gotchas`/`lessons_and_conventions`/`plan`）。

### 3.6 Knowledge Graph（opt-in）

**门控**：工作区存在目录 `kg/` 才注册 KG 工具并注入 prelude（`KnowledgeGraphFiles.knowledgeGraphDirExists`；Opencode `PluginCore.fs:55`，Mux/Omp 工具注册条件同理）。

**文件**：

- `kg/<yyyy-MM-dd>.ndjson`：日文件；头行 `DayHeader` + 事件行 append。
- 日维护：对 **过去** 且 `rewritten=false` 的天执行 `rewriteDay`（tmp + rename）；头行 `rewritten=true`（`KnowledgeGraphMaintenanceRun.fs`、`KnowledgeGraphFiles.fs`）。

**实体语义**：运行时 `validateDraft` 仅格式校验；**抽象中文概念**约束在 bookkeeper prompt 规则（`KnowledgeGraph/Prompts.fs:66-72`），非硬编码白名单表。

### 3.7 方法论工具

- **数量**：`Methodology.Registry.allSchemas` 当前列出 **54** 个 schema（`FirstPrinciples` … `UserIntentClarification`）；README 若写 53 应以 Registry 为准更新文档。
- **工具名**：`methodology_` + `methodologyId`（`SchemaCommon.methodologyToolName`）。
- **注册**：`Methodology.OpencodeTools.registerMethodologyTools`、`MuxTools`、`OmpTools` 遍历 `allSchemas` 注入宿主工具表。

### 3.8 Nudge

**决策优先级**（`Nudge.fs:45-46`）：open todos → active runner → active loop → none；`<skip-todo-check/>` / `<skip-loop-check/>` 或 question 抑制。

**Kernel**：`Coordinator.update` 对 `lastAction + lastMessage` 去重；`Transitions` 管理 `nudgedSessions` / `stoppedSessions` / `retryPending`。

**Runtime**：`NudgeRuntime.HandleEvent`；`StreamEnd` 触发 `runNudgeFlow`；并行 snapshot + `decide`   → 可选 `nudge` 副作用。

---

### 3.9 Multi-FrontMatter 作为等效事实源

一条消息体内允许存在多个 `---` YAML front-matter 块，块间保留 prose body。`PromptFrontMatter` 解析时：

- 按出现顺序提取每个 block；
- 同一 key 后出现的 block 覆盖先出现的 block；
- `bodyAfterFrontMatter` 返回最后一个 block 之后的 prose。

等效性原则：

- 工具输入、工具输出、LLM 输入输出、历史、回放、投影、宿主 wire format 仍然有效；
- `multi-frontmatter` 只是这些事实源的另一种编码；
- 消费方（`LoopMessages`、`ToolOutputInfo`、`ReviewReplayPolicy`、`BacklogProjection` 等）必须按块顺序处理，不能假设只有一块 front matter。

Compaction 补锚点：

- 折叠只压缩消息数组，不替代历史；
- 折叠完成后触发一次真实 `prompt()`，正文固定为 `See above for some messages before compaction.`；
- 该消息的多块 front-matter 包含 backlog projection 与从被压消息提取的 anchors；
- 新消息带 `source: compaction-anchor`，下次折叠时过滤自身，避免指数累积。

迁移顺序：先改 `PromptFrontMatter` 解析，再改只读消费者，再改宿主 codec，最后补测试。

---

## 4. 系统架构

### 4.1 分层拓扑

```
┌─────────────────────────────────────────────────────────────────┐
│  Host adapters (obj hooks, schema, spawn)                        │
│  Opencode/  Mimocode(PluginMimo*)  Mux/  Omp/                    │
└────────────────────────────┬────────────────────────────────────┘
                             │ codec + execute
┌────────────────────────────▼────────────────────────────────────┐
│  Shell/  FS, HTTP, executor, fuzzy, KG IO, review/nudge runtime, │
│          MessageTransformPipeline, PromiseQueue, RuntimeScope      │
└────────────────────────────┬────────────────────────────────────┘
                             │ pure types + functions
┌────────────────────────────▼────────────────────────────────────┐
│  Kernel/  ReviewSession, Nudge, KG, WorkBacklog, ToolCatalog,   │
│           ToolPermission, CapsPrelude, Methodology enum, …       │
└─────────────────────────────────────────────────────────────────┘
         Methodology/*.fs  ──►  schema → ToolSpec 投影（Registry）
```

### 4.2 OpenCode 插件装配（真实 hook 字典）

`PluginCore.pluginFor`（`src/Opencode/PluginCore.fs`）返回对象字段：

| 键 | 职责 |
|----|------|
| `id` / `name` | `"wanxiangshu"` |
| `tool` | `createTools`（含 methodology 条件集） |
| `mcp` | `stealth-browser-mcp` 本地配置 |
| `config` | `applyAgentConfigFor` + `registerCommands`（`/loop`, `/loop-review`） |
| `chat.message` | 会话生命周期、权限相关 |
| `tool.definition` / `tool.execute.before` / `tool.execute.after` | 定义裁剪、执行前后处理（含 KG、read dedup） |
| `experimental.chat.messages.transform` | caps + KG prelude + backlog + review replay 同步 |
| `command.execute.before` | `/loop` 族 + lifecycle |
| `event` | review、KG job 清理、nudge 输入 |
| `experimental.session.compacting` | backlog 与折叠协作 |
| `experimental.chat.system.transform` | system 侧变换 |
| `__knowledgeGraphRuntime` | 测试钩子（非生产契约） |

**`tool.definition` 行为**（`ToolDefinitionHooks.fs`）：对每个工具 id，按优先级处理：

1. `todoWriteToolNameFor host` → 设置 `jsonSchema` 为 `buildWorkBacklogSchema()`（Mimocode 额外走 `mergeWorkBacklogReportIntoTaskSchema` 注入 Zod safeExtend）。
2. `isModificationTool`（coder、executor、write、edit、apply_patch、patch、pty_* 等）→ 设置 `description` + `jsonSchema`，再调用 `injectWarnTddIntoJsonSchema` 注入 `warn_tdd` 必填字段。
3. 其余非修改工具 → 设置 `description` + `jsonSchema` 为 `buildWorkBacklogSchema()` 基线。
4. 若 `isWarnRequiredTool`（executor、pty_*）→ 额外调用 `injectWarnIntoJsonSchema` 注入 `warn` 必填字段。

**Client 能力**（实现依赖 `@opencode-ai/plugin`，与万象阵 PRD §4.2 同族）：`session.prompt`、`session.messages`、`session.create`、`session.command`、`event.subscribe` 等；具体封装在 `Shell.OpencodeClientCodec`、`OpencodeSessionEventCodec`。

### 4.3 Kernel 模块地图（验收用）

| 类 | 代表模块 |
|----|----------|
| 状态机 | `ReviewSession/*`, `Nudge/*`, `KnowledgeGraph/RuntimeState.fs` |
| 协议与提示 | `PromptFrontMatter`, `LoopMessages`, `CapsPrelude`, `ReviewPrompts/*`, `SubagentPrompts` |
| 工具元数据 | `ToolCatalog/*`, `ToolCopy`, `HostTools`, `Config` |
| 消息语义 | `Messaging`, `Message`, `MessageDedup`, `BacklogProjectionCore`, `ReviewReplayPolicy` |
| 纯算法 | `FuzzyQuery`, `Executor`（内核侧模型）, `TreeSitterKernel`, `Methodology`, `MethodologyCatalog` |

### 4.4 Shell 模块地图

| 类 | 代表模块 |
|----|----------|
| IO | `FileSys`, `WorkspaceFiles`, `KnowledgeGraphFiles`, `KnowledgeGraphStorage`, `KnowledgeGraphPortLock` |
| 搜索 | `FuzzyFinderShell`, `FuzzySearch`, `FuzzyIteratorStore` |
| 执行 | `Executor`, `ExecutorJavascript`, `SessionExecutor` |
| 编解码 | `*ToolsCodec`, `OpencodeHookInputCodec`, `MuxHookInputCodec`, `MessagingPartCodec`, `SubagentIntentsCodec` |
| 编排 | `MessageTransformPipeline`, `ReviewRuntime`, `NudgeRuntime`, `ReviewReplaySync`, `ChildAgentRegistry`, `SubagentSpawn` |

### 4.5 消息变换管线（SSOT 顺序）

`MessageTransformPipeline.runMessageTransformPipeline`（`MessageTransformPipeline.fs:16-35`）：

1. `applyBacklogProjection`（非 excluded agent）
2. `encodeMessages`
3. `dedupFn`（read 去重等）
4. 并行 `loadCaps` + `loadKgPrelude`
5. `buildCaps` 注入宝典 prelude + 可选 KG prelude

三宿主入口：`Opencode/MessageTransform.fs`、`Mux/MessageTransform.fs`、`Omp/MessageTransform.fs` — OMP 须经同一 pipeline（架构测试强制）。

---

## 5. 工具目录与权限（SSOT）

### 5.1 Kernel ToolCatalog（17 规范名）

`src/Kernel/ToolCatalog/Registry.fs:16-33`：

```
coder, investigator, meditator, browser, executor,
knowledge_graph_fetch, return_bookkeeper,
fuzzy_find, fuzzy_grep, websearch, webfetch,
submit_review, return_reviewer,
read, write,
executor_wait, executor_abort
```

说明：`executor_wait` / `executor_abort` 在 **Mux** 工具宇宙注册；OpenCode 主表可不暴露（见 `Opencode/Tools.fs`）。

### 5.2 子代理（Subagent）任务种类

`SubagentTaskKind`（`Subagent.fs:10-16`）：Coder / Investigator（多 intent 并行多 prompt）/ Meditator / Browser / ExecutorSummary / WebsearchSummary。

**Mux spawn 工具宇宙**（`HostTools.muxSpawnToolUniverse`）：用于子 workspace `toolPolicy.disabledTools`，与 Mux 原生工具名对齐。

### 5.3 ToolPermission 矩阵（摘要）

**分类**（`classifyTool`）：`TodoFamily`, `Read`, `WritePatchFamily`, `SubagentWebSkillOrSubmit`, `ReturnRoleEcho`, `FuzzyGrep`, `BlockedShellTaskGrep`, …

**关键拒绝**（`canUseSemantic`）：

- `bookkeeper`：**拒绝一切工具**（仅能通过角色 echo `return_bookkeeper` 由编排触发）。
- `reviewer` / `browser`：拒绝除明确允许外的写改类（整体为 false 于多数 semantic）。
- `investigator`：不可 `WritePatchFamily`；可用 `executor`（特例）。
- `coder`：不可直接 `SubagentWebSkillOrSubmit` 族中的派发（须 manager 委派）。
- `manager`：不可 `fuzzy_grep`。
- `bash` / `task` / `grep`（Mux 原生）对子代理归类为 `BlockedShellTaskGrep` → 拒绝。

各宿主在 `AgentConfig` / `Mux PluginCatalog` / `Omp AgentConfig` 调用 `canUseForHost` 生成 disabled 列表。

### 5.4 各宿主工具数量级（含 methodology）

| 宿主 | 内置 + catalog | + methodology_* |
|------|----------------|-----------------|
| OpenCode | ~15 核心 + 条件 KG | +54 |
| Mimocode | 同上；todo 工具名为 `task` | +54 |
| Mux | 更多（bash, edit, patch, glob, question, …） | +54 |
| Omp | Kernel+Shell 注册表 | +54 |

精确名单以 `Opencode/Tools.fs`、`Mux/HostTools.fs`、`Omp/Tools.fs` 为准；集成测试 `IntegrationToolSpecCatalog` / `IntegrationMuxToolSpecs*` 锁契约。

---

## 6. With-Review 详细设计

### 6.1 `/loop` 命令处理（逐步）

来源：`CommandHooks.commandExecuteBefore`（`CommandHooks.fs:31-64`）。

```
1. command ∈ { "loop", "loop-review" }
2. sessionID ← hook input
3. task ← trim(arguments)
4. if task = "":
     deactivateReview(sessionID)
     inject loopCancelledMessage (verdict: cancelled)
5. elif review already active:
     inject reviewAlreadyActiveMessage
6. elif command = "loop":
     activateReview(sessionID, task)
     inject buildLoopMessage(task, ...)
7. else command = "loop-review":
     runReviewerSession(...) → Accepted | Terminated | Rejected(feedback)
     Accepted → preReviewPassedMessage
     Terminated → preReviewCouldNotComplete
     Rejected → activateReview + buildLoopMessage with feedback block
8. setHookParts(output, parts)
```

### 6.2 submit_review 与 reviewer 子会话

- Worker 在 Active 状态调用 `submit_review`（`ToolCatalog` 必填 `report`, `affectedFiles`；`wip` 可选）。
- `ReviewerLoop.runReviewerLoop`：向子 session prompt，若未 `return_reviewer` 则 nudge，上限 `maxNudges`（与万象阵 nudge 上限思想同族）。
- Verdict 渲染回父会话时写入 `verdict` front-matter（`ReviewPrompts/Format.fs`），供 `inferReviewTaskFromTexts` 消费。

### 6.3 重启重放

```
ReviewReplaySync.syncReviewFromTexts(store, sessionID, texts)
  → reviewTaskFromTexts texts
  → syncReviewProjection store sessionID task
```

`syncReviewProjection`：task 变化 → 先 `deactivateReview` 再 `activateReview`（`ReviewRuntime.fs`）。

### 6.4 与 todo / nudge 的交叉

- Loop 活跃时，nudge 可走 `NudgeLoop` 分支（`PromptFragments.loopNudgePrompt`）。
- `submit_review` 前后钩子：`Nudge/SubmitReviewHooks.fs` 与 runtime 协作，避免双发 nudge（Coordinator 去重）。

---

## 7. Todo、Backlog 与方法论选择

### 7.1 TodoWrite 工具描述（SSOT 文案）

`WorkBacklog.toolDescriptionFor host` 拼接：

- 必填五份交接报告字段（`ahaMoments`/`changesAndReasons`/`gotchas`/`lessonsAndConventions`/`plan`，每项 ≥1024 字符）+ 全量 `todos`；
- `Methodology.selectMethodologyFieldDescription`（长枚举 + 定义附录，与 `todowrite` 工具 schema 一致）。

### 7.2 select_methodology 与工具结果

- 字段名 SSOT：`select_methodology`（`Methodology.fs:7`）。
- Todo 工具成功后可追加提示：`Great! Now please explain how to apply [...]`（`todoResultText`）。
- 每个 `methodology_*` 工具：独立 args schema（`intent`, `background` 必填；法专属字段见各 `src/Methodology/*.fs`）。

### 7.3 Compacting 钩子

`experimental.session.compacting` → `compactingHandlerFor`（`PluginCore.fs:96`）与 `BacklogSession` 协作，在压缩前保留 durable backlog 语义（细节见 `Shell/BacklogSessionCodec` 与测试 `WorkBacklogTests`）。

---

## 8. Knowledge Graph 详细设计

### 8.1 工具

| 工具 | 角色 | 要点 |
|------|------|------|
| `knowledge_graph_fetch` | manager 查询 | prelude 注入快照 |
| `return_bookkeeper` | bookkeeper 子代理回写 | `entries[]`；幂等门 `historyHasCompletedReturnBookkeeper` |

### 8.2 写入路径与锁

```
appendDraftsUnderLock / serializedWrite
  → withKnowledgeGraphPortLock (127.0.0.1 端口派生自 workspace 哈希)
  → append NDJSON 或 rewrite tmp+rename
```

默认超时 30000ms，重试间隔 1000ms（`KnowledgeGraphStorage.fs`）。

### 8.3 三宿主 Runtime 差异（摘要）

| | Opencode | Mux | Omp |
|---|----------|-----|-----|
| 状态 | `KnowledgeGraphRuntime` mutable state + commandQueue | `KnowledgeGraphRuntimeMux` + writeQueue | 无状态 IO 函数集 |
| 维护 | `Shell.KnowledgeGraphMaintenanceRun.runMaintenanceIfDue` | 同左 | runtime 内联等价逻辑（README 约束：勿在 Opencode/Mux 外再抄一套未共享调度） |
| 历史幂等 | `loadSessionMessages` + idempotency gate | `GetChatHistory` + decode | 依赖宿主传参 |
| Bookkeeper 后台 | `queueBackgroundLaunch` | `queueMuxBackgroundLaunch` | 无独立 bookkeeper 队列 |

### 8.4 NDJSON 日文件与维护

- **Append**：bookkeeper 产出 → 追加到 **当天** 文件。
- **Rewrite**：对过期且未 rewritten 的日文件，按 `Maintenance.dueMaintenance` 择 **一天** 重写，折叠冗余事实（prompt 规则 `rewritePruneRules`）。

---

## 9. 子代理、执行器、搜索与 Web

### 9.1 委派工具

| 工具 | 并行度 | TDD |
|------|--------|-----|
| `coder` | `intents[]` 每项一子代理 | `tdd`: `red` / `green` 必填 |
| `investigator` | `intents[]` | — |
| `meditator` | 单 intent + `files[]` | — |
| `browser` | 单 intent | — |

意图构造：`SubagentPromptBuild` / `SubagentSpawn`；OpenCode 用 `ChildAgentRegistry`；Mux 用 delegate API；OMP 用 `ChildSession` + `taskService`。

### 9.2 Executor

- 参数：`language`（shell/python/javascript）, `mode`（ro/rw）, `program`, `timeout_type`, `dependencies[]`。
- 内核侧 strip pipe 等：`ExecutorStrip.fs`（纯）；真正执行在 `Shell.Executor` / `SessionExecutor`。
- `executor_wait` / `executor_abort`：Mux 侧长任务控制。

### 9.3 Fuzzy find / grep

- Kernel：`FuzzyQuery`, `FuzzyPath`, `FuzzyFormat`。
- Shell：`FuzzyFinderShell` + `RuntimeScope.IteratorStore`（iterator 分页状态）。
- 输出：YAML front matter + 正文；禁止在 manager 侧滥用 `fuzzy_grep`（权限矩阵）。

### 9.4 Websearch / Webfetch

- `websearch`：原始结果经 **summarizer 子代理**（`WebsearchSummary`）。
- `webfetch`：SSRF/内网防护 `WebFetchGuard`（如 `ip6-loopback` 等拒绝规则，`WebFetchGuard.fs`）。

---

## 10. Caps（宝典）与 Prelude

- 文本 SSOT：`Kernel.CapsPrelude` + `CapsFormat`。
- 会话注入：`MessageTransform` 加载工作区 caps 文件（`CapsFileCache` / `findCapsFiles`），合成 prelude。
- Mux 额外导出 `buildCapsFileReadData`（`Mux/Plugin.fs`）供宿主模拟 read 结构。

---

## 11. 架构边界与测试门禁

### 11.1 强制规则（ArchitectureTests*）

| 规则 | 意图 |
|------|------|
| Kernel 无 Dyn；Kernel 不 open Shell | 纯函数层 |
| Shell 不引用 Opencode/Mux | 依赖方向 |
| Opencode ↔ Mux 互不 import | 宿主隔离 |
| Omp 不引用 Opencode/Mux/engine | 第五宿主 |
| 源码/测试/Methodology ≤300 行 | 文件体量铁律 |
| 无 TODO/FIXME/HACK；无 legacy 输出标记 | 卫生 |
| 工具 args 必经 codec | 禁 inline Dyn 解析 |
| Opencode HookSchema 禁本地 zod / 禁重复 selectMethodology | SSOT |
| FallbackKernel 无 Dyn/Shell/Node 引用；Fallback 相关文件无 setTimeout/setInterval/Date.now | 零定时器 + Kernel 纯度 |
| Omp/Fallback* 无 Opencode/Mux 引用 | Omp 隔离 |

注册表：`tests/TestsArchitectureRegistry.fs` + `TestsArchitectureRegistryB.fs`（约 100 项）。

### 11.2 构建与验收命令

```bash
npm run build-and-test
```

管线（`package.json:28`）：

1. `dotnet fable wanxiangshu.fsproj --outDir build`
2. 清理 `build/fable_modules/.gitignore`；`cp build-package.json build/package.json`
3. `node tests/runner.js` → `build/tests/Tests.js` `runAll`

**Target**：`net10.0`（`Directory.Build.props`）。产物：`artifacts/`、`build/`。

---

## 12. 完整会话示例（OpenCode）

### 12.1 激活 With-Review

```
用户: /loop 实现 PRD.md 中与万象阵同密度的章节
→ command.execute.before: activateReview, 注入 task front-matter 消息
→ LLM 调用 read/investigator/...
→ LLM 调用 todowrite(todos全量, ahaMoments, changesAndReasons, gotchas, lessonsAndConventions, plan, select_methodology)
→ transform: backlog 投影进上下文
```

### 12.2 方法论笔记本

```
LLM 调用 methodology_working_backwards(intent=..., background=..., target_result=..., ...)
→ 工具返回摘要写入会话；notebook 落盘策略由 methodology 工具实现（Shell 侧执行写 notebook 目录，若启用）
```

### 12.3 提交审查

```
LLM 调用 submit_review(report, affectedFiles, wip=false)
→ spawn reviewer → return_reviewer(verdict=...)
→ 父会话注入 verdict front-matter
→ accepted → inferReviewTaskFromTexts 清除 task；rejected → 保持 Active
```

### 12.4 KG 工作区（存在 kg/）

```
transform 注入 knowledge_graph prelude
任务结束路径触发 bookkeeper → return_bookkeeper(entries)
→ NDJSON append；fetch 工具读投影
```

---

## 13. 错误处理与边界

| 场景 | 行为 |
|------|------|
| 无 `kg/` | 不注册 KG 工具；transform 不注入 KG prelude |
| todowrite 缺任一交接报告字段 | codec `InvalidIntent`（字段名 + reason）；错误可折叠进 backlog 正文（`BacklogProjectionCore`） |
| todowrite 交接报告字段 <1024 字符 | codec `InvalidIntent`；拒绝并提示最小长度 |
| review 已 active 再 `/loop` | `reviewAlreadyActiveMessage` |
| bookkeeper 重复提交 | 历史幂等门拒绝第二条 `return_bookkeeper` |
| 端口锁超时 | 写失败；调用方重试；不静默丢事实 |
| OMP 误引 Opencode | 架构测试失败 |
| Mimocode todo 名 | 必须用 `task` 工具名写入（非 `todowrite`） |

哲学：**可预见失败 → 返回分支 + 固定文案**；禁止对工具输出做脆弱正则（宝典铁律）。

---

## 14. 插件接口清单

### 14.1 OpenCode / Mimocode

| 类型 | 名称 |
|------|------|
| Slash | `/loop`, `/loop-review`（`registerLoopReviewCommands`） |
| Tool | §5.1 + methodology_* + 条件 KG |
| Hook | §4.2 表 |
| MCP | stealth-browser-mcp（可选 env） |

### 14.2 Mux

| 类型 | 名称 |
|------|------|
| 注册表 | `createToolCatalog`, `createRegistration` |
| Wrapper | 工具 execute 签名适配 |
| Slash / 事件 | 见 `Mux/PluginRegistration.fs` |
| Read dedup | `deduplicateReadOutputsWithSeen` 导出 |

### 14.3 OMP

| 类型 | 名称 |
|------|------|
| 扩展 | `wanxiangshuExtension(pi)` |
| Schema | `OmpToolSchema`（TypeBox）；methodology 参数 |
| 子 workspace | `ChildSession` |
| Review | `ReviewLoop` + `ReviewToolsRegister` |

---

## 15. 技术约束

- **语言**：F# → Fable 4.x → ES module（`package.json` `"type": "module"`）。
- **异步**：仅 `promise {}` / `JS.Promise`；禁 Async/Task（宝典）。
- **依赖**：`yaml`（配置与事件 front matter）、`@opencode-ai/plugin`（Opencode 侧）；可选 tree-sitter、tsx 等见 `optionalDependencies`。
- **Mux 改动**：允许改 `../mux` binding，核心改动优先本仓库（`AGENTS.md`）。
- **OMP**：不允许改 `../oh-my-pi` 上游。

---

## 16. 明确不做的事情

| 不做 | 原因 |
|------|------|
| 多进程 DAG / worktree 协调 | 属 **万象阵** 范畴 |
| 全局常开 KG | `kg/` 门控；避免无知识工程会话暴露记账面 |
| Kernel 内直接读盘/打网 | 破坏可测性 |
| 单文件巨型 ToolSchema | 已拆 `ToolCatalog/*` + per-methodology 文件 + 300 行门禁 |
| 在 Omp 复制第二套未共享 Opencode 逻辑 | 架构测试 + README 纪律 |
| 用自然语言 alone 恢复 review 任务 | 必须用 front-matter 字段 |

---

## 17. 分期与验收（建议）

### 17.1 当前仓库已具备（持续回归）

- [x] 四宿主入口编译 + `npm run build-and-test`
- [x] ToolCatalog SSOT + 三宿主注册 + Registry 全部 methodology
- [x] `/loop` + submit_review + replay
- [x] todowrite + backlog 投影 + select_methodology
- [x] kg/ 门控 + NDJSON + 维护 + 端口锁
- [x] ArchitectureTests* 门禁

### 17.2 可增强（非阻塞）

- [ ] 更细的 PRD 附录：每个 `methodology_*` 字段表（已由 schema 单测覆盖）
- [ ] 用户可编辑 caps 文件的 UI 文档化
- [ ] 跨宿主行为一致性矩阵自动化报告

---

## 18. ReviewSession 合法转移表（实现基准）

来源：`src/Kernel/ReviewSession/StateMachine.fs:5-18`。未列出组合 → `(state, None)`，版本不增。

| 当前 state | ReviewCommand | 下一 state | ReviewEvent |
|------------|---------------|------------|-------------|
| Inactive | Activate task | Active task | Activated task |
| Active task | Submit | Active task | Submitted |
| Active task | Lock reviewerId | Locked(task, reviewerId) | LockAcquired reviewerId |
| Active task | Accept | Accepted | Accepted |
| Active task | Reject feedback | Rejected feedback | Rejected feedback |
| Locked(task, _) | Unlock | Active task | LockReleased |
| Locked _ | Accept | Accepted | Accepted |
| Locked _ | Reject feedback | Rejected feedback | Rejected feedback |

**isActive**（L20-26）：`Inactive` / `Accepted` → false；`Active` / `Locked` / `Rejected` → true（Rejected 仍算 active 语义上「审查未通过、工作继续」）。

**Reviewer 轮次控制**（L36-40）：

```
decideAfterRound(nudgeCount, outcome, maxNudges):
  Resolved result     → Finish result
  PromptFailed        → Finish Terminated
  NoResult            → if nudgeCount+1 >= maxNudges then Finish Terminated else Nudge(nudgeCount+1)
```

---

## 19. Loop front-matter 与重放（完整字段 SSOT）

| 常量 | 值 / 用途 | 文件 |
|------|-----------|------|
| `taskField` | `"task"` | LoopMessages.fs:12 |
| `originalTaskField` | `"original_task"` | L15-16 |
| `verdictField` | `"verdict"` | L18 |
| `verdictAccepted` | 结束 loop | L19 |
| `verdictCancelled` | 结束 loop | L22 |
| `verdictRejected` / `verdictTerminated` | **不**结束 loop | L20-21, isEndVerdict L27-30 |
| `commandWithReview` | slash 模板 | L23-24 |
| `doubleCheckField` | 双检锚点 | L75-78 |

**重放算法**（L64-73）：按消息时间序 fold；见到非空 `task` → 覆盖为 Some task；见到 END verdict → None；其余保持 current。

**与万象阵协同**：万象阵 slave 初始 prompt 可要求「`/loop <描述>`」或 front-matter `task:`（万象阵 PRD §6.2 路径 i）；字段名与本节 `taskField` 一致时，万象术 `inferReviewTaskFromTexts` 可识别。

---

## 20. Nudge：事件、抑制与 Runtime 路由

### 20.1 决策输入（NudgeContext）

- todos 是否仍有 open 项
- `lastAssistantMessage` 是否含 question / skip 标签
- `hasActiveRunner`（子代理/执行器仍在跑）
- `isLoopActive`（review store 对该 session active）

标签（`Nudge.fs`）：`nudge-todo` / `nudge-loop`；正则抑制 `skip-todo-check` / `skip-loop-check`。

### 20.2 Coordinator 去重（Kernel）

`Coordinator.update`：同一 `(lastAction, lastMessage)` 不重复发；`suppressSession` 在 stream-end 前记录抑制，防止结束事件二次触发。

### 20.3 Runtime 事件（Shell）

`NudgeRuntime.HandleEvent` 路由：

- `StreamEnd` → `runNudgeFlow`（并行 snapshot + decide → 可选 nudge）
- `StreamAbort` / `AbortedError` → `clearSession`

**分裂理由**（README）：纯 `decide` 可单测；`session.prompt` 异步，不可堵在 Kernel 串行队列。

---

## 21. Knowledge Graph：NDJSON 与维护状态机

### 21.1 路径与探测

```
knowledgeGraphDir(root)     = join root "kg"
knowledgeGraphDirExists     = existsSync(kg)
dayPath(root, yyyy-MM-dd)   = kg/<date>.ndjson
```

### 21.2 日文件头（DayHeader）

头行记录日期与 `rewritten` 标志；`rewritten=false` 的**过去**日期进入维护队列（`dueMaintenance` 每次最多处理一天，避免长事务）。

### 21.3 写入纪律

1. `withKnowledgeGraphPortLock(workspaceRoot)` 获取本机端口锁
2. `appendFile` 或 `writeFile(.tmp)` + `rename`（rewrite）
3. 内存 projection / runtime 状态更新在 IO 成功之后

**幂等**：`historyHasCompletedReturnBookkeeper` 在历史消息中存在 completed 的 `return_bookkeeper` 工具结果时，拒绝第二次记账（`KnowledgeGraph/Idempotency.fs`）。

### 21.4 Bookkeeper 质量规则（prompt SSOT）

摘录 `KnowledgeGraph/Prompts.fs:66-72`：

- 事实用现代压缩中文
- `entity` 必须为**中文抽象概念**；禁文件名/模块名/工具名等实现标签
- 鼓励复用已有 entity，控制词表规模

---

## 22. Methodology 工具契约（每法一 schema）

### 22.1 公共字段（全部 methodology_*）

| 字段 | 必填 | 说明 |
|------|------|------|
| `intent` | 是 | 本法要服务的根本意图（建议约 512 词，无硬最小字数） |
| `background` | 是 | 绑定本仓库本回合的笔记本上下文 |
| 法专属字段 | 依 schema | 见各 `src/Methodology/<Name>.fs` |

工具名：`methodology_` + `methodologyId`（`SchemaCommon.fs:58`）。

### 22.2 Schema 元数据

每个 `MethodologySchema` 含：`shortDefinition`, `triggerWhen`, `toolDescription`, `fields[]`, `meditatorRole`, `outputSections[]`。

Notebook 输入渲染：`renderInputYaml` 写入 methodology 笔记本文件（Shell/宿主执行路径）。

### 22.3 与 todowrite 的耦合

- `todowrite` / `task`（Mimocode）必填五份交接报告（≥1024 字符/项）+ `select_methodology[]`，枚举与 `Kernel.Methodology.methodologyEnumValues` 一致。
- 工具描述尾部拼接 `MethodologyCatalog` 长文（`WorkBacklog.toolDescriptionFor`）。

### 22.4 注册路径

| Host | 模块 |
|------|------|
| Opencode/Mimocode | `Methodology.OpencodeTools.registerMethodologyTools` |
| Mux | `Methodology.MuxTools` |
| Omp | `Methodology.OmpTools` + `OmpToolSchema.methodologyParameters` |

---

## 23. Agent 角色、Config 合并与工具裁剪

### 23.1 已知 agent 集合

`ToolPermission.knownAgentSet`：`manager`, `investigator`, `coder`, `reviewer`, `browser`, `meditator`, `executor`, `bookkeeper`。

### 23.2 各宿主 Config 钩子

- OpenCode：`Opencode/AgentConfig.applyAgentConfigFor` — 合并 agent 定义、注入 permission、`canUseForHost` 禁用工具列表。
- Mux：`Mux/PluginCatalog.buildToolPolicy` + `getPluginToolPolicy`。
- Omp：`Omp/AgentConfig` 同等逻辑，TypeBox schema。

### 23.3 MCP

OpenCode `PluginCore` 注册 `stealth-browser-mcp` 本地 MCP（`Config.getStealthBrowserMcpLocalConfig`）；仅 `browser` agent 可用 stealth-browser 工具族。

---

## 24. Mux 宿主：注册表、Wrapper 与工具宇宙

### 24.1 导出函数（`Mux/Plugin.fs`）

- `createToolCatalog` / `createRegistration`：宿主加载入口
- `getPluginToolPolicy`：子 workspace 工具策略
- `collectReadOutputs` / `deduplicateReadOutputsWithSeen`：read 去重供 transform 使用
- `buildCapsFileReadData`：caps 文件模拟 read 结构

### 24.2 Mux 独有工具名（在 Kernel catalog 之外）

`bash`, `grep`, `edit`, `patch`, `apply_patch`, `glob`, `skill`, `question`, `agent_report`, `executor_wait`, `executor_abort`, Mux 原生 `task_*` 族等（`HostTools.allToolNames mux`）。

### 24.3 子代理 spawn

Mux 通过 delegate API + `muxSpawnToolUniverse` 约束子 workspace 可见工具；归一化后仍走 `Shell.MuxSubagentToolExecute` / `ToolArgsDecode`。

---

## 25. OMP 宿主：扩展、Magic Todo、子 workspace

### 25.1 入口

`wanxiangshuExtension(pi)`：`WeakSet` 防重复注册 → `PluginCore.pluginFor pi`。

测试可见：`reviewStore` 与 `PluginCore` 单例一致（`Omp/Plugin.fs:37-40`）。

### 25.2 Magic Todo

`Omp/MagicTodo.fs` + `MessageTransform`：在 OMP 消息流中投影 magic-todo 前缀（`Messaging.fs` 中前缀常量）；与 Mimocode TUI `PluginMimoTui` sidebar 回填形成同类「结构化 todo 可见性」，实现面不同。

### 25.3 ChildSession

子 workspace 路径：`Omp/ChildSession.fs`；工具注册 `Omp/Tools.registerAllTools`（架构测试要求覆盖 fuzzy/executor/subagent/todo/methodology）。

### 25.4 KG on OMP

`Omp/KnowledgeGraph/Runtime.fs`：无长驻 commandQueue 的完整 Opencode 形态；`magicGetEntries` 解析 job 上下文；维护逻辑内联但与 Shell `runMaintenanceIfDue` **语义对齐**（README 纪律）。

---

## 26. 编解码与 ToolExecute 统一路径

### 26.1 原则

**禁止**在 Opencode/Mux/Omp 工具 execute 内手写 `Dyn.get` 解析 args；必须经：

```
ToolArgsDecode → Kernel 类型 / DU
ToolExecute / SubagentToolExecute / MuxSubagentToolExecute
→ 宿主包装输出 ToolResult
```

架构测试扫描 inline `Dyn.str` / 重复 schema 字段。

### 26.2 主要 Codec 文件（Shell）

| 族 | 模块 |
|----|------|
| 子代理 | `SubagentIntentsCodec`, `DelegateToolsCodec` |
| 文件 | `FileToolsCodec`, `PatchToolsCodec` |
| 执行器 | `ExecutorToolsCodec` |
| 审查 | `ReviewToolsCodec` |
| KG | `KnowledgeGraphToolsCodec` |
| Todo | `WorkBacklogToolsCodec`, `WorkBacklogSchema` |
| 模糊搜索 | `FuzzyToolsCodec` |
| Web | `WebToolsCodec`, `WebSearchCodec` |
| Opencode hook | `OpencodeHookInputCodec`, `OpencodeSessionEventCodec`, `ChatHookOutputCodec` |
| Mux hook | `MuxHookInputCodec`, `MuxWorkspaceCodec` |
| 消息 | `MessagingPartCodec`, `HostMessagePartCodec` |

### 26.3 Hook output 写路径

禁裸 `setKey output args` / `Dyn.get output args`；须经 `OpencodeHookInputCodec` / `ChatTransformOutputCodec` 等（README 架构纪律）。

---

## 27. 子代理与 ChildAgentRegistry

### 27.1 OpenCode

`Shell.ChildAgentRegistry`：跟踪子 session 与 abort；`SessionLifecycleObserver` 在 command/event 上清理。

### 27.2 并行 intent

`coder` / `investigator` 的 `intents[]` 每项 spawn 独立子代理；`SubagentIntentsCodec` 在 execute.before 注入 `_ui` 标签（hook 填充，LLM 不可写）。

### 27.3 TDD 相位（coder）

`ToolCatalog` 要求 `tdd` ∈ `{ red, green }`：

- **red**：本编辑为 RED——测试应失败或尚未通过
- **green**：本编辑为 GREEN——使既有失败测试通过

纪律：同一工作单元应先 red 再 green（manager 提示与 codec 拒绝语在 `ToolCopy`）。

### 27.4 Mimocode 尾注

`Subagent.formatPrompt` 对 Mimocode 在 investigator 等报告体后追加 **必须调用 `agent_report`** 尾注（`Subagent.fs:18-27`）。

---

## 28. Read 去重与 Message 投影

### 28.1 动机

长会话重复 `read` 同一文件会膨胀 context；在 **编码后** 对 tool output 去重，保留首次完整输出。

### 28.2 实现面

- OpenCode：`ReadDedupOpenCode` + transform 管线 `dedupFn`
- Mux：`ReadDedupMuxPlugin` + `Mux/Plugin.fs` 导出
- 策略核心：`Kernel.MessageDedup` / `Dedup`

### 28.3 Agent 投影排除

`MessageTransformPolicy.shouldExcludeAgentFromProjection`：部分 agent（如 reviewer）消息不进入主会话投影，避免污染 worker 上下文。

---

## 29. 安全与滥用面（engineering review）

| 边界 | 措施 |
|------|------|
| Webfetch URL | `WebFetchGuard`：内网/loopback 等拒绝 |
| Executor `mode=rw` | 仅允许修改项目源；诊断用 `ro` |
| 方法论 `background` | 大段用户可控文本写入笔记本——信任模型为「主会话管理员」 |
| KG port lock | 仅本机 127.0.0.1；降低跨进程写撕裂 |
| Tool permission | 默认拒绝 `bookkeeper` 主动调用任意工具 |

---

## 30. 端到端数据流（单 OpenCode 会话）

```
用户消息
  → chat.message / command.execute.before（/loop 激活 review）
  → LLM tool calls
  → tool.execute.before（权限/args decode/TDD 门）
  → tool.execute.after（KG job、read dedup 登记）
  → event（nudge、KG cleanup、lifecycle）
  → experimental.chat.messages.transform
        → backlog 投影
        → encode + dedup
        → caps prelude + kg prelude（若 kg/）
        → review replay sync（inferReviewTaskFromTexts）
  → 模型所见上下文
```

Compaction：`experimental.session.compacting` 与 `BacklogSession` 保留 durable 交接，不替代五份交接报告义务。

---

## 31. 测试体系与验收探针

### 31.1 运行器

- `tests/runner.js` → `build/tests/Tests.js` `runAll`；失败 exit code 非 0。
- `e2e/harness.js` + `e2e/mock-llm.js`：端到端插件测试框架，mock LLM 响应、工具 round-trip、插件热身。测试用例在 `e2e/Tests.fs`。

### 31.2 测试分层

| 层级 | 示例 |
|------|------|
| 纯 Kernel | `KernelTests.fs`, `ReviewTests.fs`, `KnowledgeGraphKernelTests.fs`, `FallbackKernelTests.fs` |
| Shell codec | `*CodecTests.fs`, `ToolArgsDecodeTests.fs`, `FallbackConfigCodecTests.fs`, `FallbackEventBridgeTests.fs`, `FallbackRuntimeStateTests.fs` |
| 架构 | `ArchitectureTests*.fs` + `TestsArchitectureRegistry*.fs` |
| 集成 | `IntegrationPluginTests.fs`, `IntegrationMux*`, `IntegrationOpencodeReviewSpecs.fs`, `FallbackIntegrationTests.fs` |
| OMP | `OmpPluginTests.fs`, `OmpKnowledgeGraphRuntimeTests.fs`, … |
| E2E | `e2e/Tests.fs`（mock-llm + harness + 插件热身 + 工具 round-trip） |

### 31.3 集成探针（契约锁定）

- `IntegrationToolSpecCatalog`：工具 schema 与 `ToolCatalog` 一致
- `IntegrationMuxMethodologySpecs`：54 methodology 注册
- `IntegrationSubmitKnowledgeGraphSpecs`：append + 幂等
- `KernelPromptSpecs*`：提示词 SSOT 快照

---

## 32. 仓库布局与编译顺序

`wanxiangshu.fsproj` **编译顺序即依赖顺序**：Kernel（含 ReviewSession 子目录、KnowledgeGraph 子目录）→ Shell → Methodology 各法 → Opencode / Mux / Omp → tests。

中间产物：`artifacts/`（MSBuild）、`build/`（Fable JS）。清理：`rm -rf build artifacts`。

**提交纪律**（`AGENTS.md`）：提交时包含 `kg/` 变化（若工作区使用 KG）。

---

## 33. 错误处理扩充表

| 场景 | 行为 | 源码锚点 |
|------|------|----------|
| Opencode client 不可用 | KG runtime 用 null client；功能降级 | PluginCore.fs:56-59 |
| `/loop-review` client 错误 | `Terminated` → `preReviewCouldNotComplete` | CommandHooks.fs:52-59 |
| methodology 工具缺 intent | codec 拒绝 | 各 Methodology schema + tests |
| fuzzy iterator 过期 | 返回下一页 iterator 或空 | FuzzyIteratorStore |
| KG 锁超时 | 写失败；调用方重试 | KnowledgeGraphPortLock |
| OMP 重复注册 extension | WeakSet 跳过 | Omp/Plugin.fs:45-47 |
| Review 非法转移 | `transition` 返回 None | StateMachine.fs:18 |
| submit_review 非 active | ToolCopy 拒绝语 | ToolCopy.fs |
| 文件 >300 行（产码） | 架构测试失败 | ArchitectureTestsFoundation |
| Fallback 链全部耗尽 | consumed=false → Nudge 感知失败 → 传播给父代理 | FallbackHooks + NudgeRuntime |
| Fallback 模型错误（401/429/5xx） | 拦截 + 切换 fallback 链中下一个模型 | FallbackKernel.StateMachine |
| Modification 工具缺 warn_tdd | tool.execute.before 拒绝并提示 canonical value | WarnTdd.parseWarnTdd |

---

## 34. OpenCode 真实 API 映射表（万象术使用面）

| 能力 | 万象术封装 | 典型用途 |
|------|------------|----------|
| 插件工厂 | `pluginFor host ctx` | 返回 hook 字典 |
| client.session.prompt | `SessionIo` / codec | nudge、review 子会话 |
| client.session.messages | `SessionIo` | KG 幂等、历史 |
| client.session.create | `SubagentSpawn` | 子代理 |
| client.session.command | 可选（万象阵路径 ii） | 未作为万象术 MVP 主轴 |
| event.subscribe | `EventHooks` / `NudgeRuntime` | stream-end |
| experimental.chat.messages.transform | `MessageTransform*` | caps/backlog/kg/review |
| command.execute.before | `CommandHooks` | /loop |
| tool.* hooks | `ToolDefinitionHooks`, `HookExecute` | 权限与执行 |
| config hook | `registerLoopReviewCommands` | slash 模板 |

**明确不存在于 opencode 且万象术不依赖**：child-exit hook（万象阵用 PID/beacon 替代）。

---

## 35. 配置与环境

### 35.1 环境变量（示例）

- `STEALTH_BROWSER_MCP_REF`：stealth browser MCP 命令解析
- `SEMBLE_MCP_REF`：semble MCP server 版本 ref（若启用）
- 测试钩子：`PluginCore` 可读 `ctx.nowMs` 固定时间

### 35.2 工作区文件

- `AGENTS.md`：用户规则；方法论 background 应引用其中约束
- `kg/`：启用 KG
- Caps 文件：由 `WorkspaceFiles` / `findCapsFiles` 发现，注入 prelude

### 35.3 用户规则（本仓库 AGENTS.md）

- 提交带 `kg/` 变更
- Mux 可改 `../mux` binding；Omp **不可**改 `../oh-my-pi`

---

## 36. 分期路线图（产品）

### 36.1 已交付（本 PRD 描述现状）

四宿主、ToolCatalog、methodology 全注册、review+replay、backlog、kg 门控、架构测试、npm 包 exports、**Fallback 模型降级**（完全平方数启发式 + 实际 continue 探测 + 三宿主适配）、**WarnTdd**（修改类工具 TDD 声明 + warn 副作用确认）、e2e 测试基础设施。

### 36.2 可演进

- 统一 README 与 Registry 对 methodology **数量** 的表述（53 vs 54）
- 更细 KG entity 运行时校验（当前靠 prompt）
- 跨宿主集成测试矩阵 HTML 报告

---

## 37. 不做的事情（扩充）

| 不做 | 说明 |
|------|------|
| 在 Kernel 写 HTTP server | 属万象阵 coordinator |
| 用 transform 切片做 review 重放 SSOT | 必须用全量 messages 路径 |
| Omp 引用 Opencode PluginCore 副本 | 共享 Shell，单份 `createReviewStore` 模式 |
| 允许 manager 默认 fuzzy_grep | 权限矩阵显式拒绝 |
| 在架构测试中放行 >300 行产码文件 | 铁律 |
| 默认开启 KG | 无 `kg/` 不注册 |
| Fallback 覆盖上下文溢出 | 由 opencode 自身压缩机制处理，Fallback 不干预 |

---

## 附录 A：术语对照

| 术语 | 定义 |
|------|------|
| Kernel | 去宿主后可执行的纯规则 |
| Shell | Node/宿主 codec 与 IO |
| With-Review Mode | `/loop` 激活的审查循环 |
| Front-matter 锚点 | `---\nkey: value\n---` 机器可读前缀 |
| Backlog projection | 历史 todo 交接报告压缩文本 |
| ToolSemantic | 权限分类中间层 |
| SerialQueue | Promise 链式串行执行 |
| Bookkeeper | KG 子代理角色 |
| Fallback | 模型降级状态机（完全平方数启发式 + 实际 continue 探测） |
| WarnTdd | 修改类工具 TDD 声明 + 副作用确认机制 |

## 附录 B：与万象阵 PRD 对照

| 维度 | 万象术（本文档） | 万象阵 |
|------|------------------|--------|
| 进程模型 | 单宿主进程内多子代理 | coordinator + 多 slave opencode |
| SSOT | session 历史 + kg 文件 | session 事件流 + git refs |
| Review | 内置 `/loop` | 依赖万象术 |
| 合并 | 无 | ff-only worktree |
| HTTP | 无 coordinator server | slave→coordinator |
| 串行 | KG 锁、git（阵）、队列 | SerialQueue on master |

## 附录 C：Review 重放伪代码

```
inferReviewTaskFromTexts(texts):
  fold texts with current=None:
    parseFrontMatterScalars(text)
    if task field non-empty → Some task
    elif verdict ∈ {accepted, cancelled} → None
    else → current
```

## 附录 D：ToolCatalog 与 Host 映射速查

- 规范名列表：§5.1
- Mimocode：`task`→todo 写入，`actor`→`task` 子代理
- Mux：`normalizeToolNameForMux` 全文见 `HostTools.fs:34-44`
- 权限：`ToolPermission.fs` 全文 86 行内

## 附录 E：Methodology ID 全表（54）

与 `src/Methodology/Registry.fs:59-113` 一致（共 54 项）：

`first_principles`, `axiomatization`, `deduction`, `induction`, `abduction`, `analogy`, `specialization`, `generalization`, `working_backwards`, `analysis_synthesis`, `auxiliary_construction`, `equivalent_transformation`, `decomposition_recombination`, `model_problem_transfer`, `constructive_method`, `reductio_ad_absurdum`, `invariance`, `symmetry_analysis`, `dimensional_reduction`, `perturbation_continuity`, `pigeonhole_principle`, `duality`, `quotient_space`, `category_mapping`, `relaxation`, `search_space_exploration`, `branch_and_bound`, `dynamic_programming`, `monte_carlo_sampling`, `simulated_annealing`, `swarm_optimization`, `systems_thinking`, `root_cause_analysis`, `state_machine_reasoning`, `type_driven_design`, `event_sourcing`, `operationalism`, `bayesian_update`, `falsification`, `thought_experiment`, `transcendental_argument`, `conceptual_analysis`, `dialectical_analysis`, `hermeneutic_circle`, `deconstruction`, `renormalization`, `simplification`, `tradeoff_analysis`, `risk_analysis`, `test_driven_reasoning`, `debugging_trace`, `security_review`, `performance_analysis`, `user_intent_clarification`

工具名 = `methodology_` + 上表 id。

## 附录 F：源码阅读顺序（与 README 对齐）

1. `src/Kernel/ReviewSession/Types.fs` + `StateMachine.fs`
2. `src/Kernel/LoopMessages.fs`
3. `src/Kernel/Nudge.fs` + `src/Kernel/Nudge/Coordinator.fs`
4. `src/Kernel/KnowledgeGraph/Types.fs` + `src/Shell/KnowledgeGraphFiles.fs`
5. `src/Kernel/BacklogProjectionCore.fs` + `WorkBacklog.fs`
6. `src/Shell/MessageTransformPipeline.fs`
7. `src/Opencode/PluginCore.fs` + `CommandHooks.fs`
8. `src/Kernel/ToolCatalog/Registry.fs` + `ToolPermission.fs`
9. `src/Methodology/Registry.fs`
10. `tests/TestsArchitectureRegistry.fs`

---

## 附录 G：OpenCode Hook 与万象阵 PRD API 表对齐说明

万象阵 PRD §4.2 列出的 `PluginInput.client`、`session.prompt`、`session.messages`、`tool` 注册、`command.execute.before` 等，在万象术中均有对应实现文件，但 **万象术不实现** `Worktree.*`、coordinator HTTP、slave `submit_to_squad`。实现万象阵时复用万象术 **Kernel/Shell** 模块的清单见万象阵 PRD §12.1。

---

## 附录 H：`package.json` exports 与发布

```json
".": "build/src/Mux/Plugin.js",
"./omp": "build/src/Omp/Plugin.js"
```

OpenCode / Mimocode 插件 JS 由宿主配置指向 `build/src/Opencode/Plugin.js` 等（见各宿主文档）；npm 包默认入口为 Mux。

---

## 附录 I：Kernel ToolCatalog 参数契约（SSOT 摘录）

来源：`src/Kernel/ToolCatalog/*.fs`。宿主 schema 生成不得偏离 `description` / `requiredFields` / `paramDocs`。

### I.1 子代理

**coder**（`Subagent.fs:5-18`）

- 必填：`intents[]`（每项含 `objective`, `background`, `targets[]` 含 `file`+`guide`，可选 `draft`；可选 `do_not_touch[]`），`tdd` ∈ `red`|`green`
- 并行：每个 intent 一个 coder 子会话

**investigator**（L20-29）

- 必填：`intents[]`（`objective`, `background`, `questions[]`；可选 `entries[]`）

**meditator**（L31-42）

- 必填：`intent`, `files[]`

**browser**（L44-53）

- 必填：`intent`

### I.2 审查

**submit_review**（`Review.fs:5-15`）

- 必填：`report`, `affectedFiles[]`
- 可选：`wip`（默认 true；false 表示宣称全量完成）

**return_reviewer**（L17-24）

- 必填：`verdict`（PASS/REJECT 语义见 `ReviewVerdict`）
- REJECT 时应提供 `feedback`

### I.3 Knowledge Graph

**knowledge_graph_fetch**（`KnowledgeGraph.fs:5-9`）

- 必填：`entity`

**return_bookkeeper**（L11-22）

- 必填：`entries[]`（`entity[]`, `fact`；`id` 可选更新）

### I.4 执行器

**executor**（`Executor.fs:5-18`）

- 必填：`language`, `program`, `timeout_type`, `mode`
- 可选：`dependencies[]`

**executor_wait** / **executor_abort**（L20-30）：Mux 可见；参数见 `paramDocs`。

### I.5 文件与搜索

见 `FileIO.fs`、`Search.fs`、`Web.fs`：`read` 用 `filePath`；`write` 用 `filePath`+`content`；`fuzzy_find`/`fuzzy_grep` 支持 `iterator` 分页；`websearch` 必填 `query`+`what_to_summarize`；`webfetch` 必填 `url`。

### I.6 TodoWrite（todowrite / task）

**todowrite**（`WorkBacklog.fs` + `WorkBacklogToolsCodec.fs`）

- 必填：`todos[]`（每项含 `content`, `status`, `priority`）
- 必填（≥1024 字符）：`ahaMoments`, `changesAndReasons`, `gotchas`, `lessonsAndConventions`, `plan`
- 必填：`select_methodology[]`（≥1 项，枚举 SSOT 在 `Registry.enumValues`）

**Mimocode task**：同上五份报告字段 + `select_methodology`，经 `mergeWorkBacklogReportIntoTaskSchema` 注入 Zod schema。

### I.7 WarnTdd 工具集

`WarnTdd.modificationTools`（必须附带 `warn_tdd` 确认字段）：

```
coder, executor, write, edit, apply_patch, patch,
ast_edit, ast_grep_replace, file_edit_replace_string, file_edit_insert,
pty_spawn, pty_write, pty_read, pty_list, pty_kill
```

`WarnTdd.warnRequiredTools`（必须附带 `warn` 确认字段）：

```
executor, pty_spawn, pty_write, pty_read, pty_list, pty_kill
```

canonical value：`i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles`（`warn_tdd`）；`it-is-not-possible-to-do-it-using-other-tools`（`warn`）。

---

## 附录 J：Backlog 投影 YAML 形状

`BacklogProjectionCore.buildBacklogText`（L61-65）：

- front-matter 根：数组项含 `user_message[]`（自上次 todo 以来的 user 文本）与五份报告字段（`aha_moments`, `changes_and_reasons`, `gotchas`, `lessons_and_conventions`, `plan`）
- 正文固定句：`Completed work from folded turns. File changes are already on disk.`
- 若上次 todowrite 为 error 状态，追加 `Last todo write error: ...`（L54-59）

折叠触发：`ToolPart` 且 `toolName = todoWriteToolName host` 且 `status = completed`（L16-18）。

---

## 附录 K：目录树（与 README 一致，细化）

```text
wanxiangshu-1/
  src/
    Kernel/
      ReviewSession/     Types, StateMachine, Registry, Effects, Facade
      KnowledgeGraph/    Types, Draft, Maintenance, Prompts, Idempotency, …
      Nudge/             Coordinator, Decision, TodoStatus, Transitions
      ToolCatalog/       Registry + 分族 Spec
      CapsPrelude.fs     宝典/铁律 SSOT
    Shell/               codec, IO, runtime, MessageTransform*
    Methodology/         54× schema + Registry.fs
    Opencode/            Plugin, PluginCore, Tools, Review*, KG runtime
    Mux/                 Plugin, PluginRegistration, HostTools, MessageTransform
    Omp/                 Plugin, PluginCore, MagicTodo, ChildSession, KG/
  tests/                 unit + integration + ArchitectureTests*
  build/                 Fable 输出
  kg/                    （用户工作区，非仓库必含）
  PRD.md                 本文档
  README.md              架构叙事
  wanxiangshu.fsproj     编译列表 SSOT
  package.json           exports + build-and-test
```

---

## 附录 L：完整工作流示例（Mux + kg/ + review）

```
[1] 用户在 Mux 主会话输入需求；manager 调用 todowrite(
      todos=全量列表,
      ahaMoments="关键突破：发现…",
      changesAndReasons="变更…",
      gotchas="陷阱…",
      lessonsAndConventions="教训…",
      plan="详细计划…",
      select_methodology=["working_backwards","test_driven_reasoning"])

[2] transform 将历史 completed todowrite 折叠为 backlog front-matter

[3] 用户或系统激活 review（若宿主暴露等效 /loop 语义）→ ReviewStore.activateReview

[4] manager 调用 methodology_root_cause_analysis(...) 记录笔记本

[5] manager 调用 investigator(intents=[{ objective, background, questions }])

[6] manager 调用 coder(intents=[...], tdd="red") 再 coder(..., tdd="green")

[7] manager 调用 submit_review(report, affectedFiles, wip=false)

[8] reviewer 子会话 return_reviewer(verdict=PASS) → 父会话 verdict: accepted front-matter

[9] 若 kg/ 存在：任务收尾触发 bookkeeper → return_bookkeeper(entries) → NDJSON append

[10] 日切后 runMaintenanceIfDue 重写最早未 rewritten 的历史日文件
```

---

## 附录 M：`inferReviewTaskFromTexts` 与 `ReviewStore` 协作序列

```
进程启动 / transform 首次运行:
  texts ← session 消息扁平文本
  taskOpt ← inferReviewTaskFromTexts texts
  syncReviewProjection reviewStore sessionID taskOpt

用户 /loop task:
  activateReview(sessionID, task)   // 内存
  注入 buildLoopMessage           // 历史 front-matter

重启后:
  内存 ReviewStore 空
  transform 再次 syncReviewProjection → 与历史一致
```

---

## 附录 N：Fable / JS 互操作纪律

- 宿主上下文：`obj` + `Dyn` 仅在 Shell；`emitJsExpr` 仅边界必要处（如 OMP `WeakSet`）
- 导出：Mux/OpenCode `[<ExportDefault>]`；OMP `wanxiangshuExtension`
- Promise：Fable `promise { }` 编译为 JS Promise；禁 F# Async 跨边界

---

## 附录 O：与 README 差异说明（PRD 权威点）

| 主题 | README | PRD（源码计数） |
|------|--------|-----------------|
| methodology 数量 | 53 | Registry **54** |
| ToolCatalog 工具数 | 未逐条列 | **17** 规范名（含 executor_wait/abort） |
| 第五宿主 | OMP | 同，且架构测试最严 |

---

## 附录 P：万象阵 slave prompt 最小片段（复用万象术）

当万象阵 coordinator 检测 `SQUAD_VIBEFS=1` 时，可在 `--prompt` 中加入：

```
万象术 /loop（With-Review Mode）可用。
完成开发并 commit 后调用 submit_to_squad 之前：
  若使用 /loop：须 submit_review 至 PASS，再提交 coordinator。
```

字段级协同：loop 消息须含 `task:` front-matter（`LoopMessages.taskField`），与万象术重放一致。

---

## 附录 Q：测试命令与环境

```bash
dotnet tool restore          # Fable
npm install
npm run build-and-test
```

单测入口无细分脚本；全量 `runAll` 为唯一门禁（与 README 一致）。

---

## 附录 R：ReviewResult 与 LoopDecision 类型

`ReviewSession/Types.fs`：

- `ReviewResult` = `Accepted` | `Rejected of feedback` | `Terminated`
- `LoopDecision` = `Finish of ReviewResult` | `Nudge of nudgeCount`

`PromptFailed` 与 `NoResult` 为 round outcome（同文件 `RoundOutcome`），驱动 `decideAfterRound`。

---

## 附录 S：MessageTransformPlan 字段

`MessageTransformPipeline.fs:8-14`：

- `SessionID`, `Agent`, `Directory`, `Excluded`（是否排除投影）
- `Cleaned`：`Message<obj> list` 经宿主预清理后输入

---

## 附录 T：未来 opencode 语义变更风险

| 风险 | 缓解 |
|------|------|
| compaction 若未来删除存储消息 | 今日重放靠 `session.messages` 全量；可增 `.vibe/backlog.json` 备份（未做） |
| transform 仅切片 | review 重放不用 transform 切片为 SSOT |
| 插件 hook 更名 | 宿主适配目录隔离改动 |

---

*文档版本：与仓库 `wanxiangshu-1` 源码同步编写；章节 §0–§37 + 附录 A–T。行数对标 `wanxiangzhen/PRD.md` 规格粒度，以 `src/` 与 `tests/` 为验收真源。*