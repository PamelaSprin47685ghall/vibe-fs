# PROOF — review-assurance

> 运行：`node --test requirements/review-assurance/tests/*.test.mjs`；权威全量：`node requirements/verification-system/tests/run.mjs`。

## 落点表

| 命题 | 可执行 proof |
|---|---|
| REVIEW-ASSURANCE-001 | `tests/witness.test.mjs` → `REVIEW_003_two_attempts_require_distinct_run_and_call`、`REVIEW_003_confirmation_still_requires_distinct_attempts`；`tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_003_reverify_is_the_direct_ce_temporal_owner` |
| REVIEW-ASSURANCE-002 | `tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_001_finality_dual_perfect_has_no_persisted_program_position`；`tests/host-reverify.test.mjs` → `HOST_reverify_terminal_before_first_judgement_fails_closed_without_hanging`、`HOST_reverify_terminal_before_second_judgement_fails_closed_without_hanging`、typed challenge dual-PERFECT Host case；`tests/witness.test.mjs` → `REVIEW_005_single_PERFECT_is_not_a_durable_pending_witness`、`REVIEW_003_confirmation_requires_exact_challenge_physical_identity`、`REVIEW_003_challenge_text_is_presentation_only_and_localized`；`tests/shared-state.test.mjs` → `SHARED_judgement_rendezvous_is_physical_not_a_business_stage` |
| REVIEW-ASSURANCE-003 | `tests/witness.test.mjs` → `REVIEW_004_attempt_identity_names_all_five_components`、`REVIEW_004_duplicate_attempt_is_refused` |
| REVIEW-ASSURANCE-004 | `tests/witness.test.mjs` → `REVIEW_005_confirmedReviewer_is_derived_from_witness` |
| REVIEW-ASSURANCE-005 | `tests/witness.test.mjs` → `REVIEW_006_confirmed_witness_is_self_contained_typed_evidence` |
| REVIEW-ASSURANCE-006 | `tests/witness.test.mjs` → `REVIEW_008_tree_change_invalidates_completed_witness`、`REVIEW_008_new_barrier_requires_a_fresh_completed_CE`、`REVIEW_008_late_old_confirmation_cannot_satisfy_current_barrier`；`tests/review-guard.test.mjs` → `RVGD_openBarrier_is_the_shared_review_barrier_writer`；`tests/host-reverify.test.mjs` → `HOST_reverify_rejects_completed_terminal_with_unknown_role` |
| REVIEW-ASSURANCE-007 | `tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_002_finality_confirmation_never_parses_provider_text_or_seals_it`；`tests/shared-state.test.mjs` → `SHARED_finality_has_no_pending_provider_input_seal_registry`；`tests/seal-bind.test.mjs` → HOST-010 generic ProviderRunBinding fail-closed cases |
| REVIEW-ASSURANCE-008 | `tests/consumable-review.test.mjs` → `REVIEW_014_a_durable_verdict_alone_never_makes_the_review_consumable`、`REVIEW_014_only_todo_review_concluded_marks_the_review_consumable`、`REVIEW_017_process_verdict_identity_comes_from_the_integrated_projection_not_a_judge_tool_call_trace`; `tests/review-guard.test.mjs` → `review_journal_rejects_forged_verdict_role_ownership_and_completion_labels` |
| REVIEW-ASSURANCE-009 | `tests/consumable-review.test.mjs` → `REVIEW_018_producer_presence_is_present_when_reviewer_handle_is_CompletedAwaitingJoin`、`REVIEW_017 durable verdict keeps record-ready producer present after the reviewer work-unit is Retired`；源码结构 = sample revision → tryConclude → presence → awaitChangeFrom sampled revision |
| REVIEW-ASSURANCE-010 | `tests/witness.test.mjs` → `REVIEW_002_REVISE_is_a_completed_revision_fact`；`tests/consumable-review.test.mjs` → concluded/assignment/producer fail-closed cases；`tests/review-guard.test.mjs` → process missing-judge repair fail-closed cases；`tests/host-reverify.test.mjs` → `HOST_reverify_rejects_completed_terminal_with_unknown_role`；`tests/finality-direct-ce-contract.test.mjs` → `REVIEW_CE_004_transient_reviewer_failures_remain_in_provider_recovery` |
| REVIEW-ASSURANCE-011 | `tests/consumable-review.test.mjs` → `REVIEW_020_a_process_revise_is_a_revision_witness_not_a_finality_rejection`；`tests/witness.test.mjs` → `REVIEW_005_single_PERFECT_is_not_a_durable_pending_witness` |
| REVIEW-ASSURANCE-012 | `tests/consumable-review.test.mjs` → `REVIEW_016_the_concluded_review_evidence_is_bounded_to_the_frozen_request_frontier`；交叉 `requirements/obligation-ledger/tests/magic-todo-after.test.mjs` 首个 T1 start 不受 post-T1 global floor 反向影响，`magic-todo-projection.test.mjs` concluded coverage 采用 assigned exact Manager frontier 而非 Prepared provisional frontier |
| REVIEW-ASSURANCE-013 | `tests/review-requirement.test.mjs` → `requirement identity is the Authority Root and duplicate roots collapse`、`confirmation clears its covered batch but replay cannot clear a later requirement` |

## 行为级 canary

`requirements/verification-system/tests/e2e/scenarios/long-stroke.toml` 的 Finality reviewer 段包含两个独立 provider requests：第一次 `judge(PERFECT)`，随后 skeptical challenge continuation，再次 `judge(PERFECT)`。权威 e2e 必须证明第二次直接形成 completed witness；不存在第三次“再尝试让状态机认账”的路径。

## 可红性

- 把 first PERFECT 恢复成 durable pending state → `REVIEW_CE_001` 红。
- Finality control path重新引用 provider wire/text/digest → `REVIEW_CE_002` 红。
- `Reverify.fs` 重新读 Journal projection 决定下一步 → `REVIEW_CE_003` 红。
- challenge physical id 与 second judgement physical id 不同仍确认 → witness physical-causality case 红。
- 同 run / 同 call 仍确认 → distinct-attempt cases 红。
- 新 barrier/tree 复用旧 witness → REVIEW-008 cases 红。
- process verdict 直接变 ConsumableReview 或伪 Finality confirmation → consumable-review cases 红。
- Finality Reviewer 的非 `TurnCompleted` 再次被 Reviewer CE 直接消费、绕过 ordinary provider recovery → `REVIEW_CE_004` 红。
