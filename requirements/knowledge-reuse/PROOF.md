# PROOF — 测试落点表

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包）/ `REUSE`（留在原处，记录锚点与 cutover 计划）。
> 单跑：`WANXIANGSHU_PROVIDER_LANGUAGE=en node --test requirements/knowledge-reuse/tests/<file>`。全套：`node requirements/verification-system/tests/run.mjs`。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| `KNOWLEDGE-REUSE-001` | `casebook-store.test.mjs` → `CASE004_005_workflow_archive_fetch_closed_loop_reads_Current_only`（archive→fetch 闭环、无 commit history 引入）；`fetch-tool.test.mjs` → `CASE009_fetch_never_writes_the_subject`（不改 subject worktree） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/fetch-tool.test.mjs` |
| `KNOWLEDGE-REUSE-002` | `lifecycle-wiring.test.mjs` → `lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once`（Q/A 进入 Case）；`fetch-tool.test.mjs` → `CASE004_fetch_returns_exact_canonical_a`（返回 exact canonical A）；`casebook-domain.test.mjs` → `CASE002_fold_captured_and_refreshed_keeps_qa_verbatim` | MOVE | `node --test requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs requirements/knowledge-reuse/tests/fetch-tool.test.mjs requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| `KNOWLEDGE-REUSE-003` | `casebook-capture.test.mjs` → 全部 6 个 test（`CASE003_read_capture_is_typed_and_hashed` / `CASE003_glob_capture_parses_rendered_paths` / `CASE003_grep_capture_keeps_match_lines` / `CASE003_unknown_tool_yields_nothing` / `S63_executor_reading_positives` / `S63_executor_reading_negatives_skip_safely`）；`casebook-domain.test.mjs` → `CASE003_normalize_dedupes_and_orders_observations` | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-capture.test.mjs requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| `KNOWLEDGE-REUSE-004` | `fetch-tool.test.mjs` → `CASE004_fetch_uses_shelfmark_and_replays_before_refreshing`；`casebook-store.test.mjs` → `CASE004_refresh_and_needsRefresh_replay_the_same_Current`；`casebook-domain.test.mjs` → `CASE004_classifyReplay_fresh_only_on_exact_normalized_equality` | MOVE | `node --test requirements/knowledge-reuse/tests/fetch-tool.test.mjs requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| `KNOWLEDGE-REUSE-005` | `casebook-store.test.mjs` → `CASE004_005_freshness_check_is_hint_not_proof_reads_Current_only`（no-delta 是 hint 非 proof）；`bookkeeper-session.test.mjs` → `CASE006_missing_session_port_keeps_old_case`（维护失败 ≠ fetch 失败，返回旧 A） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/bookkeeper-session.test.mjs` |
| `KNOWLEDGE-REUSE-006` | `js-bookkeeper-tool.test.mjs` → 全部 6 个 test（`js_bookkeeper_surface_is_program_only_and_has_case_sdk` / `js_bookkeeper_program_reshapes_question_and_answer_atomically` / `js_bookkeeper_zero_mutation_is_legal` / `js_bookkeeper_duplicate_set_rolls_back_the_whole_program` / `js_bookkeeper_program_failure_rolls_back_staged_mutation` / `js_bookkeeper_unbound_session_cannot_change_a_case`）；`edit-qa-tool.test.mjs` → `CASE006_bookkeeper_provider_contract_is_one_program`（edit-qa 非法）；`bookkeeper-{mechanical,session,synthesis}.test.mjs` → CaseRefresh 全链路 | MOVE | `node --test requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs requirements/knowledge-reuse/tests/edit-qa-tool.test.mjs requirements/knowledge-reuse/tests/bookkeeper-mechanical.test.mjs requirements/knowledge-reuse/tests/bookkeeper-session.test.mjs requirements/knowledge-reuse/tests/bookkeeper-synthesis.test.mjs` |
| `KNOWLEDGE-REUSE-007` | `casebook-store.test.mjs` → `CASE007_captured_refreshed_round_trip_through_integrator_Current` / `CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan` / `CASE007_store_has_no_loadEvents_project_or_history_reader`；交叉 REUSE `scripts/checks/unified-store-gate.mjs`（禁 feature store） | MOVE + REUSE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs`；`node scripts/checks/unified-store-gate.mjs` |
| `KNOWLEDGE-REUSE-008` | `casebook-domain.test.mjs` → `CASE008_lru_evict_keeps_most_recently_accessed` / `CASE008_fold_accessed_and_evicted_derives_access_order`；交叉 `casebook-store.test.mjs` → `CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan`（evict tombstone 事件往返）；`lifecycle-wiring.test.mjs` → `lifecycle_touchAccess_and_touchCaseAccess_advance_integrated_access_order`（last_access 派生） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-domain.test.mjs requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs` |
| `KNOWLEDGE-REUSE-009` | `casebook-store.test.mjs` → `CASE009_marker_gates_the_surface`（双门）；`lifecycle-wiring.test.mjs` → `lifecycle_disabled_marker_skips_publication` | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs` |
| `KNOWLEDGE-REUSE-010` | `lifecycle-wiring.test.mjs` → `lifecycle_cleanupInspector_never_publishes_case` / `lifecycle_missing_answer_is_noop_finalize`（unexpected delete 仅 cleanup）；`universal-loop.test.mjs` → `G6_G_universal_loop_archive_finalize_fetch` / `G6_G_lifecycle_note_finalize_fetch_and_cleanup` / `G6_G_cancel_session_cleanup_no_publication`；`g6-host-reuse-finalize.test.mjs` → `G6_G_host_reusable_inspector_one_finalize_then_cold_fetch`（exactly-one finalize）；`g6-inspector-tool-finalize-fetch.test.mjs` → `G6_inspector_tool_sync_delegate_lifecycle_bookkeeper_fetch`；`casebook-store.test.mjs` → `CASE010_finalize_is_exactly_once_per_scope` | MOVE | `node --test requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs requirements/knowledge-reuse/tests/universal-loop.test.mjs requirements/knowledge-reuse/tests/g6-host-reuse-finalize.test.mjs requirements/knowledge-reuse/tests/g6-inspector-tool-finalize-fetch.test.mjs requirements/knowledge-reuse/tests/casebook-store.test.mjs` |
| `KNOWLEDGE-REUSE-011` | `fetch-tool.test.mjs` → `CASE011_fetch_single_flight_serializes_same_shelfmark`（same-worktree 串行化）；交叉 REUSE `requirements/durable-convergence/tests/event-store-merge.test.mjs` + `event-store-converge.test.mjs`（set union / DomainConflict / 禁 LWW 的物理 substrate → `durable-convergence`） | MOVE + REUSE | `node --test requirements/knowledge-reuse/tests/fetch-tool.test.mjs`；`node --test requirements/durable-convergence/tests/event-store-merge.test.mjs requirements/durable-convergence/tests/event-store-converge.test.mjs` |
| `KNOWLEDGE-REUSE-012` | `casebook-index.test.mjs` → 全部 4 个 test（`CASEBOOK_index_exposes_shelfmark_and_canonical_question_only` / `CASEBOOK_shelfmark_is_stable_and_not_the_session_identity` / `CASEBOOK_invalidate_then_refresh_advances_epoch` / `CASEBOOK_visible_set_change_advances_epoch`） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-index.test.mjs` |

## 统计

```text
WHAT 命题：12（KNOWLEDGE-REUSE-001..012）
落点：   MOVE 12 个命题（10 个纯 MOVE + 007/011 带 REUSE 交叉）
        REUSE 2（scripts/checks/unified-store-gate.mjs、tests/unit/persist/event-store-{merge,converge}.test.mjs → durable-convergence）
        NEW  0（14 个现有文件覆盖全部命题，无 GAP）
GAP：    0
```

## 移动文件清单（源 → 目标，均单独跑绿）

| 源（tests/unit/casebook/） | 目标（requirements/knowledge-reuse/tests/） | 断言数 | 单跑结果 |
|---|---|---|---|
| `bookkeeper-mechanical.test.mjs` | 同名 | 6 pass | 绿 |
| `bookkeeper-session.test.mjs` | 同名 | 3 pass | 绿 |
| `bookkeeper-synthesis.test.mjs` | 同名 | 7 pass | 绿 |
| `casebook-capture.test.mjs` | 同名 | 6 pass | 绿 |
| `casebook-domain.test.mjs` | 同名 | 4 pass | 绿 |
| `casebook-index.test.mjs` | 同名 | 4 pass | 绿 |
| `casebook-store.test.mjs` | 同名 | 9 pass | 绿 |
| `edit-qa-tool.test.mjs` | 同名 | 1 pass | 绿 |
| `fetch-tool.test.mjs` | 同名 | 6 pass | 绿 |
| `g6-host-reuse-finalize.test.mjs` | 同名 | 4 pass | 绿 |
| `g6-inspector-tool-finalize-fetch.test.mjs` | 同名 | 1 pass | 绿 |
| `js-bookkeeper-tool.test.mjs` | 同名 | 6 pass | 绿 |
| `lifecycle-wiring.test.mjs` | 同名 | 8 pass | 绿 |
| `universal-loop.test.mjs` | 同名 | 6 pass | 绿 |

适配说明：`../support/domain.mjs` 深度修正为 `../../../requirements/verification-system/tests/support/domain.mjs`；包内互导（`./bookkeeper-session.test.mjs` 作为 helper 被 6 个文件引用）保持原样——同一目录内相对引用随族迁移不变。全部文件无 `dist/fable_modules` 直接 import（test-boundary 门不受影响）。

## semantic anchor 归属（semantic-anchors.mjs）

本包拥有 `ROLE_SEMANTIC_ANCHORS.bookkeeper` 的全部 5 个 anchor id：

```text
reusable-knowledge / one-case / question-may-change / zero-mutation / transcript-is-data
```

（`scripts/checks/semantic-anchors.mjs` 的 `bookkeeper` 组：Role Law 散文锚点，锁 Bookkeeper 的复用边界。inspector 组归 `repository-investigation`。）

## SPLIT@cutover 计划（现有测试的 owner 拆分）

| 现有文件 | 当前 owner 混合 | cutover 动作 |
|---|---|---|
| `requirements/durable-convergence/tests/event-store-merge.test.mjs` / `event-store-converge.test.mjs` | `durable-convergence`（general set union / DomainConflict 物理律）+ 本包（Case 对象冲突语义） | 留在 `durable-convergence`；本包 PROOF 只引用（REUSE）；不物理移动 |
| `tests/unit/casebook/`（已移入本包） | 全部断言归本包（PROOF-MAP：casebook KEEP knowledge-reuse） | 已 **MOVE** 完成；`tests/unit/casebook/` 目录随 cutover 删除 |
| `g6-host-reuse-finalize.test.mjs` / `g6-inspector-tool-finalize-fetch.test.mjs` / `universal-loop.test.mjs` | 本包（Casebook lifecycle/fetch）+ `managed-session-lifecycle`（ReuseScope）/`delegation`（SyncDelegate）交叉 | **SPLIT**（如需）：ReuseScope close / SyncDelegate 生命周期断言归对应包；Casebook finalize/fetch 断言留本包 |
