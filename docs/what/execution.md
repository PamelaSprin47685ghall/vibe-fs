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

## EXEC-028：同步 one-shot 返回语义

同步 one-shot agent 工具（如 `inspector`/`coder`）成功完成时：entry-local LWR 注释（`includeOpening=false`）+ 末条 TurnFormalText 报告；禁止字段式 `work_record`。LWR 物化与子→父方向同 COMPANION-003（与 EXEC-004 共用物化器，非 Join 批次 wire）。Opening 从原始 assignment 捕获（对齐 fork），以便 COMPANION-003 物化可运行；返回的 LWR 仍为 `includeOpening=false`。若 Completed 无法物化出非空 child LWR，则 fail-closed：返回工具级 `error=`（显式失败），绝不静默退回仅 formal report 的 soft success。

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

## EXEC-027：Student 学习与编译程序

只有显式 Student HumanRoot 启动本程序；其它 Agent 零副作用。

```text
HumanRoot 原文先写 QA
→ StudentLearn（工具严格 {teacher}）
→ teacher(message)：问题先写 QA，再发同一 Teacher
→ Teacher return：回答先写 QA，当前 Teacher turn 正常 completion 后再交付 Student
→ Student learning idle：发送 StudentCompile continuation
→ StudentCompile（读 QA，写一个或多个 AGENT-022 SKILL.md，工具含最终 return）
→ return(message)：删除 QA 并确认不存在
→ 同一 Host loop 的 Assistant completion 显示 message，Student Run 终止
```

Teacher 未 return 的 idle 只 nudge 同一 Teacher；自动恢复预算耗尽后父 `teacher` 失败，绝不从普通正文
截取答案。Student compile 未 return 的 idle 只发 compilation nudge；第一版不返回学习。

Teacher `return` 不 abort Session 或当前成功 turn。其 tool result 约束同一 Host loop 产生固定、无业务内容的
Assistant completion；只有该 completion 被 reconcile 为 `TurnCompleted` 后，等待中的父 `teacher` 才取得
已落盘答案。后续问题继续使用同一 Teacher Session；成功路径不得产生 `interrupted`。

同一 Student Run 同时最多一个 teacher 调用、一个 Teacher provider run、一个 QA 写入和一个 compile
continuation。异常并发明确拒绝。用户取消时 abort Student/Teacher、删除 QA、retire Teacher 关联；删除
失败保留文件并报告清理错误。

最终 `return` 先按 AGENT-022 校验全部触达制品，再删除 QA；任一步失败都不提交 terminal、不显示完成
说明。QA 不存在视为删除成功，重试幂等。最终 message 只简述生成/修改了哪些 SKILL、提醒重启 OpenCode，
不自动附带 QA/path。
