# 执行模型 — 可观察行为

条款前缀：`EXEC-`。  
Handle / PTY / Mailbox 所有权见 `shape/execution.md`。  
Join 批次、blob、进程预算见 `how/execution.md`。

## EXEC-001：Fork/Join/List

| 角色 | 工具 |
|------|------|
| Manager | `fork-agent` / `join` / `list` |
| Orchestrator | `fork-manager` / `join` |
| DevOps | 另有 `fork-pty` |

## EXEC-002：Fork-agent 语义

- 新建：准确角色名 + 非空 prompt。  
- 已有 AgentId + prompt：nudge/continue，fire-and-forget。  
- Busy existing：不新 RunId、不新 listener、不新 completion；nudge 归属当前 active Run。

## EXEC-003：Fork-pty 语义

- 新 PTY / 已有 id + 非空 prompt = write  
- id + 空 prompt = read  
- id + signal = 发信号  

## EXEC-004：Join 语义

Join 消费当前 owner 可用 completion，有界批次 wire（status/count/`[[result]]`）；agent 完成项 entry-local LWR 注释（`includeOpening=false`），禁止字段式 `work_record`。  
DevOps 角色的 `join` 在无完成项时包含 10s 超时预算（`DevOpsJoinTimeoutMs = 10_000`）；若 10s 内无 completion，返回超时错误 `ForkError.TimedOut`（`status="failed"`, `code="TIMED_OUT"`）。Orchestrator 与 Manager 角色的 `join` 维持无 10s 超时规则。  
工具调用中止（operator abort）→ `status=interrupted, reason=operator_abort`；外部用户入站 → `status=interrupted, reason=user_message`；均非 error（EXEC-017）。

## EXEC-028：同步返回语义（OneShot vs SyncDelegate）

同步 agent 工具有两条互斥生命周期路径。**不得**混用：OneShot 的 dispose-after 不得套在 dedicated
SyncDelegate Session 上；SyncDelegate 的双 await 不得退化成单次 terminal 即放行。

### A. Residual OneShot（dispose-after）

仍用于**非 dedicated SyncDelegate** 的 residual one-shot callers（若有）：每次调用新建 child Session，
成功完成后 abort/dispose child，不跨调用复用。

成功完成时：entry-local LWR 注释（`includeOpening=false`）+ 末条 TurnFormalText 报告；禁止字段式
`work_record`。LWR 物化与子→父方向同 COMPANION-003（与 EXEC-004 共用物化器，非 Join 批次 wire）。
Opening 从原始 assignment 捕获（对齐 fork），以便 COMPANION-003 物化可运行；返回的 LWR 仍为
`includeOpening=false`。若 Completed 无法物化出非空 child LWR，则 fail-closed：返回工具级 `error=`
（显式失败），绝不静默退回仅 formal report 的 soft success。

### B. Reusable SyncDelegate（Returned → Completion）

Meditator / Coder / DevOps 的 dedicated `inspector` / `coder`（及同类 SyncDelegate）走本路径：

```text
callee return(message)
→ resolve Returned only（答案已定，caller 仍阻塞）

同 Host loop 继续固定 terminal assistant completion
→ reconciler 证明 TurnCompleted
→ resolve Completion

caller 取得 tool result = message
（须 Returned 与 Completion 均完成）
```

不变量：

- `return` **只** resolve `Returned`；不得因 `return` 单独放行 caller 或 retire dedicated Session。
- `Completion` 仅在 `TurnCompleted` 证明后 resolve；成功路径无 abort / interrupted。
- caller 阻塞到 **Returned 且 Completion** 都完成，保证下一同步调用不与上一 turn 尾部重叠。
- Session 按 `(OwnerReuseScopeId, role)` 复用（EXEC-026）；wire 仍可带 entry-local LWR 注释 + formal
  report，但生命周期以双 await 为准，不以 OneShot dispose-after 为准。

### Serialization 与 tier（行为面）

- Serialization key = **immediate caller ReuseScope**（非 family root）。嵌套
  `DevOps → Coder → Inspector` 合法；同 caller ReuseScope 禁止并发两个 active sync delegate calls。
- Owner effective tier → deterministic delegate tier（`fast→fast`，`deep→deep`）；不可每轮选 Agent。

## EXEC-005：List 语义

List 列当前 running handle，不是可创建 Agent 菜单。

## EXEC-006：Child Run 生命周期

Child Run 生命周期与父背景记录分离。

## EXEC-007：Nudge

Nudge 是 Continuation（PROMPT-003），不建新 Authority。

## EXEC-008：Parent Background

父背景记录不冒充 child completion。

## EXEC-015：PTY 行为

PTY completion **只**由 backend `onExit` 触发。禁止 stdout 启发式「看起来结束了」。

## EXEC-016：Background Join Guard

有 join 义务且仍有 outstanding 后台时，本 turn 只发 JoinGuard Continuation；finality 处理停放，Manager 不做 idle 鼓励（GLORY-029/070）。

## EXEC-017：Join 中断不是错误

join 等待直至：completion 可用 / 本地 operator abort / external-user ingress 唤醒 / 适用的 DevOps deadline。中断是 `JoinWaitOutcome.Interrupted of JoinInterruptReason`，不是 ForkError。`JoinInterruptReason` = `OperatorAbort` \| `UserMessageArrived` \| `DeadlineExpired`。

External-user ingress 只打断**当前** wait：不 cancel mailbox/runtime/session/child，也不本身授予 Prompt authority。每个 `join` 入口先建立一个 `JoinAttempt`；消息只 fan-out 给该 Session 当时 active 的 attempt。无 active attempt 的消息仍进入正常 Host 队列，但作为 join wake 丢弃，绝不 latched 给 future join。任意 race 唤醒后，已可用的 completion 先 drain，再才发出 interrupt 结果。

operator abort 先打断当前 `JoinAttempt`，使 join 返回 `reason=operator_abort`；同一次 Esc 随后终止父 provider attempt，`TurnAborted` cleanup 必须取消该父全部仍在运行的 sub-session。已经完成并进入 `CompletedAwaitingJoin` 的结果仍可消费。与之相对，external-user ingress 不产生 `TurnAborted`，不得取消任何 sub-session。

wire：operator abort → `status=interrupted, reason=operator_abort`；user message → `status=interrupted, reason=user_message`；DevOps 超时 → `ForkError.TimedOut`（`status="failed", code="TIMED_OUT"`，EXEC-004）。tool abort ≠ runtime.Cancel。中途用户消息可唤醒 join，不经 AcceptHumanRoot、不重置 LogicalRun、不新建 Manager Life（PROMPT-004 不变，fail-closed）。

## EXEC-020：Agent 终态代数（无 ABORTED）

```text
Completed | Failed | Abandoned
```

**ABORTED 不是 agent 终态。** 取消是控制面。

## EXEC-021：completion blob v2

schemaVersion=2；finality 仅 `completed|failed`。  
`LegacyFalseAbort` **永不**成为 RunCompletion。  
`fromDecoded` 唯一构造。

## EXEC-022：假 completion 补偿

`HandleFalseCompletionRejected` → 确定性 replacement → parent correction。  
禁止把历史假 abort 洗成成功。

## EXEC-027：（空缺）Student 学习与编译程序 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 Student HumanRoot→QA→`StudentLearn`→`teacher`→
`StudentCompile`→SKILL/`return` 程序，以及 Teacher/Compile idle nudge 与 `StudentQaStore`。
无 alias、无 deprecated 执行路径。

后继：SyncDelegate Returned→Completion（EXEC-026/028）；idle 为 `SyncDelegateIdleNudge`
（PROMPT-003；`shape/host.md` quiescence gate）。`return` **仅** SyncDelegate。
