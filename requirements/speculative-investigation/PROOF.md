# speculative-investigation — PROOF

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包 `tests/`，删原文件）、
> `REUSE`（留在原处，记精确锚点 + SPLIT@cutover 计划）、`NEW`（本包新写）。
> 单跑命令：`node --test <file>`。全量：`node requirements/verification-system/tests/run.mjs`（自动发现
> `requirements/<package>/tests/**/*.test.mjs`）。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| SPEC-INV-001 零影响基线 | `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`（`STRENGTH_001_014_policy_nested_replica_cannot_speculate`、`STRENGTH_002_011_policy_k0_default_when_host_canary_or_cost_is_unproven`）+ 本包 `host-policy.test.mjs`（`STRENGTH_011_default_settings_are_shadow_k0_with_economic_holdout_and_no_k2_enablement`） | REUSE + MOVE | `node --test requirements/speculative-investigation/tests/host-canary-k0.test.mjs`；`node --test requirements/speculative-investigation/tests/host-policy.test.mjs` |
| SPEC-INV-002 Eligible opportunity | 本包 `authority-policy.test.mjs`（`STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_deep_opportunities`）+ `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`（`STRENGTH_002_013_review_finality_and_attached_internal_leaf_are_always_k0`、`STRENGTH_002_003_target_unbound_and_replica_request_kind_are_k0`） | MOVE + REUSE | 对应两文件 `node --test` |
| SPEC-INV-003 预算单位 K | `requirements/speculative-investigation/tests/batch-collector.test.mjs`（`STRENGTH_003_005_collector_preserves_provider_request_batches_and_concurrent_order`）+ `requirements/speculative-investigation/tests/replica-transform.test.mjs`（`STRENGTH_003_K1_aborts_before_provider_request_2_after_one_complete_batch`、`STRENGTH_003_K2_allows_request_2_then_aborts_before_request_3`、`STRENGTH_003_K2_counts_parallel_OpenCode_tool_parts_as_one_request_then_stops_before_request_3`） | REUSE | `node --test requirements/speculative-investigation/tests/batch-collector.test.mjs`；`node --test requirements/speculative-investigation/tests/replica-transform.test.mjs` |
| SPEC-INV-004 Replica authority | 本包 `authority-policy.test.mjs`（`STRENGTH_004_<role>_replica_has_exact_readonly_capabilities`、`STRENGTH_004_<role>_replica_is_fail_closed`、`STRENGTH_004_019_replica_is_never_owner_fallback_or_prefix_probe_evidence`）+ `requirements/speculative-investigation/tests/runtime.test.mjs`（`STRENGTH_014_runtime_is_owner_single_flight_and_decision_local`、`STRENGTH_004_runtime_rejects_K0_and_ineligible_replica_authority`）+ `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`（`STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network`、`STRENGTH_004_006_policy_replica_host_tool_map_denies_unknown_tools_instead_of_asking`、`STRENGTH_004_007_policy_same_role_prompt_has_no_replica_identity`、`STRENGTH_014_policy_strength_replica_is_internal_leaf_attached_not_satellite_kind`） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-005 Candidate frame | `requirements/speculative-investigation/tests/frame-projection.test.mjs`（`STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches`、`STRENGTH_005_frame_digest_and_owner_wire_ids_are_restart_stable`）+ `requirements/speculative-investigation/tests/projection-adapter.test.mjs`（`STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest`） | REUSE | `node --test requirements/speculative-investigation/tests/frame-projection.test.mjs`；`node --test requirements/speculative-investigation/tests/projection-adapter.test.mjs` |
| SPEC-INV-006 Prepared ≠ 历史 | 本包 `commit-promotion.test.mjs`（`STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing`）+ 本包 `store.test.mjs`（`STRENGTH_006_store_envelope_puts_large_material_only_in_payload_refs`、`STRENGTH_006_same_decision_different_prepared_material_is_identity_collision`、`STRENGTH_006_integrator_Current_reflects_Prepared_binding_without_history_scan`）+ 本包 `durability-port.test.mjs`（`STRENGTH_006_008_durability_port_publishes_payload_closure_and_reloads_the_same_bundle`、`STRENGTH_006_durability_port_rejects_conflicting_Prepared_identity`）+ 本包 `frame-projection.test.mjs`（`STRENGTH_006_projection_binds_prepared_identity_and_rejects_conflict`）+ 本包 `lifecycle-recovery.test.mjs`（`STRENGTH_006_008_prepared_candidate_cannot_be_traced_or_raw_replayed`）+ `requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs`（Candidate 永不进入 XTrace/LWR） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-007 Promotion 只由消费证据 | 本包 `turn-evidence.test.mjs`（`STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping`）+ 本包 `commit-promotion.test.mjs`（`STRENGTH_007_promotion_commit_unknown_never_allows_continuation_without_durable_fact`、`STRENGTH_007_promotion_requires_the_exact_target_run_and_real_provider_output`）+ 本包 `frame-projection.test.mjs`（`STRENGTH_007_projection_promotion_requires_prepared_and_exact_target`）+ 本包 `lifecycle-recovery.test.mjs`（`STRENGTH_007_lifecycle_promotes_only_exact_target_with_real_provider_output`）+ 本包 `store.test.mjs`（`STRENGTH_007_promotion_without_prepared_is_missing_parent`、`STRENGTH_007_integrator_Current_reflects_Promoted_without_history_scan`） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-008 Replay 与 XTrace closure | `requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs`（`STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor`、`STRENGTH_008_compaction_does_not_retire_raw_replay_without_xtrace_coverage`、`STRENGTH_008_trace_recovery_requires_one_exact_contiguous_canonical_match`）+ 本包 `store.test.mjs`（`STRENGTH_008_integrator_Current_reflects_Traced_range_without_history_scan`）+ `requirements/speculative-investigation/tests/projection-algebra.test.mjs`（`STRENGTH_008_009_multiple_promoted_absolute_anchors_are_registration_order_independent`）+ `requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs`（restart 后仍见 frame、Companion 只在 Promotion 后 ingestion） | REUSE | `node --test requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs`；`node --test requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs` |
| SPEC-INV-009 Projection 与 no-reflection | `requirements/speculative-investigation/tests/projection-algebra.test.mjs`（`STRENGTH_009_mirror_conflicts_with_normal_work_base_selection`、`STRENGTH_006_009_candidate_wrong_target_and_promoted_replica_reflection_conflict`、`STRENGTH_009_012_policy_promoted_frames_leave_later_pair_anchor_messages_in_place`）+ `requirements/speculative-investigation/tests/projection-adapter.test.mjs`（`STRENGTH_009_rendered_message_adapter_roundtrips_wire_semantics_with_host_only_ids`）+ `requirements/speculative-investigation/tests/frame-projection.test.mjs`（`STRENGTH_009_replica_mirror_localizes_owner_call_ids_without_changing_semantics`） | REUSE | 对应文件 `node --test` |
| SPEC-INV-010 Predictor 与 control | 本包 `authority-policy.test.mjs`（`STRENGTH_010_value_equations_charge_fast_bytes_delay_and_risk`）+ `requirements/speculative-investigation/tests/predictor-rollout.test.mjs`（`STRENGTH_010_feature_key_has_no_replica_or_score_provenance`、`STRENGTH_010_predictor_learns_only_explicit_primary_labels_and_keeps_a_bounded_feature_key`、`STRENGTH_010_control_assignment_is_restart_stable_and_has_no_predictor_score_input`、`STRENGTH_010_rollout_uses_explicit_costs_and_shadow_never_means_treatment`、`STRENGTH_010_economic_holdout_is_not_skipped_and_ineligible_never_counts_as_holdout`、`STRENGTH_010_k2_is_gated_and_not_enabled_by_this_proof`） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-011 失败、取消与熔断 | 本包 `host-policy.test.mjs`（`STRENGTH_011_dry_run_is_an_explicit_non_default_host_canary_mode`、`STRENGTH_011_dry_run_budget_defaults_to_k1_and_requires_explicit_k2_canary_opt_in`、`STRENGTH_011_host_canary_is_bound_to_the_pinned_OpenCode_and_plugin_contract`、`STRENGTH_011_process_fuse_is_first-failure-latched_and_cannot_be_cleared_by_a_session_cleanup`）+ 本包 `commit-promotion.test.mjs`（`STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing` fail-closed 行） | MOVE | `node --test requirements/speculative-investigation/tests/host-policy.test.mjs` |
| SPEC-INV-012 模型不可见、系统可审计 | `requirements/speculative-investigation/tests/invisibility.test.mjs`（`STRENGTH_012_candidate_and_promoted_semantic_bytes_have_no_mechanism_provenance`）+ `requirements/speculative-investigation/tests/projection-algebra.test.mjs`（`STRENGTH_009_012_policy_promoted_frames_leave_later_pair_anchor_messages_in_place`） | REUSE | `node --test requirements/speculative-investigation/tests/invisibility.test.mjs` |
| SPEC-INV-013 DryRun visible nonblocking shadow | `requirements/speculative-investigation/tests/dry-run-shadow.test.mjs`：DryRun branch uses distinct `StartDryRun` without awaiting decision terminal；runtime creates/registers real child and observes independently；zero Prepared/Promoted/message replacement；owner cancel still aborts child | NEW | `node --test requirements/speculative-investigation/tests/dry-run-shadow.test.mjs` |

补充 REUSE 交叉引用（非本包命题落点，供追踪）：

- `requirements/session-ontology/tests/session-ownership-ratchet.test.mjs`（`| StrengthReplica` 为允许 kind；
  StrengthReplica 是 `InternalLeaf × Attached` 的机械证明）→ owner `session-ontology`。
- ~~`tests/unit/verify/student-teacher-absence.test.mjs`~~（`| StrengthReplica` token absence ratchet）→ 已退休删除（2026-08-14）→
  GARBAGE ratchet，owner `session-ontology`。
- `requirements/verification-system/tests/e2e/entry.test.mjs` long-stroke `strength-canary-*`（K2 恰好两轮、第 3 轮物理不外发、
  `StrengthCandidatePrepared=0`）→ `verification-system` MECHANISM（HOW.md §8 交叉引用）。

## GAP

- `GAP-015` —— **CLOSED**：production DryRun 已改为 distinct `StartDryRun`：真实 `CreateChildSession` / `registerReplica` / Detached OpenCode execution，owner 只等待物理 child bootstrap 后立即继续；terminal/deadline 在独立 observation task 中结束；DryRun 不 Prepared/Promoted、不映射回 owner。落点 `dry-run-shadow.test.mjs` 已执行并通过。

## Semantic anchor ids

本包在 `scripts/checks/semantic-anchors.mjs` 中**当前无已声明 anchor 组**（catalog 的
inquiry 组归 `epistemic-reasoning`；Strength 无对应 anchor id）。如未来为 speculation 增加
anchor，应在 `ROLE_SEMANTIC_ANCHORS` 声明并在此登记。

## SPLIT@cutover 待办

1. ~~`tests/unit/strength/**` 12 个文件直接 import `dist/fable_modules/**`（test-boundary 门
   baseline 内），禁止物理移动~~ 已执行（Wave 2a）：全部迁入本包 `tests/`，fable 直连 import
   改写为 support adapter 等价调用；test-boundary baseline 已收缩。
   逐文件移入本包 `tests/`。
2. `requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs` 原含 fable_modules import，已改写为 support 等价调用（Wave 2b）。
   cutover 时按本表落点拆分（Candidate∉XTrace 断言 → `semantic-trace` 侧副本，
   Promotion/replay 断言 → 本包）。
3. `unpromoted ≠ history` 断言目前全部由本包（及 strength REUSE）测试证明；cutover 时
   `semantic-trace` 应在自己 tests/ 建立 trace 侧断言，本包保留 Candidate 侧断言，二者交叉
   引用不重复收（HANDOFF §18.6）。

## 验证状态

- 既有历史验证记录保留：4 个 MOVE 文件曾单跑绿（`authority-policy` 13、`commit-promotion` 3、`host-policy` 5、`turn-evidence` 1；2026-08-14）。
- `SPEC-INV-013` DryRun oracle 已在 2026-08-15 本轮收敛中执行：`dry-run-shadow.test.mjs` 四条均通过；其结果只证明该包的 DryRun 合同，全仓结论仍以完整门禁为准。
