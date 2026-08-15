# delegation — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型、物理落点与历史裁决。

## 实现模型

### 委托面：fork / commission / inspect / establish-behavior / repair-behavior

| 面 | owner 角色 | 语义 | 物理实现 |
|----|-----------|------|---------|
| `fork` | Manager | mission 内 witness；Byname 承接 charge；可选 attachment / tool-call estimate | `Session/ForkRuntime.fs`（ChildRun map）、`Domain/ForkChildPayload.fs`（首 prompt） |
| `commission` | Orchestrator | 独立集成之路；calling 在场=新路，缺省=续做；可选 tool-call estimate | `Application/Orchestration/*.fs`、`Infrastructure/Git/WorktreeResource.fs` |
| `inspect` / `establish-behavior` / `repair-behavior` | SyncDelegate callers | 同步委托；普通 completion → bounded WorkRecord；可选 tool-call estimate | `Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore}.fs` |

### fork attachment（DELEG-021）

`attach` 在 parent `HandleProjection` 以 Byname 定位 sibling/retired child，再调用唯一
`LifecycleWorkRecord(includeOpening=true)` projector。`ForkChildPayload` 只接收 `Attachment: string option`
并在 `commissioner_record` 后、requirements 前渲染 `attached_work_record` data block；不解析 LWR、
不复制 Journal projection。new fork 与 idle reuse 可物化；busy reuse 不物化，只返回自然语言 deferred
说明。unknown/self 在任何 send 前拒绝。

### delegated tool estimate（DELEG-022）

持久事实只有 `DelegatedToolEstimateReplaced(SessionId, ExpectedToolCalls)` 与
`DelegatedToolCallObserved(SessionId, ToolCallId)`。`DelegatedToolEstimateProjection` 纯 fold：replace →
`Remaining=X, CountedCalls=∅`；observe → duplicate/zero no-op，否则 `Remaining-1` 并记录 call id。
`CountedCalls` 最大长度 ≤ 本次 X；remaining=0 后 Host 不再 append observation，因此不会随 session 生命周期
无限增长。projection 挂在 `SessionAgentProjection`，按 SessionId O(1) 读取；禁止从 XTrace/transcript 派生。

estimate 在 delegated prompt/nudge 物理发送前 durable append：fork/reuse 在 child session 已解析后；
commission 在 Manager session 已创建/解析后；SyncDelegate 在 `GetOrCreate` 后、`SendPrompt` 前。省略参数不
append replace fact。SyncDelegate semantic batch 对全部显式值求和；无显式值 = None。

全局 `tool.execute.before` 是真实 tool invocation 的唯一 observation seam：有 session + callID 且该 session
存在 estimate/remaining>0 时 append `DelegatedToolCallObserved`。synthetic HOST-013 pair 不经过 execute hook，
天然不计数。该 hook 只记事实，不决定工具是否继续执行。

### SyncDelegate 核心类型（`Kernel/SyncDelegate.fs`）

- `SyncDelegateRole = Inspector | Coder`；`DedicatedDelegateKey = { Scope: ReuseScopeId; Role }`。
- `SyncDelegateBatch = { ProviderRun; CallOrder: ToolCallId list; CurrentCall }`——同一 ProviderRun 的
  同 role calls 按 Host tool-call 顺序构成一个语义 batch（DELEG-008）。OpenCode 边界同时保留两份
  Host 观察：`message.part.updated` 的本地 ordered projection 与 `ISessionSnapshotPort` 的 message snapshot；
  两者都是同一 call list 的暂时前缀，按前缀兼容关系选择更完整者，禁止把任一滞后的单源前缀直接封口。
- `SyncDelegateInvocationResult = WorkRecord of string | MergedInto of ToolCallId`——canonical 得正文、
  siblings 得引用（DELEG-012）。
- `tierForOwner = identity`（fast→fast、deep→deep）；`agentNameFor role tier` 生成 `fast-inspector` 等
  墙内名（DELEG-010）。
- `delegateRoleToAttachment`：`Inspector → SyncInspector`、`Coder → SyncCoder`（HOST-008 的
  Work+Attached 登记；AttachmentKind 归属 `managed-session-lifecycle`/`session-ontology`）。

### 同步委托 CE 单栈（历史 how/execution EXEC-026/031）

```text
eventPrefix = observedHostToolParts(providerRun, role) // ordered, ToolCallId de-duped
snapshotPrefix = syncCallsInHostMessage(providerRun, role)
expected = longerCompatiblePrefix(eventPrefix, snapshotPrefix)
admit current invocation against expected
when all expected members present:
  reserve (immediateCallerReuseScope, role)
  delegate = attachedSessions.GetOrCreate(ownerReuseScopeId, role)
  prepared = members |> map prepareProviderPrompt        // provider order
  request = concat charges / concat prepared prompts
  Send(delegate, request)
  completion = await ordinary Assistant Completion
  workRecord = materializeBoundedWorkRecord(InvocationStart..InvocationEnd, includeOpening=false)
  canonical = expected[0] → workRecord；siblings → merged-reference
```

`message.updated finish` 不参与 batch 封口：真实 Host 在 tool execute 返回后才发布该 finish；等待它会让
sync tool 自己阻塞自己的完成。Long Stroke 的 streamed 3×`inspect` 回归固定此边界。

### Charge / ProviderPrompt 分离

- `SyncDelegatePromptRequest = { Charge; ProviderPrompt }`（`Domain/SyncDelegatePrompt.fs`）。
- 无 warm-start 时两者字节相同；有 AGENT-032 keywords 时只 enrich `ProviderPrompt`（DELEG-019）。
- `SyncDelegatePrompt.IdleNudge = "delegation/sync-idle"`：SyncDelegate turn 失败未完成时的 idle nudge。

### NEEDHELP consultation 委托（AGENT-031 / HOST-027）

`deep-*` 命中 `[NEEDHELP]` → assistance abort（不写 FallbackCursor、不进 ProviderFailure）→ 等
`IdleRevisit` transport fence → 创建真实 `deep-inquiry` consultation child（freeze frontier →
`CommissionerRecord` = `LifecycleWorkRecord(includeOpening=true)`）→ 完成 → `includeOpening=false`
WorkRecord → typed `NeedHelpAdvice` continuation 返回原 binding。single-flight + 有限额度（资源策略，
数值不向 provider 暴露）。sentinel 在 XTrace capture 前剥离。

### 委托失败与恢复时序契约（DELEG-023）

- `SyncDelegateRuntime.HandleTurn` 仅处理 `TurnCompleted`（捕获 terminal + 物化 bounded WorkRecord + 完成 `call.Answer`）。
- `TurnFailed` / `TurnInProgress` / `TurnNeedsContinuation` 保持 child-local，返回 `false` 且不弹出调用，放行至 `OrdinaryTurnWorkflow` 触发 AABB / ProviderRetry continuation。
- `SyncDelegateWorkflow.invoke` 通过 `SubscribeTerminal` 监听终端结果：仅在 `TerminalOutcome.Failed`（恢复预算耗尽）或 `TerminalOutcome.Aborted` 时才向调用方返回失败。
- `AssistanceHost.handleConsultationTurn` 对 `TurnFailed` 返回 `NotAssistance`，放行至普通恢复流程；仅在终端失败时通过 `SubscribeTerminal` 交付失败建议。

### Join 有界批次（`Session/CompletionMailbox.fs`、`Session/ForkRuntime.fs`）

- `WaitForSignal(interrupt)` / `DrainAgentWakes`（agent 路径仅 Pulse，无 payload）/ `DrainPtyCompletions`。
- 批次上限 `MaxJoinBatch`；稳定排序；逐项 CAS；中断前再 drain（EXEC-018/019）。

## 物理落点（CURRENT EVIDENCE）

- 类型：`Kernel/SyncDelegate.fs`、`Domain/{SyncDelegatePrompt,ForkChildPayload}.fs`。
- Wiring：`Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore,ForkRuntime}.fs`。
- Resource：`resources/provider/tool/{fork,commission,inspect,sync-delegate}/`、`resources/provider/delegation/**`。
- Tests：包内 `tests/fork-child-payload.test.mjs`；REUSE 清单见 PROOF.md。

## 边界与弃权（非 normative）

- **GARBAGE——Student/Teacher/`return`/Meditator**：`Role.Student|Teacher`、Learn/Compile/SKILL、
  `StudentQaStore`、独立 `return` 工具、`Returned → Completion` 双 await、`completion_text` /
  `SyncDelegateReturnCompletion` magic、`tdd`、`list` DTO、legacy `meditator` 身份：已 clean-break 删除，
  不进入未来 WHAT（EXEC-027 空缺、AGENT-020/022 空缺、历史 how/execution「已删除算法面」、
  CHANGES-AUDIT：universal.md / ce-student-teacher-collapse.md 的 GARBAGE 裁决）。
- **GARBAGE——fork-manager 工具面**：旧 `fork-manager` / `list` / `verdict` / `blog` / `executor`(工具) /
  `fork-pty` 名：GrandRewrite clean-break，无 alias（历史 how/execution 条款）。
- **HOW——具体数值**：`MaxJoinBatch=32`、`DevOpsJoinTimeoutMs=10_000`、`ReduceFanIn=8`、
  `AwaitAgentTimeoutMs=600_000`：有界性才是 WHAT。
- **HOW——工具名**：`fork`/`commission`/`inspect`/`establish-behavior`/`repair-behavior` 是当前选择
  （DELEG-020）；改名不动 WHAT。
- **HOW——Dedicated reuse 机制**：`(OwnerReuseScopeId, role) → at most one live Session` 的复用实现、
  retire/dispose 时序 → `managed-session-lifecycle` 拥有；本包只拥有语义 batch / serialization /
  canonical 分型。
- **不复制** `work-record`（WorkRecord 三段标题、Opening 捕获、includeOpening 语义）、
  `participant-horizon`（准入 filter 全法则）、`interaction-authority`（Esc/ingress authority 语义）的命题。
