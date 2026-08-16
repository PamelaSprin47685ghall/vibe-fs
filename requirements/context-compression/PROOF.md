# context-compression — PROOF（测试落点表）

## 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| CONTEXT-COMPRESSION-001（不观察容量） | `tests/companion-projection.test.mjs`：`CTX_001_no_prompt_carries_a_token_count_or_output_budget`；`tests/ctx-capacity-observation-forbidden.test.mjs`（NEW）：`CTX_001_forbidden_capacity_synonyms_never_appear_in_production_source`、`CTX_001_the_only_allowed_byte_metric_is_the_delta_input_contract` | MOVE + NEW | `node --test requirements/context-compression/tests/ctx-capacity-observation-forbidden.test.mjs` |
| CONTEXT-COMPRESSION-002（不主动预测溢出） | `tests/recovery-slot.test.mjs`：`FALLBACK_012_a_new_sequence_always_starts_unarmed`、`CTX_006_recovery_needs_arming_a_primed_offset_and_material`、`FALLBACK_012_parked_cursor_does_not_trigger_compression_acceptance_trace` | MOVE | `node --test requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-003（200 KiB 输入合同） | `tests/blogger-delta.test.mjs`：`CTX_003_delta_limit_is_200_KiB`、`CTX_003_no_chunk_exceeds_the_limit`、`CTX_003_no_chunk_exceeds_the_limit` | MOVE | `node --test requirements/context-compression/tests/blogger-delta.test.mjs` |
| CONTEXT-COMPRESSION-004（输出预算属 provider） | `tests/terminal-validity.test.mjs`：`CTX_004_empty_terminal_is_not_a_result`、`CTX_004_xml_only_terminal_is_not_a_result`、`CTX_004_prose_is_a_result`、`CTX_004_isValid_agrees_with_check` | MOVE | `node --test requirements/context-compression/tests/terminal-validity.test.mjs` |
| CONTEXT-COMPRESSION-005（失败不分类） | `tests/recovery-slot.test.mjs`：`CTX_005_Failed_and_Aborted_take_the_identical_path`；`tests/host-compaction-policy.test.mjs`：`CTX_005_containment_does_not_discriminate_by_source`；`tests/terminal-validity.test.mjs`：`CTX_005_validity_does_not_depend_on_failure_cause` | MOVE | `node --test requirements/context-compression/tests/recovery-slot.test.mjs requirements/context-compression/tests/host-compaction-policy.test.mjs` |
| CONTEXT-COMPRESSION-006（恢复槽三合取） | `tests/recovery-slot.test.mjs`：`CTX_006_recovery_needs_arming_a_primed_offset_and_material`、`CTX_006_the_primed_slots_are_exactly_the_odd_offsets` | MOVE | `node --test requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-007（RequestKind 分派） | `tests/recovery-slot.test.mjs`：`CTX_007_a_failed_squash_fails_the_slot_without_sending_the_main_request`、`CTX_007_a_successful_main_commits_and_does_not_move_the_cursor`、`CTX_007_a_failed_main_fails_the_slot_for_every_kind`、`CTX_008_only_a_failed_slot_advances_the_cursor`、`PROMPT_008_every_request_kind_has_a_distinct_diagnostic_label` | MOVE | `node --test requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-008（X 不发压缩请求） | `tests/recovery-slot.test.mjs`：`CTX_010_only_the_work_main_request_may_carry_a_prefix_probe`；REUSE：`requirements/context-compression/tests/attempt-plan-probe-eligibility.test.mjs` `CTX_010_a_non_recovery_slot_never_asks_for_a_probe`、`CTX_010_a_companion_request_never_asks_for_a_probe_even_when_armed` | MOVE + REUSE | `node --test requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-009（候选未提交不是事实） | `tests/probe-selection.test.mjs`：`CTX_010_the_probe_records_the_epoch_it_was_built_from`；`requirements/prefix-stability/tests/prefix-epoch.test.mjs`（prefix-stability 包）`CTX_010_a_failed_probe_leaves_no_trace_to_undo`；REUSE：`requirements/context-compression/tests/attempt-plan-probe-eligibility.test.mjs` `CTX_010_a_discarded_probe_leaves_the_committed_epoch_in_place` | MOVE + REUSE | `node --test requirements/context-compression/tests/probe-selection.test.mjs` |
| CONTEXT-COMPRESSION-010（候选严格新于已提交） | `tests/probe-selection.test.mjs`：`CTX_011_a_retreating_candidate_is_refused`、`CTX_011_an_identical_candidate_is_refused_before_an_epoch_is_spent`、`CTX_011_the_same_cutoff_with_a_tighter_B_is_a_new_candidate`、`CTX_011_no_completed_turn_yet_means_no_candidate`、`COMPANION_011_a_digest_mismatch_fails_closed` | MOVE | `node --test requirements/context-compression/tests/probe-selection.test.mjs` |
| CONTEXT-COMPRESSION-011（提交语义分型） | `tests/recovery-slot.test.mjs`：`CTX_012_a_valid_squash_commits_permanently_and_the_slot_continues`、`CTX_012_an_invalid_squash_is_skipped_rather_than_repaired`；`tests/blog-projection.test.mjs`：`CTX_012_squash_replaces_the_oldest_frames_and_leaves_the_covered_range_alone`、`CTX_012_a_squash_that_consumes_the_whole_covered_range_leaves_one_coverable_frame`、`CTX_012_squash_count_outside_available_range_is_refused` | MOVE | `node --test requirements/context-compression/tests/recovery-slot.test.mjs requirements/context-compression/tests/blog-projection.test.mjs` |
| CONTEXT-COMPRESSION-012（delta TOML 合同） | `tests/blogger-delta.test.mjs`：`CTX_013_normal_chunk_is_data_only_and_counts_no_instruction_header`、`CTX_013_a_single_oversized_part_is_hard_truncated_and_marked`、`CTX_013_truncation_discards_the_tail_rather_than_resending_it`、`CTX_013_an_omission_marker_is_never_truncated`、`CTX_013_the_same_input_produces_the_same_chunks`；`tests/companion-projection.test.mjs`：`COMPANION_007_canonical_digest_uses_semantic_projection_not_toml` | MOVE | `node --test requirements/context-compression/tests/blogger-delta.test.mjs requirements/context-compression/tests/companion-projection.test.mjs` |
| CONTEXT-COMPRESSION-013（诊断不是控制输入） | `requirements/context-compression/tests/ctx014.test.mjs::CTX_014_diagnostic_emit_is_structured_and_redacted`; `requirements/context-compression/tests/ctx014.test.mjs::CTX_014_fatal_emits_structured_event_without_raw_payload`; `requirements/context-compression/tests/ctx014.test.mjs::CTX_014_fatal_path_rejects_unbounded_fields` | MOVE | `node --test requirements/context-compression/tests/ctx014.test.mjs` |
| CONTEXT-COMPRESSION-014（squash 只处理本 X frames） | `tests/blog-projection.test.mjs`：`COMPANION_006_squash_rewrites_first_half_of_frames_permanently`；`tests/companion-projection.test.mjs`：`CTX_012_a_squash_ignores_a_delta_even_if_one_is_supplied`、`CTX_012_a_squash_never_shows_the_later_frames` | MOVE | `node --test requirements/context-compression/tests/blog-projection.test.mjs requirements/context-compression/tests/companion-projection.test.mjs` |
| CONTEXT-COMPRESSION-015（busy/失败不推进 coverage） | `tests/blog-projection.test.mjs`：`COMPANION_008_entry_appends_frame_and_advances_coverage_together`、`CTX_011_entry_that_consumed_nothing_is_refused`、`CTX_011_coverage_may_not_retreat`、`PERSIST_010_entry_whose_previous_cursor_disagrees_is_refused` | MOVE | `node --test requirements/context-compression/tests/blog-projection.test.mjs` |
| CONTEXT-COMPRESSION-016（Y 只物化 PrefixCoverage 完整 turn） | `tests/blogger-delta.test.mjs`：`CTX_011_a_multi_part_turn_splits_at_part_boundaries_and_holds_the_cutoff`、`CTX_011_a_chunk_ending_on_a_non_final_part_never_advances_the_cutoff`、`CTX_011_the_cutoff_never_decreases_across_chunks`；`tests/probe-selection.test.mjs`：`CTX_011_the_candidate_never_swallows_the_message_being_answered`、`COMPANION_011_the_proof_hashes_exactly_the_clamped_cutoff` | MOVE | `node --test requirements/context-compression/tests/blogger-delta.test.mjs requirements/context-compression/tests/probe-selection.test.mjs` |
| CONTEXT-COMPRESSION-017（Opening floor） | `tests/ctx-opening-floor.test.mjs`（NEW）：`CTX_016_pre_t1_floor_is_the_xtrace_head_not_an_activation_cursor`、`CTX_016_work_activated_is_inert_and_does_not_move_the_floor`、`CTX_016_blogger_effective_start_is_max_of_record_coverage_and_floor`；跨包：`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_capture_opening_takes_authoritative_requirements` | NEW + 跨包 | `node --test requirements/context-compression/tests/ctx-opening-floor.test.mjs` |
| CONTEXT-COMPRESSION-018（连续 catch-up：无 frozen frontier；所有 quiet re-entry 先 park，wake 后读 live Current） | `tests/enforcer-cycle-commit-convergence.test.mjs`：`ENFORCER_same_run_after_squash_rejected_as_known_not_committed`（idempotent receipt quiet 必须实际调用 ParkTransform 后才可因模拟 physical expiry stop）/ `ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier`（park 前 head=2，park 期间新增 3..4，同一 continuation 立即派生 2→4 下一块）；`tests/blogger-convergence-gaps.test.mjs`：`C0_caught_up_is_parked_not_completed_and_wake_rechecks_live_Current`、`C0_commit_drains_via_tryRefresh_before_park`；跨包边界 REUSE `requirements/crash-reconciliation/tests/explicit-continue.test.mjs`：`CRASH_017_new_process_runtime_dispose_does_not_claim_or_abort_old_active_handle`、`CRASH_018_continue_discloses_restart_keeps_broken_tool_visible_and_process_locally_reenlists_survivor` | NEW + REUSE + 跨包 | `node --test requirements/context-compression/tests/enforcer-cycle-commit-convergence.test.mjs requirements/context-compression/tests/blogger-convergence-gaps.test.mjs requirements/crash-reconciliation/tests/explicit-continue.test.mjs` |

## 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/blog-projection.test.mjs` | MOVE `requirements/context-compression/tests/blog-projection.test.mjs` | 已跑绿（20 pass） |
| `tests/companion-projection.test.mjs` | MOVE `requirements/context-compression/tests/companion-projection.test.mjs` | 已跑绿（27 pass） |
| `tests/blogger-delta.test.mjs` | MOVE `requirements/context-compression/tests/blogger-delta.test.mjs` | 已跑绿（19 pass） |
| `tests/probe-selection.test.mjs` | MOVE `requirements/context-compression/tests/probe-selection.test.mjs` | 已跑绿（13 pass） |
| `tests/recovery-slot.test.mjs` | MOVE `requirements/context-compression/tests/recovery-slot.test.mjs` | 已跑绿（20 pass） |
| `tests/host-compaction-policy.test.mjs` | MOVE `requirements/context-compression/tests/host-compaction-policy.test.mjs` | 已跑绿（14 pass） |
| `tests/ctx014.test.mjs` | MOVE `requirements/context-compression/tests/ctx014.test.mjs` | 已跑绿（7 pass） |
| `tests/terminal-validity.test.mjs` | MOVE `requirements/context-compression/tests/terminal-validity.test.mjs` | 已跑绿（6 pass） |
| `tests/ctx-capacity-observation-forbidden.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/ctx-opening-floor.test.mjs` | NEW | 已跑绿（3 pass） |

## 3. 单跑命令

```text
node --test requirements/context-compression/tests/blog-projection.test.mjs
node --test requirements/context-compression/tests/companion-projection.test.mjs
node --test requirements/context-compression/tests/blogger-delta.test.mjs
node --test requirements/context-compression/tests/probe-selection.test.mjs
node --test requirements/context-compression/tests/recovery-slot.test.mjs
node --test requirements/context-compression/tests/host-compaction-policy.test.mjs
node --test requirements/context-compression/tests/ctx014.test.mjs
node --test requirements/context-compression/tests/terminal-validity.test.mjs
node --test requirements/context-compression/tests/ctx-capacity-observation-forbidden.test.mjs
node --test requirements/context-compression/tests/ctx-opening-floor.test.mjs
```

## 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/context-compression/tests/attempt-plan-probe-eligibility.test.mjs` | `CTX_010_*`（probe 只在 armed work-main slot） | SPLIT@cutover：AttemptExecutionProfile 归 provider-attempt-recovery；本包引用 probe 资格 |
| `requirements/durable-events/tests/fold-context-recovery.test.mjs` | `PERSIST_010_*`（fold 语义） | 归 durable-events |
| `requirements/provider-projection/tests/synthetic-toml.test.mjs`、`requirements/provider-projection/tests/blogger-toml.test.mjs` | TOML 布局/转义渲染 | 归 provider-projection（CTX-013 的渲染半边）；blogger-toml 待 provider-projection cutover 迁移 |
| ~~`tests/unit/enforcer/blogger-convergence-gaps.test.mjs`~~、~~`blogger-runtime.test.mjs`~~ | Blogger request-cycle 收敛（C0/ENFORCER-047；已迁本包 tests/） | SPLIT@cutover：enforcer 协议面归 behavior-diagnosis；压缩输入面归本包 |
| `requirements/semantic-trace/tests/x-trace-locality.test.mjs`（semantic-trace 包） | `TODO-004/008` XTrace range 与 LWR 交叉 | 本包引用 effectiveStart/floor |

## 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（CTX 语义由 F# 类型 + fold
测试承担）。若 cutover 后需要散文 canary，建议增加 `CONTEXT_COMPRESSION_*` 锚点并声明
owner 为本包（CTX-001/002 的墓碑扫描已是机器可执行证明）。
