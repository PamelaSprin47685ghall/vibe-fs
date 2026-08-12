# 执行 — 证明

行为：`what/execution.md`。所有权：`shape/execution.md`。程序：`how/execution.md`。

## Handle / PTY

| 证明 | 条款 |
|------|------|
| 四态不可非法回退 | EXEC-009 |
| PTY completion 仅 onExit | EXEC-015 |
| ABORTED 非 agent 终态 | EXEC-020 |
| 终端四动词；provider 无 `pty_id`/`closed`/LastPtyId | EXEC-003、EXEC-030 |

## Commission / Horizon / Join（leak-free）

| 证明 | 期望 | 条款 |
|------|------|------|
| `commission` 成功 wire | 仅自然语言（`# <Byname> has taken your charge.`）；无 `job_id`/worktree/`reused`/agent/role/tier/fallback_peer | EXEC-029、AGENT-015 |
| `horizon()` | 在场名册（Byname/TerminalName）；无 id/status/kind/ordinal/state-machine 词汇；≠ 可创建菜单 | EXEC-005、EXEC-030 |
| Join 批次 | 自然语言 + entry-local WorkRecord/LWR（`includeOpening=false`）；禁止 `status`/`count`/`ordinal`/`kind`/`agent`/`code`/`message` 与字段式 `work_record` DTO | EXEC-004、EXEC-018、EXEC-030 |
| JoinGuard | 优先于其它 Manager completion 分支；Manager 面无 Review Guard | EXEC-016、GLORY-070 |
| 中断 | operator_abort / user_message / DevOps deadline → 自然语言后果，非 error DTO；user wake 不 cancel 资源；completion 优先于 interrupt race；PromptKey continuation/compaction 不唤醒 | EXEC-017、EXEC-025 |
| active JoinAttempt | 收到 user message 会醒；zero-active 消息不唤醒 future join；attempt 建立后、mailbox wait 前的消息仍唤醒当前 attempt | EXEC-017 |
| user_message / Esc | 新 attempt 不继承旧 wake且不取消 child；Esc 返回当前 join 的 operator_abort并取消全部 running sub-session；新 child 的 completion 由后续 join 消费；全程零裸 `#` repair | `tests/e2e/cases/temporal-ownership-unhappy-path.test.mjs`（EXEC-017） |
| blob v2；LegacyFalseAbort 永不 completion | EXEC-021 |
| 假 completion 补偿路径 | EXEC-022 |

## OneShot / SyncDelegate（EXEC-028 / EXEC-026 / EXEC-031）

Residual OneShot（dispose-after，`OneShotAgentTool.run`）与 SyncDelegate（ordinary completion → bounded WorkRecord，dedicated reuse）互斥；下表分列，不混用路径。

G2 Universal Runtime 证据见下表 G2 行。**G2 Product Exit DONE**（2026-08-12 Amendment：唯一 Long Stroke = mock LLM + 本机 OpenCode，即正确 Host 证明）。cancel 留在 unit（不拆唯一 e2e）。

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
| SyncDelegate **无 return**：无 `return` 工具；无 `Returned → Completion` 双 await；ordinary Assistant completion → Host 物化 bounded WorkRecord（`includeOpening=false`）→ 投影 caller | EXEC-028、EXEC-031 → tests/unit/session/sync-delegate-runtime.test.mjs；tests/unit/tools/sync-delegate-tools.test.mjs |
| SyncDelegate 工具面：`inspect` / `establish-behavior` / `repair-behavior`；无 `agent` 枚举、无 `tdd`、无独立 `return`、无 StudentTeacher fallthrough | EXEC-026、EXEC-031 → tests/unit/tools/sync-delegate-tools.test.mjs |
| SyncDelegate WorkRecord：仅四段标题；答案在 Closing report；无 `answer`/`inspector_id`/`coder_id`/`completion_text` magic | EXEC-031、GLORY-025、COMPANION-003 |
| SyncDelegate Charge/ProviderPrompt split | warm-start provider bytes 可 enrich；Casebook `NoteInspectorPrompt` 与 Opening 仍 byte-exact raw Charge；zero-keyword 两者一致；prepare 在 single-flight admission 后 | EXEC-031/032、AGENT-032 |
| warm-start no late injection | 首 prompt 已发送后不再发 hints；Semble failure 只退 raw Charge，不失败 invocation | EXEC-032、AGENT-032 |
| G2 Q1–Q3 same SessionId serial reuse：in-flight 第二调用拒绝；Completion 后复用同一 child，不另建 | EXEC-026 → tests/unit/session/sync-delegate-runtime.test.mjs（`G2_inspector_Q1_Q2_Q3_same_session_serial_reuse`）；Host：tests/e2e/support/long-stroke-oracles.mjs `assertG2InspectorPrefixLaw`（经 `tests/e2e/entry.test.mjs`；same SessionId Q1→Q2→Q3） |
| G2 PREFIX LAW：reused Inspector `ProviderProjection.isAppendOnlyPrefix` + `wireOf`/`sealHolds`（Q1 prefix-of Q2 prefix-of Q3；same model） | ARCH-004、HOST-013 → tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs（`G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix`）；Host：`assertG2InspectorPrefixLaw`。**G2 Product Exit DONE** |
| G2 cancel：owner CancelSession → pending Invoke fail；不另建 child | EXEC-026、HOST-008 → tests/unit/session/sync-delegate-runtime.test.mjs（`G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child`）。unit 层；不拆唯一 Long Stroke |
| G2 owner cascade：owner `session.deleted` → Attached Inspector 级联 | HOST-008 → G6 Long Stroke recursive `session.deleted`（`tests/e2e/entry.test.mjs`） |

## Gate B — Provider Leak Gate

| 证明 | 期望 | 条款 |
|------|------|------|
| 禁泄漏集合 | provider 输出无 SessionId/AgentId/ManagerJobId/PtyId/FissionGroupId、lane_index、worktree、fallback offset、`fast-|deep-` 自称、spool path | EXEC-030；§17 Gate B |
| Join/horizon/commission | 无通用 `status`/`code`/`message` DTO；无 list 式 id 名册 | EXEC-004、EXEC-005、EXEC-029、EXEC-030 |
| `run` / 终端 | 无 estimates×3 / spool_path / LastPtyId；`deadline_seconds`/`output_budget_bytes`/`world_lock` 为合同字段 | EXEC-030；AGENT-013 |
| Distiller | 无固定报告 schema、无 chunk 统计穿过 horizon | §19.30 |

## Mailbox / 恢复

| 证明 | 条款 |
|------|------|
| agent Pulse vs PTY Publish 分通道 | EXEC-024 |
| ChildRecovery 分支穷尽与线性序 | EXEC-023 |
| AwaitAgentWithPermit 乱序完成只返回目标 agent；每 join 重新 requirePermit；completion 从 Journal 投影读 | EXEC-023/024 |

代表：`tests/unit/execution/*`（join-v2-wire、handle、fork）。

## Student / Teacher / return / tdd — 已删除；不得再证旧合同

`Role.Student` / `Role.Teacher`、`StudentTeacherRuntime`、`StudentQaStore`、Learn/Compile/SKILL、`return` 工具、`tdd=red|green`、`list` DTO、dual-await Returned→Completion **absent**
（G3 + GrandRewrite clean-break；AGENT-020…022 / EXEC-027 空缺）。不得再把双 await / tdd / list DTO 行当现行合同。

后继证明以上节 SyncDelegate（EXEC-026/028/031）与 commission/horizon/join leak-free 行为承接：

| 证明 | 期望 | 条款 |
|------|------|------|
| call scope / single-flight | 同 ReuseScope 并发第二调用拒绝；scope 释放后下一调用可开始 | EXEC-026 |
| completion 单栈 | ordinary completion 后只完成一个父调用；无 return/重复/错 Session fallthrough | EXEC-026、EXEC-031 |
| 静态所有权 | 无 mutable lifecycle record；registry 各一物理 lifetime；DSL gate 拒绝 stage-product PC | EXEC-026、FLOW-006 |
| 正常结束 | Assistant completion → WorkRecord 投影；成功路径无 abort/interrupted；Session 可复用 | EXEC-026、EXEC-031、HOST-008 |
| idle | 同 dedicated Session nudge；预算耗尽失败；普通正文不作答案；无 `SyncDelegateReturnCompletion` magic | EXEC-026 |
| cancel | 取消 pending Invoke；清理失败不伪装成功；不 dispose 成功路径 Session | EXEC-026、HOST-008 |
| CE collapse | Invoke = Completion 单栈；无 teacherCompletions/CompletionRun/teacherOwners；无 StudentTeacher/`return` fallthrough | EXEC-026、FLOW-006、EXEC-031 |
