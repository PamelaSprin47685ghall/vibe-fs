# 04 — Runtime 副作用与边界层

## 职责

`src/Runtime/` 是 Kernel 与 Node/宿主之间的 IO 边界。文件、锁、子进程、网络、MCP、动态对象编解码、运行时队列、review/nudge/event store、subsession actor 与 fallback 全部在此。宿主绑定位于 `src/Hosts/`。

## 能力分簇

| 分簇 | 代表模块 | 说明 |
| :--- | :--- | :--- |
| 文件系统 | `Workspace/FileSys.fs`、`WorkspaceFiles.fs` | 读写工作区 |
| 事件日志 | `EventStore/`（24 文件） | NDJSON、锁、缓存、sync 与 projection |
| 搜索 | `Search/FuzzySearch*`、`FuzzyIteratorStore.fs`、`SembleSearch.fs` | fff 后端、分页 iterator、MCP 搜索 |
| 执行器 | `Execution/Executor*.fs`、`SessionExecutor.fs`、`ExecutorToolsCodec.fs` | shell/python/js、spawn、会话级执行 |
| Tree-sitter | `Tooling/TreeSitter*.fs` | 可选语法能力 |
| 网络 | `Tooling/WebSearchApi.fs`、`WebToolsCodec.fs` | 搜索与抓取 |
| 动态类型 | `Subsession/Dyn.fs`、`DynField.fs` | `obj` 安全访问 |
| 并发 | `Subsession/PromiseQueue.fs`、`Execution/SerialStateHolder.fs`、`Execution/LivelockGuard.fs` | 串行队列与守卫 |
| 时钟 | `Execution/Clock.fs` | 可注入时间（测试） |
| 子代理分发 | `Subsession/SubagentDispatcher.fs`、`SubagentSpawn.fs`、`SubagentIo.fs`、`SubagentPromptBuild.fs` | 统一委派 |
| **Subsession Actor** | `Subsession/SubsessionActor.fs`、`SubsessionActorRegistry.fs`、`SubsessionEventStore.fs`、`SubsessionEventRouter.fs`、`SubsessionReconcile.fs`、`SubsessionService.fs` | 子会话 Actor 消息泵与恢复 |
| Review/Nudge | `ReviewPrompts/`、`Nudge/NudgeFlow.fs`、`Nudge/NudgeDispatchClaim.fs` | 投影同步与异步 nudge |
| Fallback | `Fallback/`（33 文件） | 降级规则、租约、continuation |
| Caps | `Tooling/CapsFileCache.fs`、`CapsFormat.fs` | caps 文件、格式 |
| MessageTransform | `MessageTransform/Pipeline.fs`、`Stack.fs`、`CapsStage.fs`、`ParallelHintStage.fs` | 共享管线 |
| Tool 编解码 | `Tooling/ToolArgsDecode.fs`、`ToolHookRuntime.fs` | 参数解析、控制字段 |
| 宿主 codec | `Messaging/Opencode*.fs`、`Mux*.fs`、`OmpHostBindings.fs` | hook 入参/出参 |
| Wanxiangzhen | `Wanxiangzhen/`（24 文件） | 协调器副作用 |
| Dispatch | `Dispatch/SessionDispatcher.fs`、`HostReceiptWaiter.fs` | 续命发送与宿主确认 |

## EventLog 运行时链

```
业务 hook / 工具成功
  → EventStore.AppendEvent
    → EventLogRuntimeStore.appendAndCache
      → EventLogIo.appendLine (先锁后写)
      → EventSourcing.Fold.applyEvent 更新进程内 SessionState
```

原子多事件：`AppendEventsOrFail` 在一个锁内写所有行，用于 Subsession 决策信封。

## EventLog 文件

文件名 **`.wanxiangshu.ndjson`**，锁文件 **`.wanxiangshu.ndjson.lock`**。

`WanEvent` 类型（`EventEnvelope.fs`）：

```fsharp
type WanEvent =
    { V: int; Session: string; Kind: string; At: string
      Payload: Map<string, string>
      EventId: string option; WriterId: string option
      Sequence: int option; Checksum: string option }
```

## 写路径 Writers

| Writer | 模块 | 写事件 |
| :--- | :--- | :--- |
| `SessionEventWriter` | `SessionEventWriter.fs` | `assistant_completed`、`human_turn_started`、`compaction_*` 等 |
| `NudgeEventWriter` | `NudgeEventWriter.fs` | `nudge_requested`/`dispatched`/`failed`/`cancelled`/`settled`/`dedup_cleared` |
| `ReviewEventWriter` | `ReviewEventWriter.fs` | `loop_activated`、`review_verdict` 等 |
| `ContinuationEventWriter` | `ContinuationEventWriter.fs` | `continuation_*` |
| `SubsessionEventWriter` | `SubsessionEventWriter.fs` | `subsession_*` |

## RuntimeScope

`src/Runtime/Workspace/RuntimeScope.fs` 每 workspace 持有：projection store、caps cache、fuzzy/subagent iterator store、session executor registry。禁止在 Runtime 模块级复制第二份全局 store（架构测试）。

## Dyn 纪律

Kernel 不得 `open Dyn`。Runtime codec 集中 `get`/`set`/数组变异；hook output 必须经宿主 codec。

## 并发纪律

- `PromiseQueue.SerialQueue` 串行化关键路径（append、ff 合并、nudge claim）
- 按 session/workspace 切分串行域
- Fallback 双重门闩（`EventHandlingActive` + `MainContinuationAwaitingStart`）防并发访问冲突
