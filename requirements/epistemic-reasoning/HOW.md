# epistemic-reasoning — HOW

## 架构模型与执行流

`epistemic-reasoning` 实现了带控制器的认知协同循环（Co-yield Coroutine）：

```text
start(question)
  ↓
初始化 EpistemicState (建立充分状态)
  ↓
Policy.decide → 生成挂起请求 PendingRequest (首步固定为 SemanticAssessmentRequest)
  ↓
MCP 层返回 structuredContent 携带 nextTool 提示
  ↓
[循环交互阶段]:
  阶段工具 (assess / propose / investigate / synthesize) 提交 Observation
  ↓
  校验 Observation 与当前 PendingRequest 是否严格同型
  ↓
  Absorb 阶段: 吸收观测 (提案写入控制层，仅调查产生事实与证据)
  ↓
  Global Closure: 触发闭包同步循环直至不动点
    [Bayes.update → Value.revalue → Representation.optimize → Solver 同步]
  ↓
  Policy.decide:
    - 收益收敛或预算耗尽 → 产出 CanonicalAnswer (带分列认识基底)
    - 探究继续 → 产出下一个 PendingRequest 及 nextTool 引导
```

## 核心机制

### 1. 认知状态结构与生命周期 (State Structure & Lifecycle)

- **充分状态管理**：`EpistemicState` 显式维护 `Findings`、`Evidence`、`Hypotheses`、`Dependencies` 与 `CognitiveActions`，拒绝将原始文本记录作为状态本体。
- **动态契约**：`RootContract` 维持连续概率分布，可根据调查中返回的语义评估自适应调整，动态激活对应方法生成器。

### 2. 全局闭包与幂等同步 (Global Closure & Idempotence)

- 每次接收观测后，内核必须同步推导事实推论、概率更新、动作重估与等价约简，直到达到结构不动点。
- 闭包操作满足严格幂等性，纯内部计算不创造虚假证据或人为抬高后验置信。

### 3. 概率推断资格门禁 (Bayesian Qualification Gate)

- 严格校验证据的数值资格：必须具备有限 `[0, 1]` 区间内的似然度并覆盖全部假设空间。
- 按 `DependencyKey` 进行组内聚合，每个独立来源组仅选出一个规范代表参与似然度连乘，彻底根除同源重复陈述对后验的虚假放大。

### 4. 依赖感知的 Pareto 等价约简 (Pareto Equivalence Reduction)

- 候选动作仅在内核改写或 semantic+dependency 完全相同时归入同一等价类。
- 等价类内部执行多维收益与成本的支配比较，不可直接比较的候选保留在 Pareto 前沿，防止信息价值与执行成本的权衡被单一标量粗暴抹平。

### 5. MCP 交互映射 (MCP Affordance Translation)

- MCP 服务端将内核的挂起请求严格映射为对应的阶段工具，并输出 `nextTool` 引导字段。
- 服务端身份由 `PackageMetadata` 从 `package.json` 读取，杜绝基于当前目录探测带来的环境漂移。

### 6. Sphinx-GEC 组合面 (GEC Composition Surface)

- `Sphinx/Core` 只定义 ID、canonical opaque envelope、typed hypergraph、证书槽、work 事实、预算、事件与纯 reducer；认识论零硬编码，`Kind`/`Relation`/schema 只比 identity/hash。
- 同一 `NodeId` 的 `ValueCertificate` 同时持有 exact、lower/upper envelope、sample summary、ordinal constraints、latent posterior、residual 与 witness/derivation 引用；exact/bound 声明确定性 concretization inclusion，sample/latent 声明带显式 level/error/assumptions/scope 的概率 coverage。
- `replay` 把 JS 事件解码为 canonical `InquiryEvent`（`parent:"none"` 为创世，否则链式；合成 id 为 `"ev"+revision），经 `Reducer.fold` 得到 `semanticView`；`stateHash`/`semanticHash` 是输入事件列表 canonical-JSON 的 sha256（键序无关，事件序相关）。
- `schedule` 只选依赖满足、冲突互斥、预算内批次；不可比收益保留 Pareto frontier；批组合是 canonical id 序函数复合，不做 `ΣΔ`。
- `splitBallot` 先存共同 root snapshot 再以可复现 PRNG 分配处理/标签/顺序；问法效应是带符号 difference-in-means 加 permutation null；`selfPrediction` 密封承诺加 epsilon-floor log score；`stopCertificate` 只覆盖已检验 framing 族。
- Host 与导出只翻译同一 Core 事件/证书契约：`foldHostEvents` 把 host 私有字段排除在 hash 外；`planOpenCodeDispatch` 只描述 blind child（共同根快照、无 sibling/失败泄漏、每次重试新 child、depth 恒 1）。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EPI-001 | `requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-001] start_yields_semantic_assessment_request` |
| EPI-002 | `requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-002] fsharp_kernel_has_no_agent_host_domain_dependency_and_sdk_stays_at_mcp_edge` |
| EPI-003 | `requirements/epistemic-reasoning/tests/semantics.test.mjs::WHAT[EPI-003] ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence` |
| EPI-004 | `requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-004] resume_rejects_observation_that_does_not_match_pending_kernel_request` |
| EPI-005 | `requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-005] semantic_assessment_and_candidates_are_control_observations_not_world_evidence`；`requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-005] candidate_question_must_be_investigated_before_it_can_affect_answer` |
| EPI-006 | `requirements/epistemic-reasoning/tests/bayes.test.mjs::WHAT[EPI-006] same_semantic_evidence_from_independent_dependency_groups_is_preserved_twice`；`requirements/epistemic-reasoning/tests/bayes.test.mjs::WHAT[EPI-006] same_dependency_group_is_not_counted_as_independent_evidence_twice` |
| EPI-007 | `requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-007] contract_keeps_distribution_after_semantic_assessment` |
| EPI-008 | `requirements/epistemic-reasoning/tests/semantics.test.mjs::WHAT[EPI-008] gateway_gain_can_make_low_immediate_gain_question_worth_asking` |
| EPI-009 | `requirements/epistemic-reasoning/tests/bayes.test.mjs::WHAT[EPI-009] bayesian_posterior_requires_explicit_numeric_qualification`；`requirements/epistemic-reasoning/tests/bayes.test.mjs::WHAT[EPI-009] qualified_independent_evidence_updates_posterior`；`requirements/epistemic-reasoning/tests/bayes.test.mjs::WHAT[EPI-009] unqualified_item_cannot_mask_qualified_evidence_from_same_dependency_group` |
| EPI-010 | `requirements/epistemic-reasoning/tests/search.test.mjs::WHAT[EPI-010] graph_astar_degenerates_to_standard_g_plus_h_shortest_path`；`requirements/epistemic-reasoning/tests/search.test.mjs::WHAT[EPI-010] graph_astar_reopens_closed_node_when_better_g_is_discovered`；`requirements/epistemic-reasoning/tests/search.test.mjs::WHAT[EPI-010] graph_astar_rejects_negative_cost_graph`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] log-space-bayes-survives-likelihood-product-underflow`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] exact-bayes-matches-brute-force-normalized-product-when-representable`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] astar-rejects-nonzero-goal-heuristic-and-exposes-admissibility-assumption`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] exact-bayes-reports-canonical-factors-and-ignores-invalid-shadows`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] mcts-sample-accepts-negative-rewards-and-ignores-legacy-prior`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] astar-reports-global-frontier-bound-incumbent-and-reopens-better-g`；`requirements/epistemic-reasoning/tests/classic-algorithm.test.mjs::WHAT[EPI-010] seeded-mcts-returns-descriptive-sample-summary-not-deterministic-truth` |
| EPI-011 | `requirements/epistemic-reasoning/tests/represent.test.mjs::WHAT[EPI-011] wire_equivalence_hint_cannot_force_kernel_merge`；`requirements/epistemic-reasoning/tests/represent.test.mjs::WHAT[EPI-011] same_kernel_identity_merges_candidate_provenance_instead_of_erasing_it`；`requirements/epistemic-reasoning/tests/represent.test.mjs::WHAT[EPI-011] same_question_from_independent_dependency_groups_is_not_false_deduplicated`；`requirements/epistemic-reasoning/tests/represent.test.mjs::WHAT[EPI-011] kernel_owned_equivalence_class_removes_only_truly_dominated_representation`；`requirements/epistemic-reasoning/tests/represent.test.mjs::WHAT[EPI-011] pareto_incomparable_equivalent_representations_both_survive` |
| EPI-012 | `requirements/epistemic-reasoning/tests/kernel.test.mjs::WHAT[EPI-012] closure_is_idempotent_at_fixed_point` |
| EPI-013 | `requirements/epistemic-reasoning/tests/mcp-handle.test.mjs::WHAT[EPI-013] mcp_server_surface_exposes_phase_tools_and_legacy_resume`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] generic_tools_registered_alongside_legacy_eight`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] generic_start_status_cancel_envelope_with_iq_ids_and_stale_submit_conflict`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] text_outputs_carry_handle_and_inquiry_id_for_text_only_models`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] empty_forms_assessment_abstains_but_advances`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] generic_cancel_reports_cancelled_code_and_blank_start_names_its_tool`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] legacy_shaped_id_in_generic_tool_gets_iq_hint`；`requirements/epistemic-reasoning/tests/mcp-contract.test.mjs::WHAT[EPI-013] generic_submit_with_results_advances_revision_and_status_follows`；`requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs::WHAT[EPI-013] tools_list_returns_legacy_eight_plus_generic_five_with_schemas` |
| EPI-014 | `requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs::WHAT[EPI-014] initialize_returns_server_identity_and_instructions`；`requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs::WHAT[EPI-014] newer_negotiated_capability_discovers_generic_tools_without_tasks_or_sampling` |
| EPI-015 | `requirements/epistemic-reasoning/tests/gec-core-vocabulary.test.mjs::WHAT[EPI-015] core_sources_exclude_epistemic_vocabulary_or_naive_core_reintroduces_legacy_ontology`；`requirements/epistemic-reasoning/tests/gec-core-vocabulary.test.mjs::WHAT[EPI-015] ids_are_kind_specific_opaque_or_stringly_typed_core_accepts_any_string`；`requirements/epistemic-reasoning/tests/gec-core-vocabulary.test.mjs::WHAT[EPI-015] envelopes_compare_by_schema_identity_not_payload_semantics_or_core_interprets_payload` |
| EPI-016 | `requirements/epistemic-reasoning/tests/gec-certificate-slots.test.mjs::WHAT[EPI-016] single_certificate_holds_exact_bound_sample_ordinal_latent_together_or_solver_mode_splits_state`；`requirements/epistemic-reasoning/tests/gec-certificate-slots.test.mjs::WHAT[EPI-016] sample_slot_requires_coverage_assumptions_or_point_estimate_masquerades_as_bound`；`requirements/epistemic-reasoning/tests/gec-certificate-slots.test.mjs::WHAT[EPI-016] exact_bound_declare_inclusion_while_sample_declares_coverage_or_value_preorder_collapses`；`requirements/epistemic-reasoning/tests/sphinx-longevity.test.mjs::WHAT[EPI-016] soak_multi_plugin_waves_stay_deterministic_and_honestly_labeled` |
| EPI-017 | `requirements/epistemic-reasoning/tests/gec-replay.test.mjs::WHAT[EPI-017] replay_is_key_order_invariant_or_stringify_hash_breaks_on_reordered_keys`；`requirements/epistemic-reasoning/tests/gec-replay.test.mjs::WHAT[EPI-017] replay_consumes_accepted_observations_without_provider_recall_or_replay_hits_network`；`requirements/epistemic-reasoning/tests/gec-replay.test.mjs::WHAT[EPI-017] replay_rejects_observations_missing_protocol_bindings_or_partial_provenance_replays`；`requirements/epistemic-reasoning/tests/sphinx-longevity.test.mjs::WHAT[EPI-017] soak_replay_and_hash_stay_stable_across_repeated_waves` |
| EPI-018 | `requirements/epistemic-reasoning/tests/host-equivalence.test.mjs::WHAT[EPI-018] same_ordered_canonical_events_fold_to_same_semantic_hash_across_hosts`；`requirements/epistemic-reasoning/tests/host-equivalence.test.mjs::WHAT[EPI-018] reordered_arrivals_do_not_fold_to_the_same_semantic_hash` |
| EPI-019 | `requirements/epistemic-reasoning/tests/durable-event-store.test.mjs::WHAT[EPI-019] append_before_current_only_advances_after_durable_append_and_rejects_stale_expected_revision`；`requirements/epistemic-reasoning/tests/durable-event-store.test.mjs::WHAT[EPI-019] restart_recovery_and_cache_loss_replay_the_same_canonical_hash`；`requirements/epistemic-reasoning/tests/durable-event-store.test.mjs::WHAT[EPI-019] same_work_attempt_replay_is_idempotent_but_conflicting_payload_is_rejected`；`requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs::WHAT[EPI-019] generic_restart_recovers_revision_results_and_conflict` |
| EPI-020 | `requirements/epistemic-reasoning/tests/gec-manifest-lock.test.mjs::WHAT[EPI-020] missing_dependency_duplicate_release_or_abi_mismatch_fails_closed_or_drifted_plugin_runs`；`requirements/epistemic-reasoning/tests/gec-manifest-lock.test.mjs::WHAT[EPI-020] schema_hash_mismatch_rejects_observation_or_content_drift_passes_silently`；`requirements/epistemic-reasoning/tests/gec-manifest-lock.test.mjs::WHAT[EPI-020] mid_run_plugin_swap_is_rejected_or_lock_is_advisory` |
| EPI-021 | `requirements/epistemic-reasoning/tests/gec-work-lifecycle.test.mjs::WHAT[EPI-021] ready_requires_satisfied_dependencies_or_dangling_work_runs_early`；`requirements/epistemic-reasoning/tests/gec-work-lifecycle.test.mjs::WHAT[EPI-021] terminal_states_never_return_to_executing_and_attempt_accepts_single_observation_or_retry_forks_state`；`requirements/epistemic-reasoning/tests/gec-work-lifecycle.test.mjs::WHAT[EPI-021] wall_clock_fields_are_rejected_or_timer_drives_lifecycle` |
| EPI-022 | `requirements/epistemic-reasoning/tests/gec-scheduler.test.mjs::WHAT[EPI-022] batch_respects_dependencies_conflicts_and_budget_or_naive_scheduler_overcommits`；`requirements/epistemic-reasoning/tests/gec-scheduler.test.mjs::WHAT[EPI-022] incomparable_losses_keep_pareto_frontier_or_scalar_sum_hides_tradeoff`；`requirements/epistemic-reasoning/tests/gec-scheduler.test.mjs::WHAT[EPI-022] batch_composes_by_canonical_order_not_input_sum_or_delta_addition_reorders_semantics`；`requirements/epistemic-reasoning/tests/gec-scheduler.test.mjs::WHAT[EPI-022] unconverted-loss-currencies-stay-incomparable-despite-shared-common-currency` |
| EPI-023 | `requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] deterministic-seed-reproduces-identical-balanced-assignment-matrix`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] blind-branch-view-exposes-no-sibling-answer-ranking-or-aggregate`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] wording-effect-reports-signed-difference-in-means-not-absolute-distance`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] ate-interpretation-declares-causal-assumptions-and-permutation-uncertainty`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] treatment-details-configure-wording-polarity-and-order`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] invalid-treatment-polarity-fails-closed`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] missing-root-snapshot-fails-closed-before-randomization`；`requirements/epistemic-reasoning/tests/split-ballot.test.mjs::WHAT[EPI-023] carryover-permutation-null-is-seeded-deterministic-and-capped`；`requirements/epistemic-reasoning/tests/sphinx-longevity.test.mjs::WHAT[EPI-023] soak_seeded_waves_reproduce_identical_matrices_and_stable_signed_effects` |
| EPI-024 | `requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] candidate-label-equivariance-permuted-labels-permute-scores-identically`；`requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] fractional-tie-extension-shares-average-borda-points`；`requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] appearance-normalized-extension-divides-by-ballot-appearance-not-raw-sum`；`requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] borda-guarantees-claim-only-ballot-order-invariance-and-label-equivariance`；`requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] zero-sum-gauge-fixes-location-with-strengths-summing-to-zero`；`requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] disconnected-comparison-graph-returns-typed-unidentifiable-error`；`requirements/epistemic-reasoning/tests/borda-btl.test.mjs::WHAT[EPI-024] separation-with-regularization-stays-finite-and-reports-diagnostics` |
| EPI-025 | `requirements/epistemic-reasoning/tests/self-prediction.test.mjs::WHAT[EPI-025] epsilon-clipped-log-score-stays-finite-on-zero-probability`；`requirements/epistemic-reasoning/tests/self-prediction.test.mjs::WHAT[EPI-025] brier-score-on-valid-simplex-computes-squared-error`；`requirements/epistemic-reasoning/tests/self-prediction.test.mjs::WHAT[EPI-025] brier-score-rejects-prediction-outside-the-simplex`；`requirements/epistemic-reasoning/tests/self-prediction.test.mjs::WHAT[EPI-025] commit-before-reveal-rejects-unsealed-prediction-and-binds-work`；`requirements/epistemic-reasoning/tests/self-prediction.test.mjs::WHAT[EPI-025] raw-score-keeps-calibration-sharpness-separate-and-held-out-gates-update` |
| EPI-026 | `requirements/epistemic-reasoning/tests/gec-fixedpoint.test.mjs::WHAT[EPI-026] declared_finite_dag_lattice_or_contraction_converges_or_closure_claims_without_domain`；`requirements/epistemic-reasoning/tests/gec-fixedpoint.test.mjs::WHAT[EPI-026] undeclared_domain_reports_bounded_residual_without_uniqueness_or_naive_fixed_point_overclaims`；`requirements/epistemic-reasoning/tests/gec-fixedpoint.test.mjs::WHAT[EPI-026] async_convergence_stays_conjecture_without_gap_fairness_and_order_or_partial_evidence_claims_limit`；`requirements/epistemic-reasoning/tests/gec-fixedpoint.test.mjs::WHAT[EPI-026] declared_misspecification_downgrades_async_closure_even_when_other_flags_pass` |
| EPI-027 | `requirements/epistemic-reasoning/tests/opencode-dispatch.test.mjs::WHAT[EPI-027] blind_dispatch_forks_common_root_child_with_depth_one_and_new_child_per_retry`；`requirements/epistemic-reasoning/tests/opencode-dispatch.test.mjs::WHAT[EPI-027] abort_and_drain_terminate_dispatched_work_and_workers_cannot_recurse` |
| EPI-028 | `requirements/epistemic-reasoning/tests/research-export.test.mjs::WHAT[EPI-028] export_contains_every_required_field_and_replay_matches_semantic_and_answer_hashes`；`requirements/epistemic-reasoning/tests/research-export.test.mjs::WHAT[EPI-028] externally_grounded_claims_stay_empty_without_external_source`；`requirements/epistemic-reasoning/tests/sphinx-longevity.test.mjs::WHAT[EPI-028] soak_export_bundles_replay_to_identical_hashes_every_wave` |
| EPI-029 | `requirements/epistemic-reasoning/tests/stop-certificate.test.mjs::WHAT[EPI-029] certificate-bounds-guarantee-to-tested-framing-family-only`；`requirements/epistemic-reasoning/tests/stop-certificate.test.mjs::WHAT[EPI-029] sequential-error-control-tightens-with-repeated-checks`；`requirements/epistemic-reasoning/tests/stop-certificate.test.mjs::WHAT[EPI-029] stable-minority-mode-returns-decision-distribution-not-single-winner`；`requirements/epistemic-reasoning/tests/stop-certificate.test.mjs::WHAT[EPI-029] conservative-upper-voc-blocks-stopping-on-point-estimate-alone`；`requirements/epistemic-reasoning/tests/stop-certificate.test.mjs::WHAT[EPI-029] caller-evidence-fires-stop-when-all-checks-pass`；`requirements/epistemic-reasoning/tests/stop-certificate.test.mjs::WHAT[EPI-029] caller-supplied-coverage-and-minority-thresholds-bind` |
| EPI-030 | `requirements/epistemic-reasoning/tests/legacy-golden.test.mjs::WHAT[EPI-030] frozen_transcript_sha_and_projection_match_before_replay`；`requirements/epistemic-reasoning/tests/legacy-golden.test.mjs::WHAT[EPI-030] fifty_eight_accepted_calls_replay_to_identical_revision_tool_sequence_with_golden_anchors`；`requirements/epistemic-reasoning/tests/mcp-stdio.test.mjs::WHAT[EPI-030] restart_recovers_same_durable_inquiry` |
