# prefix-stability — PROOF（测试落点表）

## 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PREFIX-STABILITY-001（同 epoch append-only prefix law） | `tests/prefix-append-only-law.test.mjs`（NEW）：`PREFIX_STABILITY_append_only_law_holds_within_one_epoch`、`PREFIX_STABILITY_modified_historical_bytes_break_the_law`；REUSE：`tests/unit/host/pair-thought-anchored.test.mjs` `H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix`、`H13_08_n_round_property_prefix_law_holds`、`tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs`（PREFIX LAW on reused child） | NEW + REUSE | `node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-002（冷边界三证据源） | `tests/prefix-epoch.test.mjs`：`COMPANION_009_initial_epoch_has_no_snapshot`、`CTX_012_successful_probe_promotes_its_candidate_verbatim`、`HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch`、`CTX_012_probe_capability_returns_after_a_reanchor` | MOVE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-003（candidate ≠ committed） | `tests/prefix-epoch.test.mjs`：`CTX_010_a_failed_probe_leaves_no_trace_to_undo`、`CTX_010_a_replayed_rebase_is_reported_as_stale`；REUSE：`tests/unit/context/attempt-plan.test.mjs` `CTX_010_a_discarded_probe_leaves_the_committed_epoch_in_place`、`CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way` | MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-004（ActivePrefixEpoch 唯一 SSOT） | `tests/prefix-epoch-todo-checkpoint.test.mjs`（NEW）：`PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract`；`tests/prefix-epoch.test.mjs`：`PERSIST_010_rebase_epoch_must_be_the_successor`、`CTX_011_an_identical_candidate_is_reported_as_not_new`；REUSE：`tests/unit/context/fold-context-recovery.test.mjs` `CTX_012_rebase_folds_into_the_prefix_projection_only` | NEW + MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch-todo-checkpoint.test.mjs requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-005（seal 后不因 provider 成败回滚） | `tests/prefix-epoch.test.mjs`：`CTX_012_a_replayed_rebase_is_reported_as_stale`；`tests/prefix-epoch-todo-checkpoint.test.mjs`（NEW）：`PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract`（SolvingProviderRun=None）；REUSE：`tests/unit/context/fold-context-recovery.test.mjs` `CTX_012_a_replayed_rebase_is_absorbed_so_crash_recovery_is_idempotent` | MOVE + NEW + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-006（ContextReanchored 重锚语义） | `tests/prefix-epoch.test.mjs`：`HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch`、`HOST_006_reanchoring_a_session_that_never_promoted_still_advances`、`HOST_006_the_same_compaction_is_never_reanchored_twice`、`HOST_006_a_recorded_compaction_stays_refused_after_the_epoch_moves_on`、`HOST_006_a_genuinely_new_compaction_reanchors_again` | MOVE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-007（system prompt byte-identical） | REUSE：`tests/unit/invariants/prompt-stability.test.mjs` `PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes`、`PROMPT_STABILITY_t1_review_reanchor_keep_system_prompt_id_persona_and_catalog_bytes` | REUSE | `node --test tests/unit/invariants/prompt-stability.test.mjs` |
| PREFIX-STABILITY-008（FrozenRecordPrefix 明确标记 + 冻结） | 跨包：`tests/companion-projection.test.mjs`（context-compression 包）`COMPANION_010_memory_block_marks_the_body_as_low_trust_context`、`COMPANION_009_the_same_epoch_and_frames_produce_byte_identical_messages`；REUSE：`tests/unit/context/attempt-plan.test.mjs` `COMPANION_010_the_memory_is_wrapped_as_low_trust_context` | 跨包 + REUSE | `node --test requirements/context-compression/tests/companion-projection.test.mjs` |
| PREFIX-STABILITY-009（cutoff 完整 turn + digest fail closed） | 跨包：`tests/probe-selection.test.mjs`（context-compression 包）`COMPANION_011_a_digest_mismatch_fails_closed`、`COMPANION_011_the_proof_hashes_exactly_the_clamped_cutoff`、`CTX_011_the_candidate_never_swallows_the_message_being_answered` | 跨包 | `node --test requirements/context-compression/tests/probe-selection.test.mjs` |
| PREFIX-STABILITY-010（历史 pair 原位 replay，anchor 缺失不重定位） | REUSE：`tests/unit/host/pair-thought-anchored.test.mjs` `H13_02_historical_pair_never_relocates_to_current_batch`、`H13_03_same_placement_reentry_appends_no_pair`、`H13_04_restart_replay_is_byte_identical`、`H13_05_missing_anchor_pair_is_omitted_not_relocated` | REUSE | `node --test tests/unit/host/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-011（冷边界由事实驱动） | REUSE：`tests/unit/host/pair-thought-anchored.test.mjs` `H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix`、`H13_08_n_round_property_prefix_law_holds`；跨包：`tests/host-compaction-policy.test.mjs`（context-compression 包）`HOST_006_containment_keys_on_the_folded_predicate_not_raw_fields` | REUSE + 跨包 | `node --test tests/unit/host/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-012（reanchor/rebase 提交后不回滚） | `tests/prefix-epoch.test.mjs`：`CTX_012_a_replayed_rebase_is_reported_as_stale`、`HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch`；REUSE：`tests/unit/context/fold-context-recovery.test.mjs` `HOST_006_a_replayed_reanchor_leaves_rebuilt_coverage_alone` | MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-013（prefix identity 范围） | `tests/prefix-append-only-law.test.mjs`（NEW）：`PREFIX_STABILITY_tool_set_change_breaks_the_law_even_if_messages_prefix`、`PREFIX_STABILITY_identity_or_system_change_breaks_the_law`、`PREFIX_STABILITY_reverse_order_is_not_a_prefix` | NEW | `node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-014（synthetic 正文不进 trace 系） | REUSE：`tests/unit/host/pair-thought-anchored.test.mjs`（pair 正文只在 wire，不进 XTrace 的交叉由 HOST-013 行为约束 4 覆盖）；跨包：`tests/x-trace-capture.test.mjs`（semantic-trace 包）`COMPANION_012_*`（capture 边界无 synthetic 输入） | REUSE + 跨包 | `node --test tests/unit/host/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-015（synthetic id 确定性派生） | 跨包：`tests/companion-projection.test.mjs`（context-compression 包）`COMPANION_013_seal_root_is_derived_from_exactly_the_candidate_identity`、`COMPANION_013_seal_root_changes_when_any_identity_field_changes`、`COMPANION_013_seal_root_is_stable_across_calls`；REUSE：`tests/unit/context/attempt-plan.test.mjs` `COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id` | 跨包 + REUSE | `node --test requirements/context-compression/tests/companion-projection.test.mjs` |

## 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/prefix-epoch.test.mjs` | MOVE `tests/unit/context/prefix-epoch.test.mjs` | 已跑绿（15 pass） |
| `tests/prefix-append-only-law.test.mjs` | NEW | 已跑绿（5 pass） |
| `tests/prefix-epoch-todo-checkpoint.test.mjs` | NEW | 已跑绿（3 pass） |

## 3. 单跑命令

```text
node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs
node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs
node --test requirements/prefix-stability/tests/prefix-epoch-todo-checkpoint.test.mjs
```

## 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `tests/unit/invariants/prompt-stability.test.mjs` | `PROMPT_STABILITY_*`（Gate D byte invariants） | SPLIT@cutover：participant-identity（Persona 绑定）+ prefix-stability（system 字节）+ provider-language（语言绑定）三分 |
| `tests/unit/host/pair-thought-anchored.test.mjs` | `H13_01/02/03/04/05/08`（PREFIX LAW + 原位 replay） | SPLIT@cutover：前缀律/锚点语义归本包；wire 渲染归 provider-projection；marker 正文归 cognitive-environment |
| `tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs` | `isAppendOnlyPrefix(Q1,Q2)`（PREFIX LAW on reused child） | SPLIT@cutover：sync-delegate 生命周期归 delegation；prefix 断言归本包 |
| `tests/unit/context/attempt-plan.test.mjs` | `CTX_010_*`、`COMPANION_010/013_*`（profile 选择、frozen prefix、synthetic id） | SPLIT@cutover：AttemptExecutionProfile 归 provider-attempt-recovery；epoch 相关归本包 |
| `tests/unit/context/fold-context-recovery.test.mjs` | `CTX_012_rebase_folds_into_the_prefix_projection_only`、`HOST_006_*` | 归 durable-events（fold 语义） |
| `tests/unit/enforcer/blogger-convergence-gaps.test.mjs` 等 | C0 观察（squash 不破坏 prefix 前提） | SPLIT@cutover：enforcer 协议面归 behavior-diagnosis |

## 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（PREFIX LAW 由
`ProviderProjection.isAppendOnlyPrefix` + fold 测试承担）。若 cutover 后需要散文 canary，
建议增加 `PREFIX_STABILITY_*` 锚点并声明 owner 为本包。
