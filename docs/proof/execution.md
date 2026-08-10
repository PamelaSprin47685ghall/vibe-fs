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

## OneShot / SyncDelegate（EXEC-028 / EXEC-026）

Residual OneShot（dispose-after，`OneShotAgentTool.run`）与 SyncDelegate（Returned→Completion，dedicated reuse）互斥；下表分列，不混用路径。

| 证明 | 条款 |
|------|------|
| residual OneShot 返回 = LWR 注释（includeOpening=false）+ 末条 TurnFormalText；无字段式 work_record；dispose-after | EXEC-028（residual OneShot）→ tests/unit/tools/oneshot-tools.test.mjs（`ONESHOT_success_reports_outcome_and_disposes_the_child`、`ONESHOT_parent_work_record_lands_in_the_digest_field`） |
| residual OneShot：Completed 无物化 LWR → 工具级 error=，不 soft-omit（fail-closed） | EXEC-028（residual OneShot）→ tests/unit/tools/oneshot-tools.test.mjs（`ONESHOT_completed_without_lifecycle_work_record_fails_closed`） |
| residual OneShot：真实 Journal 物化成功 → LWR + formal（Opening 捕获 → ChildWorkRecordFor 物化） | EXEC-028（residual OneShot）→ tests/unit/tools/oneshot-tools.test.mjs（`ONESHOT_completed_materializes_lifecycle_work_record_from_real_journal`） |
| SyncDelegate：ReuseScope 内 dedicated Inspector 复用同一 SessionId（不每调新建） | EXEC-026 → tests/unit/session/sync-delegate-runtime.test.mjs（`EXEC_026_sync_delegate_reuses_session_after_full_completion`） |
| SyncDelegate：同 immediate caller ReuseScope 禁止两路并发；第二调用不得在第一 Completion 前进入 provider | EXEC-026 → tests/unit/session/sync-delegate-runtime.test.mjs（`EXEC_026_sync_delegate_second_invoke_while_in_flight_is_rejected`） |
| SyncDelegate：嵌套 DevOps→Coder→Inspector 无 family-root deadlock；serialization key = immediate caller ReuseScope | EXEC-026 |
| SyncDelegate：Dedicated Inspector/Coder = Attached（SyncInspector/SyncCoder）；Work ≠ InternalLeaf Satellite | EXEC-026、HOST-008 → tests/unit/kernel/sync-delegate.test.mjs（`HOST_008_delegateRoleToAttachment_maps_inspector_and_coder`、`HOST_008_SessionOwnership_tryOwner_and_attachmentKind`、`HOST_008_SessionExecutionClass_predicates`） |
| SyncDelegate：owner tier → deterministic delegate tier（fast→fast，deep→deep）；模型不可同 scope 切换 | EXEC-026 → tests/unit/kernel/sync-delegate.test.mjs（`EXEC_026_tierForOwner_is_identity_for_fast_and_deep`、`EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder`）；tests/unit/session/sync-delegate-runtime.test.mjs（`EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names`） |
| SyncDelegate dual-await：`return(A)` 只 resolve Returned；Completion 在 TurnCompleted 后；caller 在两者完成前仍 pending | EXEC-028（SyncDelegate）→ tests/unit/session/sync-delegate-runtime.test.mjs（`EXEC_028_sync_delegate_return_settles_before_completion_keeps_invoke_pending`） |
| SyncDelegate 工具面：Inspector/Coder 无 `agent` 枚举；Coder 必填 `tdd`；统一 `return` **仅** SyncDelegate，**无** StudentTeacher fallthrough | EXEC-026、EXEC-028 → tests/unit/tools/sync-delegate-tools.test.mjs |

## Mailbox / 恢复

| 证明 | 条款 |
|------|------|
| agent Pulse vs PTY Publish 分通道 | EXEC-024 |
| ChildRecovery 分支穷尽与线性序 | EXEC-023 |
| AwaitAgentWithPermit 乱序完成只返回目标 agent；每 join 重新 requirePermit；completion 从 Journal 投影读 | EXEC-023/024 |

代表：`tests/unit/execution/*`（join-v2-wire、handle、fork）。

## Student / Teacher — G3 已删除；证明迁 SyncDelegate

`Role.Student` / `Role.Teacher`、`StudentTeacherRuntime`、`StudentQaStore`、Learn/Compile/SKILL **absent**
（G3 clean-break；AGENT-020…022 / EXEC-027 空缺）。不得再把下表当现行合同。Teacher CE 价值由上节
OneShot / SyncDelegate 与下表 SyncDelegate 行承接。

| 证明 | 期望 | 条款 |
|------|------|------|
| call scope / single-flight | 同 ReuseScope 并发第二调用拒绝；scope 释放后下一调用可开始 | EXEC-026 |
| return completion scope | 无 call/重复/错 Session fail closed；匹配固定 completion 后只完成一个父调用 | EXEC-026、EXEC-028 |
| 静态所有权 | 无 mutable lifecycle record；registry 各一物理 lifetime；DSL gate 拒绝 stage-product PC | EXEC-026、FLOW-006 |
| 正常结束 | `return` 后固定 completion 为 `TurnCompleted`；成功路径无 abort/interrupted；Session 可复用 | EXEC-026、EXEC-028、HOST-008 |
| idle | 同 dedicated Session nudge；预算耗尽失败；普通正文不作答案；payload normalize 比对 `SyncDelegateReturnCompletion` | EXEC-026 |
| cancel | 取消两个 CE await 点；清理失败不伪装成功；不 dispose 成功路径 Session | EXEC-026、HOST-008 |
| CE collapse | Invoke = Returned→Completion 单栈；无 teacherCompletions/CompletionRun/teacherOwners；无 StudentTeacher fallthrough | EXEC-026、FLOW-006 |
| SyncDelegate CE | Acquire(immediate caller ReuseScope)→GetOrCreate→Send→await Returned→await Completion；return 不 dispose dedicated Session | EXEC-026、EXEC-028 → tests/unit/session/sync-delegate-runtime.test.mjs（`EXEC_028_sync_delegate_return_settles_before_completion_keeps_invoke_pending`、`EXEC_026_sync_delegate_reuses_session_after_full_completion`） |
