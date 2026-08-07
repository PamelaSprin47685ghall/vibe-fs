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
用户消息中断 → `status=interrupted`，不是 error（EXEC-017）。

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

有 join 义务且仍有 outstanding 后台时，JoinGuard Continuation 优先于 Manager Review Guard。本 turn 不做 review 检查。

## EXEC-017：Join 中断不是错误

用户新消息打断 join 等待 → 特殊 interrupted 结果，优先处理用户消息。tool abort ≠ runtime.Cancel。

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
