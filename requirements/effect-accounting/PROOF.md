# PROOF —— 测试落点表（effect-accounting）

## 运行方式

```bash
node --test requirements/effect-accounting/tests/effect-facts.test.mjs   # 本包 NEW
node --test requirements/effect-accounting/tests/reconcile-before-retry.test.mjs   # 本包 NEW
node --test requirements/effect-accounting/tests/write-unknown-explicit.test.mjs   # 本包 NEW
node --test requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs   # 本包 NEW
node requirements/verification-system/tests/run.mjs                                                  # 全量
```

本包 4 个 NEW 测试文件（effect-facts / reconcile-before-retry / write-unknown-explicit /
todo-accepted-precise-ref）单独跑绿；其余落点 REUSE 原位/跨包。

## 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EFFECT-ACCOUNTING-001 | `requirements/effect-accounting/tests/effect-facts.test.mjs::worktree_requested_created_are_distinct_typed_states_not_one_bool`（Requested≠Created 两个 typed 状态）+ `::typed_effect_facts_replace_the_generic_durable_effect_union`（WorktreeCreateRequested/WorktreeCreated/PublishClaimed 是 typed case） | NEW | `node --test requirements/effect-accounting/tests/effect-facts.test.mjs` |
| EFFECT-ACCOUNTING-002 | `requirements/effect-accounting/tests/join-missing-final-report.test.mjs::EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed`、`EXEC_join_empty_Completed_keeps_run_pending_not_failed`（invalid terminal 仍是 observation-only）、`EXEC_join_interaction_repair_exhausted_settles_the_run`（有界 repair 已耗尽后明确 terminal failure 必须 settle）、`EXEC_join_real_Failed_still_claims_run` + `requirements/change-integration/tests/job.test.mjs::PERSIST_009_request_alone_is_not_created` | REUSE | `node --test requirements/effect-accounting/tests/join-missing-final-report.test.mjs requirements/change-integration/tests/job.test.mjs` |
| EFFECT-ACCOUNTING-003 | `requirements/change-integration/tests/job.test.mjs::PERSIST_009_worktree_request_then_created_marks_identity_created`（Requested→Created 顺序事实）+ `requirements/change-integration/tests/runtime.test.mjs::ORCH_007_NeedsReview_preserves_the_active_worktree`（worktree 保留断言；PERSIST-009 顺序注释见文件头） | REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-004 | `requirements/change-integration/tests/job.test.mjs::PERSIST_009_duplicate_request_after_created_does_not_regress_to_requested`（Accepted→Requested 回归被拒）+ `PERSIST_009_duplicate_created_is_idempotent` + `requirements/effect-accounting/tests/effect-facts.test.mjs::worktree_requested_created_are_distinct_typed_states_not_one_bool`（helper 路径不回归） | REUSE + NEW | 各文件 `node --test` |
| EFFECT-ACCOUNTING-005 | `requirements/effect-accounting/tests/reconcile-before-retry.test.mjs`：`WHAT[EFFECT-ACCOUNTING-005] requested_only_without_physical_evidence_stays_pending_not_blind_retry`、`WHAT[EFFECT-ACCOUNTING-005] outcome_unknown_without_physical_evidence_never_becomes_terminal`、`WHAT[EFFECT-ACCOUNTING-005] terminal_issued_only_after_proven_physical_evidence`（先证后重试律：无物理证据 → pending/RecoveryIncomplete；只有 proven 证据才产生 terminal）+ REUSE `requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs::THEOREM_publish_claimed_three_branch_order_is_fixed`（先查物理 head）+ `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs::TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission`（证据门决定等待/重试） | NEW + REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-006 | `requirements/effect-accounting/tests/write-unknown-explicit.test.mjs`：`WHAT[EFFECT-ACCOUNTING-006] write_after_dispose_returns_explicit_unknown_not_pretended_commit`（writer dispose 后 append → WriteUnknown/CommitUnknown 显式分型，不假装 committed、不盲重试）+ REUSE `requirements/change-integration/tests/job.test.mjs::PERSIST_009_request_alone_is_not_created` | NEW + REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-007 | `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`：`P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal`、`P0_RECOVERY_JOIN_001_aborted_observed_never_joinable`、`P0_RECOVERY_JOIN_001_tryFromDurableCompleted_rejects_cancelled`、`P0_RECOVERY_JOIN_001_joinable_completion_has_no_fromAborted_export`、`P0_RECOVERY_JOIN_001_aborted_times_n_projection_unchanged`（在 crash-reconciliation 包测试内，见 SPLIT 清单）+ `requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs::P0_RECOVERY_JOIN_GATE_positive_clean_break_shapes_present`（completion-blob-schema-v2 / legacy-false-abort-decode / false-completion-rejected-fact 正向规则）+ `P0_RECOVERY_JOIN_GATE_production_sources_are_green` | REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-008 | `requirements/change-integration/tests/job.test.mjs::PERSIST_009_*`（5 锚点：Worktree typed 对）+ `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs::C5_entry_commit_records_receipt_and_clears_open_request`（Blogger Request→Entry）+ `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs::TODO-012 persists typed prepared identity through AgentJournal and EventStore boot`（Todo Prepared→Accepted） | REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-009 | `requirements/effect-accounting/tests/effect-facts.test.mjs::publish_claimed_recovery_three_branch_order_is_fixed`（BackfillPublished/AttemptPublish/RebaseAndReviewAgain/FailClosed 四态）+ `::publish_claim_without_durable_rebase_witness_is_rejected` + `requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs::THEOREM_publish_claimed_three_branch_order_is_fixed`、`THEOREM_publish_claimed_without_prior_rebase_is_rejected` | NEW + REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-010 | `requirements/durable-events/tests/fact-codec.test.mjs::PERSIST_005_pre050_marker_refuses_with_migration_message`（DurableEffectRequested/Accepted 拒绝）+ `requirements/effect-accounting/tests/effect-facts.test.mjs::typed_effect_facts_replace_the_generic_durable_effect_union` | REUSE + NEW | 各文件 `node --test` |
| EFFECT-ACCOUNTING-011 | `requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs`：`WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected`、`WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption`、`WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately`（PreparedFactRef 精确指名；缺失/失配拒绝；Accepted 后 Current 立即切换）+ REUSE `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs::TODO-004 rejects Accepted when it names another Prepared envelope`（PreparedFactRef 精确指名）+ `TODO-005 Accepted supersedes Current immediately and REVISE conclusion cannot roll it back`（Accepted 不折回）+ `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs::TODO-012 persists typed prepared identity through AgentJournal and EventStore boot` | NEW + REUSE | 各文件 `node --test` |
| EFFECT-ACCOUNTING-012 | `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`：`TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission`、`TODO-006 T2 prepare succeeds once T1 process review is Concluded (lag-1 wait resolves, no invalidOp)`、`TODO-005 REVISE is feedback only: next checkpoint sees the report and Current never rolls back` + `requirements/effect-accounting/tests/effect-facts.test.mjs::publish_claim_without_durable_rebase_witness_is_rejected` | REUSE + NEW | 各文件 `node --test` |

## 统计

- 命题 12 条；落点行 12；NEW 4 文件（`effect-facts.test.mjs`、`reconcile-before-retry.test.mjs`、
  `write-unknown-explicit.test.mjs`、`todo-accepted-precise-ref.test.mjs`）+ REUSE 11 个现有
  文件（`requirements/effect-accounting/tests/join-missing-final-report.test.mjs`、
  `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`、
  `requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs`、
  `requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs`、
  `requirements/change-integration/tests/runtime.test.mjs`、
  `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`、
  `requirements/durable-events/tests/fact-codec.test.mjs`、`requirements/effect-accounting/tests/blogger-request-materialized.test.mjs`、
  `requirements/change-integration/tests/job.test.mjs`、
  `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs`、
  `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs`）。
- GAP：0。

## SPLIT@cutover 清单

1. `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`：单-owner（本包）但**暂不物理
   移动**——`requirements/structured-workflow/PROOF.md`（STRUCTURED-WORKFLOW-015）已按
   当前路径引用其落点 token；`requirements/crash-reconciliation/PROOF.md` 亦在
   SPLIT@cutover 清单中点名「effect-accounting owner」。cutover 时移入本包 `tests/` 并
   同步更新 structured-workflow 的 PROOF 落点路径。
2. `requirements/effect-accounting/tests/p0-recovery-join-clean-break.test.mjs` + `scripts/checks/p0-recovery-join.mjs`：
   共享 checker；按规则 id 拆分——A 组（aborted≠terminal）归本包，B 组（recovery）归
   `crash-reconciliation`。cutover 时拆成两个 oracle，各自留在 owner 包。
3. `requirements/crash-reconciliation/tests/join-aborted-race.test.mjs` / `join-recovery-crash-matrix.test.mjs`：
   已由 `crash-reconciliation` 迁移；其 aborted≠terminal 断言与 EA-007 交叉，按
   「恢复矩阵归 crash、false-finality 律归本包」互不复制命题。
4. `requirements/change-integration/tests/job.test.mjs` PERSIST-009 小节、
   `requirements/change-integration/tests/runtime.test.mjs` 顺序断言：`change-integration` 已在其
   SPLIT@cutover 清单中把 PERSIST-009 事实顺序断言划归本包；cutover 时物理拆分。
5. `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`：lag-1 证据门断言
   （TODO-006/005 的 effect 半边）归本包；membrane 的 Host/snapshot 面归各自 owner。

## 本包拥有的 semantic anchor id

空。`scripts/checks/semantic-anchors.mjs` 无 effect-accounting 语义 ID。
