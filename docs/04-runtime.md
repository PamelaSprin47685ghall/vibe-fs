# 04 — Runtime 副作用与边界层

## 职责

`src/Runtime/` 是 Kernel 与 Node/宿主之间的常规 IO 边界：文件、锁、子进程、网络、MCP、动态对象编解码、运行时队列、review/nudge/event store、subsession actor 与 fallback。宿主绑定位于 `src/Hosts/`，不再以 `Shell/` 作为源码路径别名。

## 能力分簇

| 分簇 | 代表模块 | 说明 |
| :--- | :--- | :--- |
| 文件系统 | `FileSys`、`WorkspaceFiles` | 读写工作区 |
| 事件日志 | `EventStore/`（`EventStore`、`EventLogCodec`、`EventLogIo`、`EventLogFile`、`EventLogRuntime*`、各类 EventWriter） | NDJSON、锁、缓存、sync 与 projection |
| 搜索 | `Search/FuzzySearch*`、`FuzzyIteratorStore`、`Semble*` | fff 后端、分页 iterator、MCP 搜索 |
| Semble | `SembleMcp`、`SembleSearch`、`SembleSearchClient`、`SembleSearchTypes` | MCP 客户端与 inspector 断点注入 |
| 执行器 | `Execution/Executor*`、`SessionExecutor`、`ExecutorToolsCodec` | shell/python/js、spawn、会话级执行 |
| Tree-sitter | `TreeSitterShell`、`TreeSitterPlatform` | 可选语法能力 |
| 网络 | `WebSearchApi`、`WebFetch`、`WebSearchCodec`、`TitleFetchGuardCommon` | 搜索与抓取守卫 |
| 动态类型 | `Dyn`、`DynField`、`ErrorClassify`、`JsArrayMutate`、`PromiseStr` | `obj` 安全访问 |
| 并发 | `PromiseQueue`、`Execution/SerialStateHolder`、`Execution/LivelockGuard`、`Wanxiangzhen/CoordinatorLifecycle` | 串行队列与守卫 |
| 时钟 | `Clock` | 可注入时间（测试） |
| 子代理分发 | `Subsession/SubagentDispatcher`、`SubagentSpawn`、`SubagentIo`、`SubagentPromptBuild`、`SubagentIntentsCodec`、`SubagentSimpleArgsCodec`、`SubagentIteratorStore` | 统一委派 + 各宿主接线 |
| **Subsession Actor** | `Subsession/` 下的 `SubsessionActor`、`SubsessionActorRegistry`、`SubsessionEventRouter`、`SubsessionEventStore`、`SubsessionEventWire`、`SubsessionReconcile`、`SubsessionService`、`SubsessionTranscript`、`SubsessionChildObserver` | 子会话 Actor 消息泵与恢复 |
| Review/Nudge | `ReviewPrompts/`、`Nudge/`、`EventStore/ReviewEventWriter`、`EventStore/NudgeEventWriter` | 投影同步与异步 nudge |
| Fallback | `Fallback/`、`Continuation*`、`RuntimeStore`、`SessionRuntime*Pure` | 降级规则、租约、continuation 与运行时状态 |
| Context budget | `Execution/ContextBudget*`、`MessageTransform/ContextBudget*` | 用量、触发、模型窗口与投影周期 |
| Caps | `Workspace/`、`PromptFragments.fs`、`PromptFrontMatter.fs` | caps 文件、片段与 front matter |
| MessageTransform | `MessageTransform/`、各宿主 `MessageTransform*`、`Messaging/` codec | 共享管线与宿主边界 |
| Tool 编解码 | `Tooling/`、`Execution/*ToolsCodec`、`Messaging/` codec | 参数解析、控制字段、执行分发 |
| 宿主专用 codec | `Messaging/Opencode*`、`Mux*`、`OmpHostBindings.fs` | hook 入参/出参 |
| OMP 绑定 | `src/Runtime/Messaging/OmpHostBindings.fs` | 宿主 API 薄封装 |
| Subsession 宿主适配 | `src/Hosts/OpenCode/SubsessionHostAdapter.fs`、`src/Hosts/Omp/SubsessionHostAdapter.fs` | `ISubsessionHost` 实现 |
| 万象阵 | `Wanxiangzhen/`（`CoordinatorRuntime`、`CoordinatorReplay`、`SessionIo`、`HttpServer`、`SlaveRuntime`、`SquadEventLogRuntime` 等） | 协调器副作用；事件 append 走共用 `EventStore` |

## EventLog 运行时链（写路径）

```text
业务 hook / 工具成功
  → EventStore / EventLogRuntime append*
  → EventLogRuntimeStore.appendAndCache
  → EventStore.AppendEvent
  → EventLogIo.appendLine (先锁后写)
  → EventSourcing.Fold.applyEvent 更新进程内 SessionState
```

原子多事件：`AppendEventsOrFail` 在一个锁内写所有行，用于 Subsession 决策信封。

读路径：`GetSessionState` / `ReadAllEvents` → `EventLogRuntimeSync` → review、backlog、fallback projection 重建。

文件名为 **`.wanxiangshu.ndjson`**，锁文件 **`.wanxiangshu.ndjson.lock`**（`EventLogCodec`）。

## Tool 执行路径（概念）

1. 宿主传入 `toolName` + `args`（`obj`）
2. `ToolArgsDecode` → 强类型参数
3. `Kernel` 校验（权限、业务规则）
4. `ToolExecute` / 子代理模块执行 Shell IO
5. 结果编码为宿主 `parts` / `output`

OpenCode/Mux/OMP 通过各自 `src/Hosts/` 绑定调用 Runtime；OMP 不引用 OpenCode 或 Mux 宿主实现。

## Subsession Actor 运行时链

```text
父工具（coder/inspector/browser/meditator）
  → Subsession.SubagentDispatcher.dispatch
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
  → 各宿主 `src/Hosts/*/Fallback/` handler
    → Runtime/Fallback Coordinator
      → IEventTranslator.TranslateError → FallbackEvent
      → Kernel.FallbackKernel.StateMachine.transition → (newState, action)
      → 若 action 为 SendContinue → 构造六阶段续命租约
        → appendContinuationRequested → TryTransitionPendingLease → executor.SendContinue
        → appendContinuationDispatched
      → 返回 FallbackHookResult { Consumed; State }
```

详 [12-fallback.md](./12-fallback.md)。

## MessageTransform 管线

`src/Runtime/MessageTransform/Pipeline.fs` 接收计划（session、agent、directory、scope、token 预算等），依次：

- 清理/过滤消息
- caps / backlog 投影注入
- Semble 注入（开关）
- parallel tool prompt（单工具伪装并行时的 SSOT 文案）
- context budget 阶段（见 [13-context-budget.md](./13-context-budget.md)）

宿主入口：

- `src/Hosts/OpenCode/MessageTransformPipeline.fs`
- `src/Hosts/Mux/MessageTransform.fs`
- `src/Hosts/Omp/MessageTransform.fs`

## RuntimeScope

每个会话/工作区上下文由 `src/Runtime/Workspace/RuntimeScope.fs` 持有：

- Fuzzy iterator store
- Subagent iterator store
- SessionProjectionStore（backlog 缓存）
- Caps 文件缓存
- ContextBudget 状态
- 其他 scope 级可变状态（含 `fallbackRuntime` 引用）

**禁止**在 Runtime 模块级复制第二份全局 store（架构测试）。

## Dyn 纪律

Kernel 不得 `open Dyn`。Runtime codec 集中 `get`/`set`/数组变异；hook output 必须经宿主 codec，避免 OpenCode 插件「换引用」导致 hook 失效（`AGENTS.md`）。

## 与 ../mux、../oh-my-pi 的改动范围

- **Mux**：优先改本仓库；必要时改 `../mux` **binding**，核心最小动。
- **OMP**：禁止改 `../oh-my-pi` 上游，仅参考；实现留在本仓库 `src/Hosts/Omp/` + `src/Runtime/`。

## REF 架构演进方向

### Effect Supervisor（效应监督器）

当前架构中 Fallback 续命与 Subsession Dispatch 的效应执行散布在各模块中。REF 架构提出**效应监督器**模式：

- 监督器从 **持久化 Outbox**（而非内存 `CommittedDecision`）消费 Effect
- 执行宿主调用（Dispatch、Abort、Query、Reconciliation）
- 处理超时/重试，将结果映射为领域 Command 回流至 Inbox
- 交付语义：**At-least-once delivery + idempotent host operation + correlation / reconciliation**

### Durable Effect Outbox

每个事务提交时，领域事件与 Effect 意图在**同一提交屏障**写入持久化 Outbox：

```text
1. Dequeue Command → 2. Validate → 3. Decide → 4. Persist(Domain + Outbox) → 5. Commit →
6. Reconcile Resources → 7. Commit Handlers → 8. Publish Events → 9. Wake Supervisors → 10. Next
```

第 4 步失败则整个事务回滚（5–9 不执行）。第 6–9 步失败不触发回滚——已落盘的事实不被撤销，依赖 Reconciliation 恢复。

### RAII Resource Scopes

资源管理从手动 `ArmXxx / CancelXxx` 演化为 `CommittedState → ResourceSpec` 纯函数投影。

**资源分类**：

|类别|示例|重启后恢复|管理方式|
|:---|:---|:---|:---|
|Durable Resource|Turn Deadline、Abort Deadline、Reconciliation Deadline|✅ 需恢复|CommittedState 投影 → ResourcePlan|
|Invocation Resource|CallerReplyLease、UI subscription、trace listener|❌ 不恢复|调用 Scope 管理|

**Stable Resource Identity Law**：资源是否复用由稳定 Key（`TurnDeadline(turnId)` / `AbortDeadline(turnId)`）决定，而非由 State 对象引用相等决定。Deadline 使用**绝对到期时间**（DeadlineAt），重启后 `remaining = DeadlineAt - Clock.Now`。

### 相关

- REF.md（架构演进总纲）
- [02-architecture.md](./02-architecture.md) § 架构演进
- [05-event-sourcing.md](./05-event-sourcing.md) § Durable Effect Law

## 相关文档

- [05-event-sourcing.md](./05-event-sourcing.md)
- [10-message-transform.md](./10-message-transform.md)
- [11-subagents.md](./11-subagents.md)
- [12-fallback.md](./12-fallback.md)
