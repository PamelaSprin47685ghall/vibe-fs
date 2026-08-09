# 执行 — 证明

行为：`what/execution.md`。所有权：`shape/execution.md`。程序：`how/execution.md`。

## Handle / PTY

| 证明 | 条款 |
|------|------|
| 四态不可非法回退 | EXEC-009 |
| PTY completion 仅 onExit | EXEC-015 |
| ABORTED 非 agent 终态 | EXEC-020 |

## Join

| 证明 | 条款 |
|------|------|
| JoinGuard 优先于其它 Manager completion 分支；Manager 面无 Review Guard | EXEC-016、GLORY-070 |
| 中断 = interrupted（operator_abort 与 user_message）非 error；DevOps 超时 → TIMED_OUT；user wake 不 cancel 资源；completion 优先于 interrupt race；PromptKey continuation/compaction 不唤醒 | EXEC-017、EXEC-025 |
| active JoinAttempt 收到 user message 会醒；zero-active 消息不唤醒 future join；attempt 建立后、mailbox wait 前的消息仍唤醒当前 attempt | EXEC-017 |
| user_message 后的新 attempt 不继承旧 wake且不取消 child；Esc 返回当前 join 的 operator_abort并取消全部 running sub-session；新 child 的 completion 由后续 join 消费；全程零裸 `#` repair | `tests/e2e/cases/temporal-ownership-unhappy-path.test.mjs`（EXEC-017） |
| EXEC-018 的批次上限、稳定排序、CAS | EXEC-018 |
| blob v2；LegacyFalseAbort 永不 completion | EXEC-021 |
| 假 completion 补偿路径 | EXEC-022 |

## One-shot

| 证明 | 条款 |
|------|------|
| 同步 one-shot 返回 = LWR 注释（includeOpening=false）+ 末条 TurnFormalText；无字段式 work_record | EXEC-028 → tests/unit/tools/oneshot-tools.test.mjs |
| Completed 无物化 LWR → 工具级 error=，不 soft-omit（fail-closed） | EXEC-028 → tests/unit/tools/oneshot-tools.test.mjs（`COD_completed_without_lifecycle_work_record_fails_closed`、`INSPECTOR_completed_without_lifecycle_work_record_fails_closed`） |
| 真实 Journal 物化成功 → LWR + formal（Opening 捕获 → ChildWorkRecordFor 物化） | EXEC-028 → tests/unit/tools/oneshot-tools.test.mjs（`COD_completed_materializes_lifecycle_work_record_from_real_journal`） |
| 生产链路 Opening 捕获 → ChildWorkRecordFor 物化 → LWR + formal（happy path，含真实 Opening） | EXEC-028 → tests/integration/plugin/manager-tool-contract.test.mjs（`EXEC_002`） |

## Mailbox / 恢复

| 证明 | 条款 |
|------|------|
| agent Pulse vs PTY Publish 分通道 | EXEC-024 |
| ChildRecovery 分支穷尽与线性序 | EXEC-023 |
| AwaitAgentWithPermit 乱序完成只返回目标 agent；每 join 重新 requirePermit；completion 从 Journal 投影读 | EXEC-023/024 |

代表：`tests/unit/execution/*`（join-v2-wire、handle、fork）。

## Student / Teacher

| 证明 | 期望 | 条款 |
|------|------|------|
| teacher call scope | 并发第二调用拒绝；问题已落盘不回滚；scope 释放后下一调用可开始 | EXEC-027、EXEC-026 |
| return completion scope | 无 call/重复/错 Session fail closed；匹配正常 terminal 后只完成一个父调用 | EXEC-026 |
| 静态所有权 | 无 mutable lifecycle record；scope fixture 的状态轴与业务字段均被 DSL gate 拒绝 | EXEC-026、FLOW-006 |
| Teacher 正常结束 | return 后固定 completion 为 `TurnCompleted`；成功路径无 abort/interrupted | EXEC-026、EXEC-027、HOST-014 |
| Teacher idle | 同 Session nudge；预算耗尽失败；普通正文不作答案；payload normalize 后比对固定 completion | EXEC-027 |
| Student idle | Learn→Compile 一次；Compile idle nudge（有界）；不回 Learn；Claimed-not-Accepted 不误判 Learn | EXEC-027、PROMPT-012 |
| final return | AGENT-022 全量校验后才 delete/pending terminal；完成判据读 QA 存在性；失败可重试且无最终回复 | EXEC-027、AGENT-022、EXEC-026 |
| cancel/delete | abort 两端、retire Teacher、删除 QA；两个 CE await 点均可取消；清理失败不伪装成功 | EXEC-027、HOST-008 |
| CE collapse | InvokeTeacher 为 Returned→Completion 单栈；无 teacherCompletions/CompletionRun；无 teacherOwners cache | EXEC-026、FLOW-006 |
