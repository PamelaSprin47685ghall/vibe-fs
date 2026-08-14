# delegation — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型、物理落点与历史裁决。

## 实现模型

### 委托面：fork / commission / inspect / establish-behavior / repair-behavior

| 面 | owner 角色 | 语义 | 物理实现 |
|----|-----------|------|---------|
| `fork` | Manager | mission 内 witness；Byname 承接 charge | `Session/ForkRuntime.fs`（ChildRun map）、`Domain/ForkChildPayload.fs`（首 prompt） |
| `commission` | Orchestrator | 独立集成之路；calling 在场=新路，缺省=续做 | `Application/Orchestration/*.fs`、`Infrastructure/Git/WorktreeResource.fs` |
| `inspect` / `establish-behavior` / `repair-behavior` | SyncDelegate callers | 同步委托；普通 completion → bounded WorkRecord | `Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore}.fs` |

### SyncDelegate 核心类型（`Kernel/SyncDelegate.fs`）

- `SyncDelegateRole = Inspector | Coder`；`DedicatedDelegateKey = { Scope: ReuseScopeId; Role }`。
- `SyncDelegateBatch = { ProviderRun; CallOrder: ToolCallId list; CurrentCall }`——同一 ProviderRun 的
  同 role calls 按 Host tool-call 顺序构成一个语义 batch（DELEG-008）。
- `SyncDelegateInvocationResult = WorkRecord of string | MergedInto of ToolCallId`——canonical 得正文、
  siblings 得引用（DELEG-012）。
- `tierForOwner = identity`（fast→fast、deep→deep）；`agentNameFor role tier` 生成 `fast-inspector` 等
  墙内名（DELEG-010）。
- `delegateRoleToAttachment`：`Inspector → SyncInspector`、`Coder → SyncCoder`（HOST-008 的
  Work+Attached 登记；AttachmentKind 归属 `managed-session-lifecycle`/`session-ontology`）。

### 同步委托 CE 单栈（`docs/how/execution.md` EXEC-026/031）

```text
expected = syncCallsInHostMessage(providerRun, role)   // ordered ToolCallIds
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
  不进入未来 WHAT（EXEC-027 空缺、AGENT-020/022 空缺、`docs/how/execution.md`「已删除算法面」、
  CHANGES-AUDIT：universal.md / ce-student-teacher-collapse.md 的 GARBAGE 裁决）。
- **GARBAGE——fork-manager 工具面**：旧 `fork-manager` / `list` / `verdict` / `blog` / `executor`(工具) /
  `fork-pty` 名：GrandRewrite clean-break，无 alias（`docs/how/execution.md`）。
- **HOW——具体数值**：`MaxJoinBatch=32`、`DevOpsJoinTimeoutMs=10_000`、`ReduceFanIn=8`、
  `AwaitAgentTimeoutMs=600_000`：有界性才是 WHAT。
- **HOW——工具名**：`fork`/`commission`/`inspect`/`establish-behavior`/`repair-behavior` 是当前选择
  （DELEG-020）；改名不动 WHAT。
- **HOW——Dedicated reuse 机制**：`(OwnerReuseScopeId, role) → at most one live Session` 的复用实现、
  retire/dispose 时序 → `managed-session-lifecycle` 拥有；本包只拥有语义 batch / serialization /
  canonical 分型。
- **不复制** `work-record`（WorkRecord 三段标题、Opening 捕获、includeOpening 语义）、
  `participant-horizon`（准入 filter 全法则）、`interaction-authority`（Esc/ingress authority 语义）的命题。
