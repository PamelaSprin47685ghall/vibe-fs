# PROOF — review-assurance

> 每条 WHAT 命题一行落点。类型：`MOVE`（物理移入本包 tests/）、`REUSE`（留在原处，cutover 拆分）、`NEW`（本包新写）、`MECHANISM`（共享 gate）。
> 运行命令：`node --test requirements/review-assurance/tests/<file>`；套件级 `node requirements/verification-system/tests/run.mjs`。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REVIEW-ASSURANCE-001（九条件、禁 same-root、REVISE 中断链） | `tests/witness.test.mjs` → `REVIEW_003_confirmation_requires_two_distinct_attempts`、`REVIEW_003_two_attempts_are_distinct_only_when_run_AND_call_both_differ`、`REVIEW_002_a_REVISE_clears_an_unfinished_confirmation`、`REVIEW_006_the_witness_has_no_authority_root_field_at_all` | MOVE | `node --test requirements/review-assurance/tests/witness.test.mjs` |
| REVIEW-ASSURANCE-002（单次 PERFECT 不足；challenge 因果消费） | `tests/witness.test.mjs` → `REVIEW_003_the_challenge_text_and_its_version_are_pinned`、`REVIEW_003_the_challenge_digest_is_the_digest_of_that_exact_text`、`REVIEW_003_challenge_follows_session_language`、`REVIEW_005_a_first_PERFECT_becomes_a_pending_witness_the_fold_can_produce`、`REVIEW_005_recording_a_PERFECT_verdict_alone_does_not_make_it_pending`、`REVIEW_003_the_witness_carries_the_digests_rather_than_a_boolean` | MOVE | 同上 |
| REVIEW-ASSURANCE-003（attempt identity、同 run 不计数、窗口有界） | `tests/witness.test.mjs` → `REVIEW_004_the_attempt_identity_names_all_five_components`、`REVIEW_004_a_repeated_attempt_is_refused_as_a_duplicate`、`REVIEW_004_the_attempt_window_is_bounded`、`REVIEW_010_the_seal_window_is_bounded` | MOVE | 同上 |
| REVIEW-ASSURANCE-004（confirmed 派生、禁布尔） | `tests/witness.test.mjs` → `REVIEW_005_confirmedReviewer_is_derived_from_the_witness_not_stored_beside_it`、`REVIEW_005_an_empty_guard_is_NoReview_and_satisfies_nothing` | MOVE | 同上 |
| REVIEW-ASSURANCE-005（witness 自包含、无外围 Map） | `tests/witness.test.mjs` → `REVIEW_006_a_confirmed_witness_answers_every_identity_question_inline`、`REVIEW_006_the_witness_has_no_authority_root_field_at_all` | MOVE | 同上 |
| REVIEW-ASSURANCE-006（tree 失效、不删除、新 barrier 新链） | `tests/witness.test.mjs` → `REVIEW_008_a_tree_change_makes_a_confirmed_witness_insufficient`、`REVIEW_008_a_new_barrier_clears_the_pending_challenge_but_keeps_the_witness`、`REVIEW_008_a_new_barrier_invalidates_a_witness_even_when_the_tree_hash_is_unchanged`、`REVIEW_008_a_late_confirmation_cannot_rewind_the_current_barrier`、`REVIEW_008_re_entering_the_same_barrier_changes_nothing`、`REVIEW_008_every_witness_state_reports_the_tree_it_belongs_to` | MOVE | 同上 |
| REVIEW-ASSURANCE-007（seal fail-closed、HOST-010 四条件） | `tests/seal-bind.test.mjs` → `HOST_010_positive_unique_incomplete_assistant_with_matching_parent`、`HOST_010_parent_id_mismatch_is_no_bindable_run`、`HOST_010_completed_assistant_is_no_bindable_run`、`HOST_010_ambiguous_run_when_two_incomplete_children`、`HOST_010_not_latest_run_when_newer_assistant_exists`、`HOST_010_compaction_assistant_is_no_bindable_run`、`HOST_010_summary_true_is_compaction`；`tests/witness.test.mjs` → `REVIEW_010_a_seal_records_the_tool_result_digests_the_run_actually_saw` | MOVE | `node --test requirements/review-assurance/tests/seal-bind.test.mjs` + witness 命令 |
| REVIEW-ASSURANCE-008（VerdictKnown vs ConsumableReview 两段式、禁提前 Concluded） | `tests/consumable-review.test.mjs` → `REVIEW_014_a_durable_verdict_alone_never_makes_the_review_consumable`、`REVIEW_014_only_todo_review_concluded_marks_the_review_consumable` | NEW | `node --test requirements/review-assurance/tests/consumable-review.test.mjs` |
| REVIEW-ASSURANCE-009（同 snapshot、排他 frontier、事件驱动、waiter fail-closed 恢复语义） | `tests/consumable-review.test.mjs` → `REVIEW_018_await_consumable_review_fails_closed_when_the_producer_is_absent`（Pending + Absent + fail-closed，无轮询）；`REVIEW_014_only_..._consumable` 的 `ReviewerRecordFrontier` 断言 | NEW | 同上 |
| REVIEW-ASSURANCE-010（infra ≠ REVISE、不伪 Concluded） | `tests/consumable-review.test.mjs` → `REVIEW_018_concluded_without_accepted_is_rejected`、`REVIEW_018_concluded_without_assignment_is_rejected`、`REVIEW_018_concluded_must_bind_to_its_assignment_identity`、`REVIEW_018_await_consumable_review_fails_closed_when_the_producer_is_absent` | NEW | 同上 |
| REVIEW-ASSURANCE-011（process ≠ terminal witness 代数分离） | `tests/consumable-review.test.mjs` → `REVIEW_020_a_process_revise_is_a_revision_witness_not_a_finality_rejection`；`tests/witness.test.mjs` → `REVIEW_005_recording_a_PERFECT_verdict_alone_does_not_make_it_pending` | NEW + MOVE | 同上 + witness 命令 |
| REVIEW-ASSURANCE-012（request-bounded 证据、frontier 冻结） | `tests/consumable-review.test.mjs` → `REVIEW_014_only_todo_review_concluded_marks_the_review_consumable`（`WorkRecordRef`/`ReviewerRecordFrontier` 断言）；REUSE 交叉见下 | NEW | 同上 |
| REVIEW-ASSURANCE-013（requirement 以 Authority Root 标识、覆盖清除幂等） | `tests/witness.test.mjs` → `REVIEW_007_a_requirement_is_keyed_by_authority_root_and_deduped`、`REVIEW_007_a_confirmed_review_clears_the_requirements_it_covered` | MOVE | witness 命令 |

## 本包拥有的 semantic anchor id

`review-assurance` 在 `scripts/checks/semantic-anchors.mjs` 中无专属 anchor family（reviewer family 归 `review-judgement`）；本包语义由 witness/seal/projection 可执行代数证明，不依赖 prompt anchors。

## REUSE / cutover 拆分计划（SPLIT@cutover）

| 现有测试 | 现状 | 计划 |
|---|---|---|
| `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` | 多 owner（obligation-ledger 主 + review-assurance 交叉）；`TODO-006 T2 prepare succeeds once T1 process review is Concluded` 断言 ConsumableReview 消费门槛 | cutover 时把 `AwaitingConsumableReview` 阻塞/放行断言按 assertion 拆出（review-assurance 侧）或标注 obligation-ledger 消费侧 |
| `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` | `await ConsumableReview failed: fatalInfrastructure` 断言 REVIEW-018 的 infra fail-fast 出口 | cutover 时确认 fail-fast 分类归 `host-boundary`/`crash-reconciliation`，review 侧负边界归本包 |
| `requirements/finality/tests/lifecycle.test.mjs` | GLORY-057（`FinalityUndecided` / undecidable golden bytes）、GLORY-055（REVISE 关 request）、GLORY-060（blessing 顺序重读 tree）为 finality 主 + review-assurance 交叉 | cutover 时按断言拆分；GLORY-059 tree 重读 → 本包 REVIEW-ASSURANCE-006 交叉 |
| `tests/unit/temporal/finality-cohort-law.test.mjs` | roster/graduate 代数 → finality 主 | 无本包 assertion 冲突；challenge/witness 断言如出现按 PROOF 归属拆分 |
| `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs`（已随 obligation-ledger 迁移） | `TODO-006 rejects a conclusion with no matching assignment` 与本包 `REVIEW_018_concluded_without_assignment_is_rejected` 同源 | 保留 obligation-ledger 版本；本包版本以 review 视角断言（fold 拒绝 = 不伪 Concluded） |

## 可红性说明

- `witness.test.mjs` / `seal-bind.test.mjs`（MOVE）：若确认链允许 same-root、attempt 去重失效、witness 失去自包含、tree 失效规则松动、seal 绑定放宽任一条，对应断言即红。
- `consumable-review.test.mjs`（NEW）：若 verdict 单独产生 Concluded、fold 接受无 assignment 的 Concluded、过程 REVISE 产出 Confirmed witness、或 producer 缺失时 waiter 悬挂/伪 Concluded，断言即红。

## 未覆盖与理由

- 终末双 PERFECT 端到端（cohort 全员确认 → blessing）与 REVISE 关 cohort 的 canary 在 `tests/e2e/cases/finality-cohort-law.test.mjs`（`verification-system` e2e 阶梯，cutover 阶段由 lead 处理）。
- `awaitChangeFrom` 事件唤醒的时序行为由 `causal-wait` 包与 `executor-summarize` 行为断言覆盖；本包只证明 review 侧 wait 的 fail-closed 分型。
- record-ready 的 Blogger coverage 前进/`Chronicle` 渲染细节归 `work-record`/`context-compression`；本包只拥有「同 snapshot 判定 + 排他 frontier + 消费门槛」。
