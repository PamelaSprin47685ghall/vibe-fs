# effect-accounting — HOW

## 架构机制与副作用状态投影

`effect-accounting` 统一管理跨外部系统的副作用生命周期：

1. **类型化事实流与编排时序**：
   - 业务流程严格执行“先意图事实、后物理调用、再确认事实”的执行顺序。
   - 状态投影将未匹配确认事实的记录维护为 `Requested`，匹配成功后跃迁至 `Created` / `Published` / `Accepted` 并永久锁定。

2. **PublishClaim 三分支判定**：
   `classifyPublishClaim` 在处理发布未决事实时，直接比对 Git 目标头部的物理快照：
   - `TargetHead = RebasedCommit` → 物理操作已完成，补发 `Published` 事实；
   - `TargetHead = ExpectedHead` → 分支未被篡改，执行原子推进；
   - 其它情况 → 目标引用已被并发修改，作废当前 Claim 并触发重试链路。

3. **未知结局（Outcome-Unknown）捕获与门禁**：
   底层存储写失败时抛出 `WriteUnknown`，上层门禁关闭后续调用准入并保留现场，由统一的崩溃对账机制根据外部物理见证裁决，禁止同进程进行盲目的就地重发。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EFFECT-ACCOUNTING-001 | `requirements/effect-accounting/tests/effect-facts.test.mjs::WHAT[EFFECT-ACCOUNTING-001] worktree_requested_created_are_distinct_typed_states_not_one_bool` |
| EFFECT-ACCOUNTING-002 | `requirements/effect-accounting/tests/join-missing-final-report.test.mjs::WHAT[EFFECT-ACCOUNTING-002] EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed`；`requirements/effect-accounting/tests/join-missing-final-report.test.mjs::WHAT[EFFECT-ACCOUNTING-002] EXEC_join_empty_Completed_keeps_run_pending_not_failed`；`requirements/effect-accounting/tests/join-missing-final-report.test.mjs::WHAT[EFFECT-ACCOUNTING-002] EXEC_join_interaction_repair_exhausted_settles_the_run`；`requirements/effect-accounting/tests/join-missing-final-report.test.mjs::WHAT[EFFECT-ACCOUNTING-002] EXEC_join_real_Failed_still_claims_run` |
| EFFECT-ACCOUNTING-003 | `requirements/effect-accounting/tests/runtime-persist-order.test.mjs::WHAT[EFFECT-ACCOUNTING-003] PERSIST_009_fork_appends_worktree_request_created_then_manager_job` |
| EFFECT-ACCOUNTING-004 | `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-004] C5_same_request_materialize_is_idempotent` |
| EFFECT-ACCOUNTING-005 | `requirements/effect-accounting/tests/reconcile-before-retry.test.mjs::WHAT[EFFECT-ACCOUNTING-005] requested_only_without_physical_evidence_stays_pending_not_blind_retry`；`requirements/effect-accounting/tests/reconcile-before-retry.test.mjs::WHAT[EFFECT-ACCOUNTING-005] outcome_unknown_without_physical_evidence_never_becomes_terminal`；`requirements/effect-accounting/tests/reconcile-before-retry.test.mjs::WHAT[EFFECT-ACCOUNTING-005] terminal_issued_only_after_proven_physical_evidence` |
| EFFECT-ACCOUNTING-006 | `requirements/effect-accounting/tests/write-unknown-explicit.test.mjs::WHAT[EFFECT-ACCOUNTING-006] write_after_dispose_returns_explicit_unknown_not_pretended_commit` |
| EFFECT-ACCOUNTING-007 | `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_observed_never_joinable`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_with_session_active_is_recovered_active`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_mid_turn_snapshot_active_with_session_active_is_recovered_active`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_true_unreadable_is_recovery_incomplete`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_tryFromProvenTerminal_rejects_empty_body`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_tryFromDurableCompleted_rejects_cancelled`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_joinable_completion_has_no_fromAborted_export`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_proven_terminal_then_joinable`；`requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs::WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_durable_completed_awaiting_join_is_joinable` |
| EFFECT-ACCOUNTING-008 | `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_materialize_opens_request_queryable_by_blogger`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_entry_commit_records_receipt_and_clears_open_request`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_same_provider_run_cannot_be_both_entry_and_squash`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_materialize_prompt_key_fill_in_after_send`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_materialize_prompt_key_cannot_rebind`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_duplicate_request_materialize_different_context_rejected`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_abandon_clears_open_request`；`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::WHAT[EFFECT-ACCOUNTING-008] C5_request_id_cannot_rebind_to_different_provider_run` |
| EFFECT-ACCOUNTING-009 | `requirements/effect-accounting/tests/effect-facts.test.mjs::WHAT[EFFECT-ACCOUNTING-009] publish_claimed_recovery_three_branch_order_is_fixed` |
| EFFECT-ACCOUNTING-010 | `requirements/effect-accounting/tests/pre050-effect-marker.test.mjs::WHAT[EFFECT-ACCOUNTING-010] PERSIST_005_pre050_marker_refuses_with_migration_message` |
| EFFECT-ACCOUNTING-011 | `requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs::WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected`；`requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs::WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption`；`requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs::WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately` |
| EFFECT-ACCOUNTING-012 | `requirements/effect-accounting/tests/effect-facts.test.mjs::WHAT[EFFECT-ACCOUNTING-012] publish_claim_without_durable_rebase_witness_is_rejected` |
