# 04 — Shell 副作用与边界层

## 职责

Shell（约 **139** 个 `.fs`）是 Kernel 与 Node/宿主之间的**唯一**常规 IO 通道：文件、锁、子进程、网络、MCP、动态对象编解码、运行时队列、review/nudge/eventlog 运行时、subsession actor 运行时、fallback 运行时。

## 能力分簇

| 分簇 | 代表模块 | 说明 |
| :--- | :--- | :--- |
| 文件系统 | `FileSys`、`WorkspaceFiles` | 读写工作区 |
| 事件日志 | `EventLogCodec`、`EventLogIo`、`EventLogFiles`、`EventLogRuntime*`、`EventLogSquadProjection` | NDJSON（含万象阵行、subsession 行）、锁、缓存、sync |
| 搜索 | `FuzzyFinderShell`、`FuzzySearch/*`、`FuzzyIteratorStore` | fff 后端、iterator 状态 |
| Semble | `SembleMcp`、`SembleSearch`、`SembleSearchClient`、`SembleSearchTypes` | MCP 客户端与 investigator 断点注入 |
| 执行器 | `Executor*`、`SessionExecutor` | shell/python/js、spawn、会话级执行 |
| Tree-sitter | `TreeSitterShell`、`TreeSitterPlatform` | 可选语法能力 |
| 网络 | `WebSearchApi`、`WebFetch`、`WebSearchCodec`、`TitleFetchGuardCommon` | 搜索与抓取守卫 |
| 动态类型 | `Dyn`、`DynField`、`ErrorClassify`、`JsArrayMutate`、`PromiseStr` | `obj` 安全访问 |
| 并发 | `PromiseQueue`、`SerialStateHolder`、`LivelockGuard`、`CoordinatorLifecycle` | 串行队列与守卫 |
| 时钟 | `Clock` | 可注入时间（测试） |
| 子代理分发 | `SubagentDispatcher`、`ChildAgentRegistry`、`SubagentSpawn`、`SubagentIo`、`SubagentPromptBuild`、`SubagentIntentsCodec`、`SubagentSimpleArgsCodec`、`SubagentIteratorStore` | 统一委派 + 各宿主接线 |
| **Subsession Actor** | `SubsessionActor`、`SubsessionActorRegistry`、`SubsessionEventRouter`、`SubsessionEventStore`、`SubsessionEventWire`、`SubsessionReconcile`、`SubsessionService`、`SubsessionTranscript`、`SubsessionChildObserver` | 子会话轻量 Actor 消息泵 |
| Review/Nudge | `ReviewRuntime`、`ReviewReplaySync`、`ReviewToolsCodec`、`NudgeRuntime`、`NudgeRuntimeTypes`、`NudgeRuntimeMux` | 投影同步与异步 nudge |
| Fallback | `FallbackConfigCodec`、`FallbackRuntimeState`、`FallbackRuntimeStateGates`、`FallbackMessageCodec`、`FallbackMessageParser`、`FallbackEventBridge`、`FallbackRecoveryWait`、`FallbackGateObservation` | 降级运行时（续命租约、门闩、事件桥） |
| Context budget | `ContextBudgetStore`、`ContextBudgetUsageCodec`、`ContextBudgetLimitResolver`、`ContextBudgetResolve` | 用量、触发、从 session/model 解析 `maxInputTokens` |
| Caps | `CapsFileCache`、`CapsSynthCommon`、`OmpCaps`、`CapsPrelude` | caps 文件缓存与组装 |
| MessageTransform | `MessageTransformPipeline`、`MessageTransformCore`、`MessageTransformHostEntry`、`MessageTransformHostHooks`、`MessageTransformCommon`、`Messaging*Codec`、`Chat*Codec`、`HostMessagePartCodec` | 共享管线 |
| Tool 编解码 | `ToolArgsDecode`、`ToolExecute`、`ToolRuntimeContext`、`ToolHookRuntime`、`*ToolsCodec`、`JsonSchemaBuilders`、`MuxJsonSchema`、`MuxPluginCatalogShell`、`MuxToolDefinition` | 参数解析与执行分发 |
| 宿主专用 codec | `Opencode*Codec`、`Mux*Codec`、`MuxHookInputCodec`、`MuxWorkspaceCodec`、`MuxAiSettingsCodec`、`MuxHostBindings` | hook 入参/出参 |
| OMP 绑定 | `OmpHostBindings` | 宿主 API 薄封装 |
| Subsession 宿主适配 | `Opencode/SubsessionHostAdapter.fs`、`Omp/SubsessionHostAdapter.fs` | `ISubsessionHost` 实现 |
| 万象阵 | `Shell/Wanxiangzhen/*`（`CoordinatorReplay`、`HttpServer`、`SquadEventLogRuntime`、`GitShell`、`SlaveSpawn`、`SlaveRuntime`、`SymlinkShell`、`SessionIo`、`PidMonitor`、`ConfigReader`、`CoordinatorRoutes`、`CoordinatorOps`、`CoordinatorLifecycle`、`CoordinatorSquadUpdate`、`CoordinatorBootstrap`、`CoordinatorDepsFactory`、`CoordinatorRuntime`、`EventCodec`、`SquadEventWanCodec`） | 协调器副作用；事件 append 走共用 `EventLogFiles` |

## EventLog 运行时链（写路径）

```text
业务 hook / 工具成功
  → EventLogRuntimeAppend.append*
  → EventLogRuntimeStore.appendAndCache
  → EventLogFiles.EventLogStore.AppendEvent
  → EventLogIo.appendLine (先锁后写)
  → foldWan 更新进程内 SessionState
```

原子多事件：`AppendEventsOrFail` 在一个锁内写所有行，用于 Subsession 决策信封。

读路径：`GetSessionState` / `ReadAllEvents` → `EventLogRuntimeSync` → `ReviewRuntime` / `ProjectionStore` / `FallbackRuntimeState` 重建。

文件名为 **`.wanxiangshu.ndjson`**，锁文件 **`.wanxiangshu.ndjson.lock`**（`EventLogCodec`）。

## Tool 执行路径（概念）

1. 宿主传入 `toolName` + `args`（`obj`）
2. `ToolArgsDecode` → 强类型参数
3. `Kernel` 校验（权限、业务规则）
4. `ToolExecute` / 子代理模块执行 Shell IO
5. 结果编码为宿主 `parts` / `output`

Opencode/Mux 经 `SubagentToolExecute` / `MuxSubagentToolExecute`；OMP 在 `Omp/` 内调用 Shell，但不 `open` Opencode/Mux。

## Subsession Actor 运行时链

```text
父工具（coder/investigator/browser/meditator）
  → SubagentDispatcher.dispatch
    → IHostAdapter.SpawnSubagent
      → SubsessionService.StartRun
        → SubsessionActorRegistry.GetOrCreate
          → SubsessionActor.BeginRun (原子: 注册 reply + 决策 + 事件持久化)
            → Kernel/Subsession/Decision.decide
              → 宿主 ISubsessionHost.Dispatch
                → session.prompt (含 model + nonce)
                  → 宿主回调: DispatchAccepted / DispatchRejected / TurnErrorObserved / SessionIdleObserved
                    → SubsessionActor.Post(Command)
                      → 再次决策 → 循环直至终局
```

详 [11-subagents.md](./11-subagents.md)。

## Fallback 运行时链

```text
宿主事件（session.error / session.idle / session.busy / message.updated）
  → 各宿主 FallbackHandler（Opencode/Mux/Omp）
    → FallbackEventBridge.handleEvent
      → IEventTranslator.TranslateError → FallbackEvent
      → Kernel.FallbackKernel.StateMachine.transition → (newState, action)
      → 若 action 为 SendContinue → 构造六阶段续命租约
        → appendContinuationRequested → TryTransitionPendingLease → executor.SendContinue
        → appendContinuationDispatched
      → 返回 FallbackHookResult { Consumed; State }
```

详 [12-fallback.md](./12-fallback.md)。

## MessageTransform 管线

`MessageTransformPipeline` 接收计划（session、agent、directory、scope、token 预算等），依次：

- 清理/过滤消息
- caps / backlog 投影注入
- Semble 注入（开关）
- parallel tool prompt（单工具伪装并行时的 SSOT 文案）
- context budget 阶段（见 [13-context-budget.md](./13-context-budget.md)）

宿主入口：

- `Opencode/MessageTransform.fs`
- `Mux/MessageTransform.fs`
- `Omp/MessageTransform.fs`

## RuntimeScope

每个会话/工作区上下文持有：

- Fuzzy iterator store
- Subagent iterator store
- SessionProjectionStore（backlog 缓存）
- Caps 文件缓存
- ContextBudget 状态
- 其他 scope 级可变状态（含 `fallbackRuntime` 引用）

**禁止**在 Shell 模块级复制第二份全局 store（架构测试）。

## Dyn 纪律

Kernel 不得 `open Dyn`。Shell codec 集中 `get`/`set`/数组变异；hook output 必须经 `OpencodeHookInputCodec` / `MuxHookInputCodec` 等，避免 Opencode 插件「换引用」导致 hook 失效（`AGENTS.md`）。

## 与 ../mux、../oh-my-pi 的改动范围

- **Mux**：优先改本仓库；必要时改 `../mux` **binding**，核心最小动。
- **OMP**：禁止改 `../oh-my-pi` 上游，仅参考；实现留在本仓库 `Omp/` + Shell。

## 相关文档

- [05-event-sourcing.md](./05-event-sourcing.md)
- [10-message-transform.md](./10-message-transform.md)
- [11-subagents.md](./11-subagents.md)
- [12-fallback.md](./12-fallback.md)