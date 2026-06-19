# vibe-fs 架构宪法 v2

> 这不是建议清单，是合同。落地到代码上的每一处偏离都要在评审里被记录、修复，或显式签收。
> 上一版『克制版』已完成；它做的是清扫，没动骨架。本版要把骨架捏到 Kolmogorov 极小：每一行托着真实概念，每一个模块对应不可消的领域事实，每一处抽象都付得起它的篇幅。

---

## 0. 北极星：什么叫"最小描述"

代码库的总长度只该等于：

```
|workspace| = |本质领域知识| + |外部 SDK 抽税| + |不可避免的胶水|
```

任何一行不能归到这三类的，都是债：

- **本质领域知识** = review 状态机、nudge 决策、magic-todo 折叠、subagent 意图、syntax check、fuzzy 搜索协议。
- **外部 SDK 抽税** = 让 Fable / Node / opencode-plugin SDK / Mux SDK 能链上的最少接口。
- **不可避免的胶水** = 进程边界、JSON / Zod / JSON Schema 互转、AbortSignal 翻译、文件系统 IO。

**单一事实来源 (SSOT) 法则**：每一个概念在代码库里有且仅有一处定义。两个文件长得像，是巧合；两个文件描述同一个事实，是 bug。

判断 SSOT 是否被破坏的三个反例：

1. 同一个 prompt 主体在 `Opencode/Tools.fs` 和 `Mux/SubagentTools.fs` 都能找到 → 已修。
2. 同一个工具的描述文案、字段含义在两份 schema builder 里各写一遍 → 部分修，仍存在零碎拷贝。
3. 同一个状态可以从 `tool.execute.before` 写也能从 `event` 写、还能从 `messagesTransform` 间接观察到，谁先到看顺序 → **当前最大债务，本版的核心打击对象**。

---

## 1. 现状诊断（v1 收尾后）

v1 完成项（保留）：

- `Kernel/Subagent.fs` 把任务 → prompt 收敛成单点。
- `Kernel/ToolCatalog.fs` 持有所有工具的 description / paramDocs。
- `Opencode/Hooks.fs` 拆为 `HookExecute` / `HookTransform`（旧文件留为空 stub，**本版直接删除**）。
- `Opencode/Session.fs` 拆为 `SessionIo` / `ReviewerLoop`（同样删 stub）。
- `MagicSession` 收编 `completedWorkReportByCall`。
- `Shell/FuzzySearch.fs` 内部 `TypedIteratorStore` 强类型化。

剩余债务，按"违反 SSOT 程度"排序：

| # | 违反 | 证据 | 深度 |
| - | --- | --- | --- |
| D1 | 双 host 仍各持一份 *工具装配* 代码 | `Opencode/Tools.fs` (330) ≈ `Mux/SubagentTools.fs` (218) + `Mux/HostTools.fs` (246) 同构 | 高 |
| D2 | 双 host 各持一份 *Schema 编译器*（Zod vs JSON Schema），但所有字段已经在 `ToolCatalog` 集中 | `Opencode/ToolSchema.fs` 里的 `coderIntentsSchema` 仍硬编码字段；`SubagentIntents.fs` 里又有 `muxCoderIntentsSchema` | 高 |
| D3 | 状态副作用四处长 | `MagicSession` 里 `Dictionary<string,string>`、`reportTables` 静态字典、`MagicTodo.shared`、`FinderCache`、`callStore`、`ReviewStore` 内部 `mutable registry / effects` | 高 |
| D4 | `Hooks.fs` / `Session.fs` 仍以空文件存在；`Plugin.fs` 内仍残留无用 `getPluginToolPolicy` / `deduplicateReadOutputsXXX` 兼容门面 | `Mux/Plugin.fs:109-131` | 中 |
| D5 | `Dyn.fs` 是项目里最危险的"任意 obj"出口；目前还在被 hooks 直接调 `setKey` / `replaceArrayInPlace` 等原地写 | `HookTransform.fs:27 replaceArrayInPlace` | 中 |
| D6 | 错误处理三套（`raise exn`、`Result<_,string>`、字符串前缀如 `"Error: ..."`、JSON-stringified `{ success=false; error }`），调用方各猜各的 | `Mux/HostTools.fs websearchTool`、`Opencode/Tools.fs websearchTool`、`Shell/FileSys.fs write` | 中 |
| D7 | Mimocode 任务参数清洗（`stripMimocodeTaskArgsForExecute` + `restoreMimocodeTaskArgsAfterExecute`）依赖 `callID` 静态字典做跨钩子隐式通信 | `MagicTodo.fs:18 reportTables` | 中 |
| D8 | `Config.fs::canUseCanonical` 把权限策略写成嵌套子串匹配的 if-else 瀑布；可读但拒绝增长 | `Kernel/Config.fs:16-30` | 低 |
| D9 | `Prompts.fs` 同时承担 prompt body、reviewer instructions、UI 文案、search/fetch 格式化等四类内容，按角色未拆 | 单文件 236 行 | 低 |
| D10 | `Opencode/Plugin.fs` / `PluginMimo.fs` / `PluginMimoTui.fs` 是三个仅入口形状不同的薄壳 | 三文件总共 < 130 行，但概念散落 | 低 |

---

## 2. 目标架构：三层契约

```
┌─────────────────────────────────────────────────────────────┐
│  Kernel       纯归约：领域事实 + 代数数据类型 + 纯函数      │
│  ──────────                                                 │
│  Domain / ReviewSession / Nudge / NudgeState / MagicCore /  │
│  MagicProjection / MagicTodo / SubagentIntents / Subagent / │
│  ToolCatalog / Executor / Fuzzy / Prompts / TreeSitterKernel│
│  CapsFormat / Dedup / MessageDedup / Message / HostTools    │
│  Config（权限规则）                                         │
├─────────────────────────────────────────────────────────────┤
│  Shell        外壳：所有 IO、所有 mutable 状态容器、所有   │
│  ──────────   与 Node/进程/网络/文件系统/SDK 的对话        │
│  FileSys / Executor / ExecutorJavascript / TreeSitterShell /│
│  FuzzyFinderShell / FuzzySearch / OllamaClient /            │
│  ReviewRuntime / WorkspaceFiles                             │
├─────────────────────────────────────────────────────────────┤
│  Hosts        每个 host 只剩三件事：                        │
│  ──────────   1. 翻译外部 SDK 形状 ↔ Kernel 数据类型       │
│               2. 注册 hook / tool / mcp，转发到 Kernel     │
│               3. 进程入口 (ExportDefault)                   │
│                                                             │
│  Hosts/Common  ── 共用宿主装配：toolBindings / hookBindings │
│  Hosts/Opencode ── opencode SDK 形状 + Zod schema 投影      │
│  Hosts/Mux      ── Mux SDK 形状 + JSON Schema 投影          │
└─────────────────────────────────────────────────────────────┘
```

铁律：

- **Kernel 不出现 `obj`**。任何 `obj` 都必须在 Shell 或 Host 边界翻译为代数数据类型。当前 `Dyn.fs` 是 Kernel 模块，违法 → 移到 `Shell/Dyn.fs` 并改名 `Shell.JsBridge`，让"我在和 JS 玩 duck typing"这件事写在路径上。
- **Kernel 不出现 `mutable` / `Dictionary` / `ref`**。状态聚合必须以 `state -> command -> state * event list` 的归约形式表达。当前 `Nudge.NudgeCoordinator` 类有 `mutable state` → 拆成 `update : State -> Cmd -> State * Effect`，由 Shell 持有引用。
- **Kernel 不出现 `JS.Promise`**。Promise 是 Node 的事；Kernel 用 `Async` 或纯返回值。
- **Host 不出现业务判断**。"什么时候 nudge"不是 host 的事；host 只把外部事件翻译成 `NudgeState.Cmd`。

---

## 3. 删除清单（Kill List）

凡是在以下清单上的代码，必须在 v2 完工前消失。无人问津的 Public、契约、影响面大都不是借口；下游通过 git 通知。

### 3.1 立即删除（一行不留）

- `src/Opencode/Hooks.fs`（空 stub，6 行）。
- `src/Opencode/Session.fs`（空 stub，5 行）。
- `Mux/Plugin.fs:109-131` 的 `getPluginToolPolicy` / `deduplicateReadOutputsXXX` / `collectReadOutputs` 兼容门面。已经没人调用，删。
- `Kernel/Dyn.fs::ArgsPatch` + `applyPatch` —— v1 引入但调用点几乎为零；跨钩子 args 重写应通过 Kernel 的 `ArgsCommand` 类型表达，而不是把"我要 mutate 这个 obj"封装一层。如果 v2 落地后还需要这条路径，再以代数数据类型形式重新引入。

### 3.2 收编后删除（合并到新 SSOT）

- `Mux/SubagentTools.fs::buildLoopMessage` —— 与 `Opencode/PluginCore.fs::buildLoopMessage` 是同一段拼接，搬进 `Kernel/LoopMessages.fs`。
- `Mux/SubagentTools.fs::reviewVerdictInstructions` 与 `Mux/SlashCommands.fs::loopReviewVerdictInstructions` —— 两段几乎逐字相同的 reviewer 指令模板。搬进 `Kernel/Prompts.fs::ReviewerVerdictPrompts` 模块。
- `Opencode/Tools.fs::formatReviewResult` —— 与 `Mux/SubagentTools.fs` 中 review verdict 文案是兄弟。搬到 `Kernel/Prompts.fs`。
- `Mux/Wrappers.fs::createAllWrappers*` 的 `mkWebOverride` —— 让 Mux 的 web_search / web_fetch 直接调用 Kernel `Subagent` 入口；不需要先注册一份 `websearch` / `webfetch` 工具又通过 wrapper 重映射到 host 的 `web_search` / `web_fetch`。
- `Opencode/PluginMimo.fs` / `Opencode/PluginMimoTui.fs` 与 `Opencode/Plugin.fs` —— 收成 `Hosts/Opencode/Entry.fs` 一个文件，三个 `[<ExportDefault>]` 入口同源。

### 3.3 禁止再生

- 任何形如 `match host with | Opencode -> ... | Mimocode -> ...` 的分支，**只能在 `Hosts/*` 目录里出现，且每个分支不超过 5 行**。Kernel 内禁止出现 `Host` 模式匹配（除了 `HostTools.fs` 这种 enum 定义本身）。
- 任何形如 `if Dyn.isNullish ... then ... else ...` 的链条 **只能在 Shell 边界出现一次**，目的是把 obj 翻译成 record / DU。Kernel 内见到 `Dyn.*` 调用 = bug。
- 任何 `mutable` 字段 / `Dictionary` / `ref` 在 Kernel 模块里出现 = bug。Shell 模块允许，但必须封在 `internal` actor 后面，外部只能 `Async`-post 命令。
- 任何 `failwith` / `raise exn` 用于业务可预见失败 = bug。业务可预见失败必须是返回类型里的 DU 分支。`exn` 只留给"程序无法继续"。
- 任何字符串前缀错误（`"Error: ..."`, `"Failed: ..."`, `"(no output)"`） = bug。错误是数据，不是文本。

---

## 4. 目标模块重组

```
src/
├── Kernel/
│   ├── Domain.fs                 ── ID 类型、DomainError、ChildSession 状态机 (现有，保留)
│   ├── HostTools.fs              ── Host enum + 工具名归一 (保留)
│   ├── Config.fs                 ── 权限规则。改造：从子串瀑布 → 显式 (Agent, ToolKind) 表
│   ├── ToolCatalog.fs            ── 工具元数据 SSOT (保留，扩展为 ToolSchemaSpec：types, requireds, enums)
│   ├── SubagentIntents.fs        ── intent 解析 → DU (现有)。删除其中的 muxXxxSchema：schema 改由 ToolCatalog 投影
│   ├── Subagent.fs               ── SubagentTaskKind + formatPrompt + joinReports (现有)
│   ├── Prompts.fs                ── 拆为目录：见 §4.1
│   ├── ReviewSession.fs          ── 状态机 + 注册表 + LoopDecision (保留)
│   ├── Nudge.fs / NudgeState.fs  ── 合并：移除 NudgeCoordinator class，统一为 (State, Cmd) -> (State, Effect)
│   ├── MagicCore.fs / MagicProjection.fs / MagicTodo.fs ── 保留
│   ├── Message.fs / MessageDedup.fs / Dedup.fs / CapsFormat.fs / TreeSitterKernel.fs / Fuzzy.fs / Executor.fs ── 保留
│   ├── LoopMessages.fs           ── (新) buildLoopMessage / loopFooter SSOT
│   ├── ToolPolicy.fs             ── (新) Config 输出，给 host 投影 add/remove 列表的纯函数
│   └── Hooks.fs                  ── (新) HookCommand / HookEvent DU；hook 平台通用契约
├── Shell/
│   ├── JsBridge.fs               ── (旧 Kernel/Dyn.fs 整体迁入) 仅在 Shell 内可见
│   ├── FileSys.fs / Executor.fs / ExecutorJavascript.fs / TreeSitterShell.fs ── 保留
│   ├── FuzzyFinderShell.fs / FuzzySearch.fs ── 保留
│   ├── OllamaClient.fs / WorkspaceFiles.fs ── 保留
│   ├── ReviewRuntime.fs          ── 保留 (Kernel 状态机 → Shell 可变容器的唯一桥)
│   ├── NudgeRuntime.fs           ── (新) 同样的桥：内核 NudgeState → Shell 持有
│   ├── CallStore.fs              ── 从 Mux/CallStore.fs 上移到 Shell；不是 Mux 专属
│   ├── MagicSessionStore.fs      ── (新) 收编 MagicSession 内部所有 mutable Dictionary
│   └── ChildAgentRegistry.fs     ── 从 Opencode/Actors.fs 上移到 Shell
├── Hosts/
│   ├── Common/
│   │   ├── ToolBindings.fs       ── (新) 把 ToolCatalog 编译为 host 无关的 ToolBinding<'schema>
│   │   ├── HookBindings.fs       ── (新) Hook 平台通用契约 -> 三个 hook handler 抽象
│   │   ├── SubagentRunner.fs     ── (新) SubagentRunner 接口 + 默认 timeout / retry 策略
│   │   └── SchemaProjection.fs   ── (新) ToolSpec -> SchemaIR 抽象 (字符串、整型、枚举、对象、数组)
│   ├── Opencode/
│   │   ├── Entry.fs              ── 合并 Plugin.fs / PluginMimo.fs / PluginMimoTui.fs
│   │   ├── ZodCompile.fs         ── 旧 ToolSchema.fs：只剩 SchemaIR -> Zod 投影
│   │   ├── HookExecute.fs / HookTransform.fs ── 改名 Hooks*.fs 后保留；只做 obj↔Kernel 翻译
│   │   ├── SessionIo.fs / ReviewerLoop.fs ── 保留 (但调用 SubagentRunner 抽象)
│   │   ├── NudgeBridge.fs        ── 旧 NudgeHook.fs：只翻译事件，决策回 Kernel
│   │   ├── MagicTodoBridge.fs    ── 旧 Opencode/MagicTodo.fs：仅 host 侧引用 Shell.MagicSessionStore
│   │   └── Tools.fs              ── 只剩工具名 → SubagentRunner 调用，约 80 行上限
│   └── Mux/
│       ├── Entry.fs              ── 合并 Plugin.fs / SlashCommands.fs 入口
│       ├── JsonSchemaCompile.fs  ── 旧 Wrappers.fs schema 工厂 + Mux JSON Schema 投影
│       ├── Hooks.fs              ── EventHook + 任何 chat/messages 翻译；调用 Kernel.Hooks
│       ├── SubagentRunner.fs     ── 旧 Delegate.fs 实现 SubagentRunner 接口
│       ├── AiSettings.fs         ── 保留
│       └── Tools.fs              ── 只剩 schema 注册 + SubagentRunner 调用
```

文件总数不是 KPI。**不是删了多少行、拆了多少文件的胜利**，而是『同一概念的描述只剩一份』的胜利。

### 4.1 Prompts 目录化

`Kernel/Prompts.fs` 是否拆分，取决于它是否继续承担多个彼此独立的知识块；不是因为它超过若干行就机械拆。

优先方案：

- 先保留单文件。
- 先把 prompt 里的重复语义、重复字符串、无关职责清掉。
- 只有当 reviewer / subagent / search-formatting 三类知识持续共同演化、导致修改原因长期不同步时，才拆。

若未来确认必须拆，建议上限为以下粒度，不再继续细切：

```
Kernel/Prompts/
  Common.fs        ── readOnlyRules / reviewCriteria / 公用片段
  Subagent.fs      ── coder / investigator / meditator / browser / executor / websearch
  Reviewer.fs      ── reviewInstructions / reviewerNudgePrompt / agentReportReviewInstructions / 两版 verdictInstructions SSOT
  Manager.fs       ── managerSystemPromptFor + 各角色 hint
  Search.fs        ── SearchResult / FetchResponse + formatXxx
  Nudges.fs        ── todoNudgePrompt / loopNudgePrompt / meditatorNudge
```

这不是默认动作，只是确认职责分裂后的最大拆分方案。理由不是“每文件 ≤ 80 行”，而是 prompt 文案本身是 SSOT 最敏感的地方，一旦不同知识块开始独立演化，就不该继续共居。

---

## 5. 状态封装：把"什么时候改"写进类型

当前所有 mutable 状态的容器列表：

| 容器 | 当前位置 | v2 归属 | 命令集 |
| --- | --- | --- | --- |
| `ChildAgentRegistry` | `Opencode/Actors.fs` | `Shell/ChildAgentRegistry.fs` | `Register / Unregister / Lookup / ResolveParent` |
| `ExecutorActor` | `Opencode/Actors.fs` | `Shell/Executor.fs` 内 actor 子模块 | `Submit (sessionID, work)` |
| `FinderCache` | `Shell/FuzzyFinderShell.fs` | 保留位置 | `Get / Destroy / DestroyAll` |
| `CallStore` | `Mux/CallStore.fs` | `Shell/CallStore.fs` (host 无关) | `Register / Resolve / Reject(timeout)` |
| `ReviewStore` | `Shell/ReviewRuntime.fs` | 保留位置 | 当前接口已正确，不动 |
| `MagicSession` 内部 `Dictionary<string,_>` | `Opencode/MagicTodo.fs` | `Shell/MagicSessionStore.fs` | `CaptureReport / TakeReport / GetOrRebuildBacklog` |
| `NudgeCoordinator.mutable state` | `Kernel/Nudge.fs` | 删除 class；改 `Shell/NudgeRuntime.fs` 持有 `Kernel.NudgeState` | `Submit (sessionId, ctx) -> action` |
| `EventHook` 内 `HashSet<string>` 三件套 | `Mux/EventHook.fs` | 全部表达为 `Kernel.NudgeState`，hook 仅翻译事件 | — |
| `registeredToolNames` 模块全局可变 | `Mux/Wrappers.fs` | 删除；改成函数参数显式传 | — |

每一个容器在 Shell 里都必须满足三条：

1. 类型构造私有：外部拿不到 `Dictionary` / `ref`，只能拿 actor 接口或 `interface`。
2. 命令必须是封闭 DU 或显式接口方法；不允许 `member _.set(key,value)` 这种逃生口。
3. 任何"读后写"必须是单一方法，不允许把 read + decide + write 暴露给上层让上层手攒。

`NudgeCoordinator` 的当前形态特别糟：它一边持有 mutable 状态，一边还把 `update : State -> ... -> State * Action` 暴露成 module level 函数。两个 API 描述同一个状态机，但 class 内部又重复 `state` 与 `update`。v2 把 class 删掉，留 module。

---

## 6. 错误处理：一类 IO，一类 业务

定义两层：

```fsharp
// Kernel/Domain.fs (扩展)
type DomainError =
    | MessageAborted
    | SessionBusy
    | TaskWaitBackgrounded
    | ExecutorExecutableMissing of executable: string
    | ParseError of context: string * detail: string
    | ToolNotPermitted of agent: string * tool: string
    | InvalidIntent of tool: string * field: string * detail: string
    | UpstreamTimeout of seconds: int
    | UpstreamRefused of reason: string
    | SystemPanic of message: string
    | UnknownJsError of message: string
```

铁律：

- **业务可预见失败**（参数缺失、权限不足、上游超时、找不到、并发冲突）→ 返回类型必须是 `Result<'T, DomainError>` 或 `Async<Result<'T, DomainError>>`。
- **程序无法继续**（assertion 失败、NRE、SDK 返回了文档不允许的形状）→ `raise`。Shell 边界统一翻译为 `SystemPanic`。
- **字符串拼接错误信息**只在最末端出现一次，由 `Hosts/*` 决定 user-visible 文案，Kernel/Shell 内部一律传 DU。
- 删除所有 `try ... with _ -> "string"` 兜底。每一个 `_` 异常吞掉的地方都贴近一个 v1 没修干净的根因，v2 必须查清楚是哪类，再决定让它变成 DU 分支或者真的 `raise`。

宽容 SDK 形状的代码（如 OllamaClient 解析 fetch 失败）必须显式：

```fsharp
let ollamaPost ... : Async<Result<obj, DomainError>>
```

调用方拿到 `Error UpstreamRefused` 自然写出『搜索失败时返回原始结果摘要』，不用再发明 `try ... with ex -> jsonStringify {|success=false; error=ex.Message|}`。

---

## 7. 并发协议

JS 单线程 + 多 Promise 并发是事实，不是借口。v2 强制：

- **每条 hook 必须 5ms 内同步返回或 yield**。任何重活（subagent prompt、文件 IO、SDK 等待）必须 `Async.StartImmediate` / `Async.StartAsPromise`，不能同步 `await` 在 hook 主流。`NudgeHook.runNudgeFlow` 已经做对，作为模板。
- **跨调用共享状态必须经 actor 或 lock-free 命令模式**。当前 `NudgeCoordinator` / `ReviewStore` / `CallStore` 三家用三种风格，v2 统一为：所有"读改写"通过单一 `Mutate` 函数，函数签名 `state -> command -> state * effect`。Effect 是 DU；上层在锁外执行。
- **AbortSignal 必须穿透每一层**。当前 `Mux/Delegate.fs` 与 `Opencode/SessionIo.fs` 各写一遍 promiseRace。抽到 `Hosts/Common/AbortRace.fs`。
- **禁止 setTimeout 直接出现在 Kernel/Shell 业务逻辑里**。CallStore 的 TTL 是合法用例，但只在 `Shell/CallStore.fs` 内；任何业务文件 grep 命中 `JS.setTimeout` = bug。

---

## 8. 持久化与事件溯源

vibe-fs 没有显式磁盘日志（review/nudge/magic-todo 都是内存状态机），但**对话本身就是事件流**。两条要求：

- **事件先于内存**：处理完一条 `tool.execute.after` 必须先把 backlog 报告捕获写入 Shell 的 `MagicSessionStore`，再让 hook 去做后续的 syntax check / nudge dispatch。如果中间崩溃，重启从 messages 流重放即可；当前已基本如此，v2 只补一条单测保证 `captureCompletedWorkReport → takeCompletedWorkReport` 路径在 Mimocode 严格 schema 下不丢。
- **不可改写**：projection 输出（`projectMagicFor`）永远从 messages 全量重算，绝不缓存可被外部修改的中间态。当前 `MagicSession.GetOrRebuildBacklog` 缓存了 `BacklogEntry list`，但只在 messages.Length = 0 时取缓存 —— 这是 OK 的『书签』，缓存上必须有完整指纹（messages 末尾消息 id + count），v2 加上。

---

## 9. 类型化边界

凡是字符串穿过两个模块，必须升级为命名类型：

- `SessionId` / `WorkspaceId` / `AgentId` / `ToolId` / `CallId` / `ChildId` —— 已经在 `Kernel/Domain.fs::Id`。**v2 强制全代码库使用**，禁止裸 `string` 表示这些概念。检查方式：grep `: string` 出现在签名里且参数名为 `sessionID` / `workspaceId` / `agent` / `tool` / `callID` 的一律改类型。
- `RawHostEvent` —— 当前 hook 全部以 `obj` 流转，把 `event.type` / `properties` 各自 `Dyn.str` 是危险游戏。v2 在 `Kernel/Hooks.fs` 定义：

```fsharp
type RawHostEvent =
    | StreamEnd of sessionId: SessionId * lastAssistant: string * stopReason: string
    | StreamAbort of sessionId: SessionId
    | MessageUpdated of sessionId: SessionId * info: AssistantInfo
    | MessagePartUpdated of sessionId: SessionId * part: PartInfo
    | SessionIdle of sessionId: SessionId
    | SessionError of sessionId: SessionId * error: DomainError
    | SessionBusy of sessionId: SessionId
    | StepFailed of sessionId: SessionId * error: DomainError
    | ToolFailed of sessionId: SessionId * error: DomainError
    | StepEnded of sessionId: SessionId * finish: FinishReason
    | Other
```

Kernel 拿到这种类型决策；host 仅做翻译。`dispatchEventState` 的 13 路 string match 立刻转成对 DU 的 match，编译器给"漏分支"上保险。

---

## 10. 路线图（六阶段，每阶段必须 `pnpm build && pnpm test` 全绿）

| 阶段 | 主题 | 范围 | 退出条件 |
| --- | --- | --- | --- |
| **S0** | 删 stub + 死代码 | 删 `Hooks.fs`/`Session.fs` 空 stub；删 `Mux/Plugin.fs` 兼容门面；删 `Dyn.ArgsPatch` | grep 命中 0；`vibe-fs.fsproj` 干净 |
| **S1** | Prompts 目录化 + LoopMessages SSOT | §4.1 拆分；移 `buildLoopMessage` / verdict instructions | `Kernel/Prompts/*.fs` ≤80 行；两侧 host 同源调用 |
| **S2** | 错误统一为 DomainError | §6 全量；删除所有 `try _ -> "string"` 与 `"Error:"` 字符串前缀 | 全代码库 `failwith` 仅在 `ToolCatalog.specOf` 这种程序错误处；`Result<_, DomainError>` 出现在所有外部 IO 入口 |
| **S3** | 状态封装与去 mutable | §5 表；删 `NudgeCoordinator` class；`Mux/EventHook` 内 HashSet 全部上升为 `NudgeState` | Kernel grep `mutable\|Dictionary\|ref ` 命中 0；Shell 容器全部 `internal` |
| **S4** | 双 host 收编 | §4 目录重组；`Hosts/Common` 抽出 ToolBindings / SubagentRunner / SchemaProjection；删 `Mux/Wrappers.fs` 的 `mkWebOverride` | `Opencode/Tools.fs` ≤120 行；`Mux/Tools.fs` ≤120 行；同一个工具的 schema 字段在代码里只有一份 |
| **S5** | 类型化边界 + RawHostEvent | §9；`Hooks.fs` 引入 DU；event hook 翻译层之外不再出现 `Dyn.str event "type"` | Kernel 任何函数签名内 `obj` 出现次数 = 0；hook 决策路径有完整 match exhaustiveness |

每阶段独立 PR；任何阶段无法在 2 个工作日内完成意味着拆分错了，回溯重切。

---

## 11. 验收预算

预算是预警器，不是机械切片器。任何超出都必须回答“是否混入了多个独立知识点”；如果答案是否定的，则可以保留，不为了数字好看继续拆。

| 维度 | 上限 | 当前最大违反者 |
| --- | --- | --- |
| 单文件行数 | 260 预警，300 以上才默认要求解释职责是否混杂 | `Opencode/Tools.fs` 330；`MessageDedup.fs` 269；`NudgeState.fs` 252；`SubagentIntents.fs` 182 |
| 单函数行数 | 45 预警，60 以上才默认要求解释是否包含多个规则块 | `MessageDedup.deduplicateModelReadOutputsWithSeen` 70+；`Hooks.runNudgeFlow` ≈30（OK） |
| 模块圈复杂度 | 10 | `dispatchEventState` 13 路 → §9 后归零 |
| Kernel 内 `obj` 出现次数 | 5（仅在 `MagicCore` / `Message` 这种 SDK 边界 helper）| 当前满地都是，S5 出口归零 |
| Kernel 内 `Dictionary` / `mutable` / `ref` | 0 | 当前 `Nudge.NudgeCoordinator` / `MagicSession`（实质 Shell）违例 |
| 同一字符串字面量在两个文件 | 0 | `loopFooter`、verdict instructions、`(no output)` 三处 |
| 公开 API 中 `string` 表示 SessionId/WorkspaceId/CallId/AgentId/ToolId | 0 | 当前 host 层大量裸 `string` |
| 工具 description 文案出现在 `ToolCatalog` 之外 | 0 | 当前 `Opencode/ToolSchema.fs` `description` 直接 import 自 `ToolCatalog` (OK)，但 Mux 侧 `coder = description "coder"` 正好；新增工具时容易破窗 |
| `match host with` 在 Kernel 模块出现 | 0 | 当前 `MagicCore.fs:isTodoResultFor` / `MagicProjection.fs` 多处 → 改成 host 注入闭包 |

---

## 12. 反目标（明确不做的事）

- **不发明自研 DI 容器**。F# 模块 + 显式参数足够，加 IoC 是给自己挖坑。
- **不抽 `IFooBar` 把每个状态容器变 interface 给假"测试用"**。Shell 层用具体类型 + 内部 actor，单测在 Kernel 已经无副作用，不需要 mock。`HostReadExec = obj option ref` 这种伪接口必须删。
- **不重写领域语义**（review 状态机、nudge 决策、magic-todo 折叠、subagent intent 解析）。这些是项目核心知识，已经写得对；本宪法只整顿外壳与边界。
- **不引入新的运行时**。不上 Effect / Eff、不上 Reader monad transformer 这种偏离 F# 默认风格的玩具。需要解耦就用函数参数 + record。
- **不为"未来可能多 host"预留扩展点**。两个 host (opencode + mux) 是事实；扩展第三个 host 时再抽，不提前。
- **不写 facade / wrapper / shim 来"避免影响下游"**。下游就是这两个 host 自己；改名直接改，编译器报错 = 提示。
- **不为了预算机械拆文件、拆函数**。一个 220 行但语义单一的文件，胜过 4 个来回跳转的空壳文件；一个 55 行但顺读的纯解释器，胜过 6 个只有包装价值的小函数。
- **不接受 `// TODO`**。要么实现，要么删掉；TODO 就是债务伪装成谦虚。
- **不接受 fallback / default-value / 兜底**。任何 fallback 都是把 bug 推迟到看不见的地方。看到 `defaultArg ... ""`、`if isNull then "" else ...` 用于业务字段都要给原因或改成 `Result.Error InvalidIntent`。

---

## 13. 文档自身的 SSOT

本文件是项目唯一的架构权威。下列文件应消失或合并：

- 任何写在源码注释里的"架构说明"超过 5 行的，应当抽到本文件对应章节，注释里只留 `// see REFACTOR.md §N`。
- 任何 `docs/architecture-*.md` —— 本仓库目前没有，**永远不许新建**。
- AGENTS.md / 子模块 AGENTS.md —— 那是开发协议，不是架构文档；不重叠。

本文件被 v3、v4 取代时，是因为目标更狠，不是因为目标更糟。

---

## 14. 总结

vibe-fs 的内核（review / nudge / magic todo / subagent intents / fuzzy / executor / tree-sitter）已经是事件驱动 + 纯归约的好结构。残余复杂度全部分布在 **JS 边界、状态封装、错误风格、双 host 重复** 这四个外圈。

v2 的工作不是『再清理一遍』，是**把所有不属于本质领域的 mutable 状态、字符串错误、obj 流转、host 分支，从 Kernel 推到 Shell；从 Shell 推到 Hosts/* 边界；在 Hosts/* 边界统一塌缩到一份 SSOT**。这个过程可以伴随少量必要拆分，但拆分永远服务于语义聚焦，不服务于数字洁癖。完成后：

- Kernel 是一坨纯函数 + 代数数据类型，可以脱离 opencode-plugin / Mux SDK 单独读懂、单独测试。
- Shell 是有限几个 actor + 翻译器，每一个都对应一项真实 IO。
- Hosts 是薄到手心一摞的 SDK 形状适配器，三百行内能写完一种新 host。

读者沿任何一个概念边界往下追都不会撞到第二份描述、不会撞到 fallback、不会撞到 try-catch 黑洞、不会撞到 `obj?.foo?.bar?.baz`。这是 Kolmogorov 级别的 codebase 的样子。
