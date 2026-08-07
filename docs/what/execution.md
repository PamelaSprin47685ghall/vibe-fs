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
工具调用中止（operator abort）→ `status=interrupted, reason=operator_abort`，不是 error（EXEC-017）；排队中的用户消息不中断 join。

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

join 等待只被 `OperatorAbort`（宿主中止当前工具调用）或 `DeadlineExpired`（DevOps 超时）中断；中断是 `JoinWaitOutcome.Interrupted of JoinInterruptReason`，不是 ForkError。wire：operator abort → `status=interrupted, reason=operator_abort`；DevOps 超时 → `ForkError.TimedOut`（`status="failed", code="TIMED_OUT"`，EXEC-004）。排队中的用户消息不进入 join race，不中断等待。tool abort ≠ runtime.Cancel。

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
