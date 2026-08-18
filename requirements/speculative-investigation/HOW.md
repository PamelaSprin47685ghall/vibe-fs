# speculative-investigation — HOW

> 非 normative。描述当前实现模型与约束，以及「历史与弃权」裁决。
> 当前实现名（Strength、same-role-fast、K1/K2 数值、predictor 特征）全部是 HOW，不是 WHAT。
> 若未来换实现，WHAT.md 不变。源：历史 how/shape strength 条款、历史 change（strength）、
> `src/Wanxiangshu/**`。

## 1. 模块地图（当前实现）

```text
src/Wanxiangshu/Domain/
  StrengthBudget.fs        StrengthBudget ∈ {K0,K1,K2}；requestLimit；K1/K2 margin 门
  StrengthPolicy.fs        StrengthOpportunity / StrengthDecision；eligibility / controlBucket /
                           isControlHoldout / decideFromFacts / budgetOf / isSpeculate
  StrengthCostModel.fs     StrengthValueInputs / StrengthValueEstimate{V0,V1,V2}；estimateFrom
  StrengthPredictor.fs     StrengthPrimarySymbol / StrengthFeatureKey / StrengthPredictorBucket；
                           observeFirst / observeSecond / predict（纯、确定性状态更新）
  StrengthRollout.fs       StrengthRolloutMode（Shadow/DryRun/...）/ StrengthExplicitCostTemplate；
                           estimate / isShadow
  StrengthFrame.fs         StrengthToolExchange / StrengthRequestBatch / StrengthFrameBundle；
                           isAllowedTool（read/glob/grep）；tryBuild（完整配对校验）；utf8ByteCount；
                           canonicalText（去 wire-id 的 canonical）；tryLocalizeMirror
  StrengthBatchCollector.fs  collectCompleteBatches：按 provider request 边界收完整 call/result 配对
  StrengthEvents.fs        StrengthCandidatePrepared / Promoted / FramesTraced / CandidateAbandoned；
                           StrengthEventTypes.all；事件只含 opaque PayloadRef
  StrengthProjection.fs    StrengthProjection；tryCandidate / hasPrepared / isPromoted /
                           tryDecisionForTarget / tryTraceRange；apply（纯 fold，不扫全 EventStore）
  StrengthCommit.fs        StrengthAppendOutcome / StrengthDurableEvidence / StrengthCommitDecision；
                           resolvePrepared / resolvePromotion（CommitUnknown 三态裁决）
  StrengthPromotion.fs     StrengthProviderOutputEvidence / StrengthPromotionDecision；decide
                           （wrong run / NoOutput / TransportOnly → 不 Promote）

src/Wanxiangshu/Application/Strength/
  StrengthDurabilityPort.fs    Application 只依赖 typed port（local EventStore / PayloadRef 物理只在 Persist）
  StrengthLifecycle.fs         reconcileEvent（ReconciledTurn → Promotion/Abandoned）；replayPlans；
                               needsRawReplay（以 Companion coverage 判退休）；replayIntents
  StrengthReplicaRuntime.fs    decision-local InternalLeaf 物理资源 + request budget + fuse gate
  StrengthReplicaTransform.fs  StrengthSpeculate 的 Replica 侧：frozen mirror → batch 收割 → insert
  StrengthTraceRecovery.fs     recoverRange：XTrace 已写、Traced 未写时按 stable identity / 唯一
                               canonical contiguous range 补 fact
  StrengthTurnEvidence.fs      classifyParts（NoOutput/TransportOnly/RealOutput）；primarySymbol；
                               promotionDecision

src/Wanxiangshu/Session/StrengthRuntime.fs          decision 生命周期（single-flight、retire）
src/Wanxiangshu/Infrastructure/Persist/
  StrengthDurability.fs / StrengthStore.fs          Prepared/Promoted payload 与 EventStore 收口
src/Wanxiangshu/Infrastructure/OpenCode/Host/
  StrengthSettings.fs / PluginStrengthScope.fs      env 设置、Host canary fingerprint、process fuse
```

主 transform 顺序固定（历史 how/strength 条款）：

```text
StrengthReplay → XTraceCapture → Companion → XWire → EnforcerHost → StrengthSpeculate
→ PairProgrammingThoughtTransform → HostMessageProjection.sanitizeMessages
```

- `StrengthReplay` 只读 durable Promoted view，把 frame 插在 TargetProviderRun 对应 assistant
  output 之前；Candidate 永不早期 replay。
- `StrengthSpeculate` 在 post-Enforcer view 上先冻结 `ProviderSemanticProjection`。Treatment 路径可等待
  Replica 并声明 Candidate insertion；**DryRun 路径只启动真实、OpenCode 可见的 Replica 后立即返回**，
  不等待 Replica terminal/deadline，也不声明 Candidate。

## 2. Opportunity → Decision 管线

Coordinator 构造不可变 `StrengthOpportunity`（owner/session ownership、AuthorityRoot、
TargetProviderRun、CanonicalRole、Selected/EffectiveAgent、tier/model binding/cost metadata、
request kind、fallback/recovery/finality facts、frozen semantic history/bytes、
EventStore/canary health）。`StrengthPolicy.decideFromFacts` 顺序：

```text
eligibility gate → deterministic control bucket → shadow/treatment mode
→ predictor P1/P2 + evidence floor → value(V0,V1,V2) → margin gate
→ Skip | ControlHoldout | Speculate K1/K2
```

任何缺失/不可信证据直接 `Skip`。control hash 使用 canonical frozen key + PolicyVersion，
不调用 RNG/clock（SPEC-INV-010）。

## 3. Replica request loop（决策内）

1. 解析 same-role fast peer；创建/复用仅当前 decision 的 `InternalLeaf × Attached(StrengthReplica)`。Host 必须
   以普通 child/session 表面把该 physical execution 暴露给 OpenCode 用户；“internal”不等于 UI 隐藏。
2. **继承** owner `SessionPersona` / `SessionProviderLanguage`；只换 ExecutionBinding
   （`fast-<owner-role>`）。禁止新建 Persona、重写 system 身份字节、换世界语。
3. `ProviderRequestKind.StrengthReplica` profile；tool schema 从 `ToolCapabilitySet` 生成，
   execution gate 读同一 set（恰好 `read/glob/grep`）。
4. `UseStrengthMirror`：frozen owner semantic history 作 base；Host 边界临时读 owner wire 保留
   call/result 配对，随后按 `DecisionId + semantic digest + encounter ordinal` 把 owner
   ToolCallId 全部重定位为 decision-local deterministic id；`ProviderSemanticProjection` 前后
   相等。media / 无法唯一配对的历史不可逆 → K0。
5. 每次 provider request 完成后只收集真实 readonly call/result。并发调用按 Host/provider 稳定
   顺序收割；任一未配对、未知 tool、超 byte limit → 本 decision unusable。
6. 请求计数达 K 后，在下一 transform/reconcile 边界停止并 retire，禁止 K+1 外发。text-only
   completion 丢弃正文并停止；之前完整 batch 可保留。
7. owner cancellation 立即 abort/retire leaf。Replica provider/tool 普通失败不进入 owner fallback。
8. **DryRun scheduling**：`StrengthSpeculate` 只负责 `StartDryRun` 并确认 child 已成功注册/启动；其后 request loop
   由 `StrengthReplicaRuntime`/Host lifecycle 独立推进，terminal/budget timeout 只结束该 observation。owner transform
   不 `let!` / await DryRun Task。Treatment 仍使用需要结果的 awaitable decision API。

## 4. Frame canonicalization 与 durable 事实

- **DryRun 永远停在 observation**：可以收集 batch/diagnostics 供用户和 Host 审计，但不调用 Prepared/Promoted/
  replay pipeline，不把 frame 映射回 owner。下面 Candidate/Promotion 流程只属于 Treatment。
- 每个 exchange 规范化为 `ToolName + CanonicalArguments + CanonicalResult`；batch 保留
  `RequestOrdinal`，exchange 保留 stable ordinal。semantic digest 对去 wire-id 的 canonical
  bundle 计算。owner synthetic ToolCallId 由 `ownerSessionId + decisionId + requestOrdinal +
  exchangeOrdinal + semanticDigest` 经稳定 hash 派生；同 DecisionId 不同 digest 是冲突。
- Prepared append：`write raw frame payload → append StrengthCandidatePrepared(envelope.payload_refs)`
  → resolve append outcome → 成功后才声明 InsertStrengthFrames(Candidate, targetRun)。
  append 明确失败 → K0；CommitUnknown → 查 projection（同 digest+refs → 继续；明确不存在 →
  K0；无法证明 → 阻止 target request）。
- Promotion reconcile：`无 Prepared → no-op；Prepared + same run InProgress → no-op；
  Prepared + same run NeedsContinuation/Completed 且有 usable output → append Promoted；
  Failed/Aborted 或 Completed 无 usable output → append Abandoned`。Promoted 校验
  Prepared/run/digest/payload refs 完全一致；重复同事实幂等。
- Replay → Traced：多 Promoted decision 的 `BeforeMessageIndex` 是原始 base 的绝对索引；
  planner canonical sort、renderer 按 index 倒序插入。XTraceCapture 写入正常 XTrace parts 后
  append `StrengthFramesTraced`。raw replay 退休只由语义 coverage 证明（当前读 Companion
  `IngestedThroughSequence` 覆盖到 traced range 最后一项）；物理 message cutoff 不参与。

## 5. Predictor 与 value 方程（当前数值）

- Primary request 符号化为 readonly/read-search/mutate/execute/text/other；Shadow/control 观察
  下一次 primary request `R1`（非空纯 readonly batch 再观察 `R2`）；Replica request 永不进入
  该序列。第一版 predictor 按 CanonicalRole + 最近 1..3 primary symbols + tool-result
  structural features + visible bytes 分桶；纯、确定性状态更新。
- value：`V0=0`；`V1=P1*SavedDeep1−Fast1−Byte1−Delay1−Risk1`；
  `V2=P1*SavedDeep1+P1*P2*SavedDeep2−Fast1−P1*Fast2−Byte2−Delay2−Risk2`。选最大值后应用
  `K1Margin` / 更高 `K2Margin` 与 K2 evidence floor。没有可靠 fast/deep cost metadata 时
  treatment 强制 K0，shadow 仍可记录 prediction。默认 Host settings = Shadow（SPEC-INV-010）。

## 6. 崩溃矩阵（当前行为）

| 崩溃点 | 行为 |
|---|---|
| DryRun 正在执行 | owner 不依赖其 terminal；进程退出即自然丢弃 observation，已无 semantic promotion |
| Treatment Replica 尚未 Prepared | 重启丢弃，只读副作用为零 |
| Prepared durable、target 未消费 | 同 run + 同 AnchorDigest 重放同 Candidate；run 明确终止且未消费 → Abandoned |
| provider 已产出可用 output、Promotion 前 crash | reconcile 以 ProviderRunIdentity 补 Promotion；Failed/Aborted 不补 |
| Promoted、XTrace 未捕获 | StrengthReplay 重建 |
| XTrace 已捕获、Traced 缺失 | stable identity 或唯一 canonical contiguous range 补 fact |
| durable outcome 无法证明 | fail closed；普通 pre-commit Replica failure：fail open K0 |
| process-local fuse | 记住首个 durable/projection/schema/frame 不一致，只禁止**新** speculation；Prepared recovery、Promoted replay/promotion/tracing 永不被 fuse 关闭 |
| Host canary | `WANXIANGSHU_STRENGTH_HOST_CANARY` 必须逐字等于当前 `opencode-ai` + `@opencode-ai/plugin` 版本指纹；依赖版本变化自动回到 K0 |

## 7. 依赖（DEPENDS ON，逐条理由）

来自 `requirements/INDEX.md` 依赖骨架（不增删 edge）：

- `repository-investigation`：投机的是「接下来需要哪些只读调查」；被消费后的 frame 是
  repository fact acquisition 的合法输入。
- `participant-identity`：Replica 继承 owner 的 persona/language、只换 execution binding——
  「换执行者不等于换人」由该包保证。
- `execution-model-routing`：`fast-<owner-role>` 的实际 ModelTarget、lease occupancy 与 process-shared admission 由 MJS scheduler/runtime 保证；Strength 不再读静态 fast/deep model string。scheduler 返回 `null` 时该 optional replica 直接 K0，不进入 required wait queue。
- `participant-horizon`：Replica 可见面是 owner horizon 的投影；跨 Session 只比语义投影。
- `provider-projection`：UseStrengthMirror / InsertStrengthFrames 的代数与确定性由该包保证。
- `semantic-trace`：Promoted 最终进入 XTrace；unpromoted ∉ history 的另一半在该包。

## 8. 历史与弃权（考古记录，非 normative）

- **算法/常量降为 HOW**：`same-role-fast` 模型选择、K1/K2 数值、margin/evidence floor、
  predictor 特征分桶、canary 指纹格式——全部是当前实现，不进 WHAT（边界卡片
  DOES NOT OWN 与 HANDOFF §6.7 同类裁决）。
- **STRENGTH-013..019（历史 shape/strength 条款）**：这些是「所有权分配」条款，不是本包新增
  行为——Session 归属 → `session-ontology`、profile 构造 → `participant-identity`、
  projection intent → `provider-projection`、durable substrate → `durable-events`、
  XTrace/Companion coverage → `semantic-trace`/`context-compression`、fallback/review 隔离 →
  `provider-attempt-recovery`/`review-*`。信息已分别落入本 WHAT 各命题的「边界」节。
- **被拒方向**：见 WHY.md §3（历史 change（strength）§三十逐条）。
- **Semble 弃权**：Strength 不消费 Semble（AGENT-027）；历史伪造 read 的失败模式见
  WHY.md §1.9。
- **Student/Teacher**：已删除领域；`Student & Teacher.md` 为 GARBAGE（CHANGES-AUDIT）；
  absence ratchet 归 `session-ontology` 的 `student-teacher-absence` gate，本包不背墓碑。
- **历史 loop 条款（why/what/how/proof）**：loop 主题是退化循环检测
  （`degeneration-guard`），全篇 grep `speculat/投机/strength` 零命中——无本包可吸收的
  speculation 内容，弃权。
- **dry-run / e2e**：DryRun 定义现为“real + OpenCode-visible + owner-nonblocking + zero-promotion”。
  `requirements/verification-system/tests/e2e/entry.test.mjs` long-stroke `strength-canary-*` 继续证明 K2 物理 request budget；
  本包新增 frozen unit contract 证明 owner continuation 不等待 unresolved Replica deadline，且 DryRun 不 Prepared/Promoted。
- **GARBAGE 结论**：旧稿 `FrameBundleRef` / `PredictorSnapshotRef` / Journal NDJSON /
  RuntimePath blob 类型名已被存储收口删除（历史 change（strength）§二十二）——只留
  EventStore `payload_refs`；不进入 WHAT。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包 `tests/`，删原文件）、
> `REUSE`（留在原处，记精确锚点 + SPLIT@cutover 计划）、`NEW`（本包新写）。
> 单跑命令：`node --test <file>`。全量：`node requirements/verification-system/tests/run.mjs`（自动发现
> `requirements/<package>/tests/**/*.test.mjs`）。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| SPEC-INV-001 零影响基线 | `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`（`STRENGTH_001_014_policy_nested_replica_cannot_speculate`、`STRENGTH_002_011_policy_k0_default_when_host_canary_or_cost_is_unproven`）+ 本包 `host-policy.test.mjs`（`STRENGTH_011_default_settings_are_shadow_k0_with_economic_holdout_and_no_k2_enablement`） | REUSE + MOVE | `node --test requirements/speculative-investigation/tests/host-canary-k0.test.mjs`；`node --test requirements/speculative-investigation/tests/host-policy.test.mjs` |
| SPEC-INV-002 Eligible opportunity | 本包 `authority-policy.test.mjs`（`STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_deep_opportunities`）+ `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`（`STRENGTH_002_013_review_finality_and_attached_internal_leaf_are_always_k0`、`STRENGTH_002_003_target_unbound_and_replica_request_kind_are_k0`） | MOVE + REUSE | 对应两文件 `node --test` |
| SPEC-INV-003 预算单位 K | `requirements/speculative-investigation/tests/batch-collector.test.mjs`（`STRENGTH_003_005_collector_preserves_provider_request_batches_and_concurrent_order`）+ `requirements/speculative-investigation/tests/replica-transform.test.mjs`（`STRENGTH_003_K1_aborts_before_provider_request_2_after_one_complete_batch`、`STRENGTH_003_K2_allows_request_2_then_aborts_before_request_3`、`STRENGTH_003_K2_counts_parallel_OpenCode_tool_parts_as_one_request_then_stops_before_request_3`） | REUSE | `node --test requirements/speculative-investigation/tests/batch-collector.test.mjs`；`node --test requirements/speculative-investigation/tests/replica-transform.test.mjs` |
| SPEC-INV-004 Replica authority | 本包 `authority-policy.test.mjs`（`STRENGTH_004_<role>_replica_has_exact_readonly_capabilities`、`STRENGTH_004_<role>_replica_is_fail_closed`、`STRENGTH_004_019_replica_is_never_owner_fallback_or_prefix_probe_evidence`）+ `requirements/speculative-investigation/tests/runtime.test.mjs`（`STRENGTH_014_runtime_is_owner_single_flight_and_decision_local`、`STRENGTH_004_runtime_rejects_K0_and_ineligible_replica_authority`）+ `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`（`STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network`、`STRENGTH_004_006_policy_replica_host_tool_map_denies_unknown_tools_instead_of_asking`、`STRENGTH_004_007_policy_same_role_prompt_has_no_replica_identity`、`STRENGTH_014_policy_strength_replica_is_internal_leaf_attached_not_satellite_kind`） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-005 Candidate frame | `requirements/speculative-investigation/tests/frame-projection.test.mjs`（`STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches`、`STRENGTH_005_frame_digest_and_owner_wire_ids_are_restart_stable`）+ `requirements/speculative-investigation/tests/projection-adapter.test.mjs`（`STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest`） | REUSE | `node --test requirements/speculative-investigation/tests/frame-projection.test.mjs`；`node --test requirements/speculative-investigation/tests/projection-adapter.test.mjs` |
| SPEC-INV-006 Prepared ≠ 历史 | 本包 `commit-promotion.test.mjs`（`STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing`）+ 本包 `store.test.mjs`（`STRENGTH_006_store_envelope_puts_large_material_only_in_payload_refs`、`STRENGTH_006_same_decision_different_prepared_material_is_identity_collision`、`STRENGTH_006_integrator_Current_reflects_Prepared_binding_without_history_scan`）+ 本包 `durability-port.test.mjs`（`STRENGTH_006_008_durability_port_publishes_payload_closure_and_reloads_the_same_bundle`、`STRENGTH_006_durability_port_rejects_conflicting_Prepared_identity`）+ 本包 `frame-projection.test.mjs`（`STRENGTH_006_projection_binds_prepared_identity_and_rejects_conflict`）+ 本包 `lifecycle-recovery.test.mjs`（`STRENGTH_006_008_prepared_candidate_cannot_be_traced_or_raw_replayed`）+ `requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs`（Candidate 永不进入 XTrace/LWR） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-007 Promotion 只由消费证据 | 本包 `turn-evidence.test.mjs`（`STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping`）+ 本包 `commit-promotion.test.mjs`（`STRENGTH_007_promotion_commit_unknown_never_allows_continuation_without_durable_fact`、`STRENGTH_007_promotion_requires_the_exact_target_run_and_real_provider_output`）+ 本包 `frame-projection.test.mjs`（`STRENGTH_007_projection_promotion_requires_prepared_and_exact_target`）+ 本包 `lifecycle-recovery.test.mjs`（`STRENGTH_007_lifecycle_promotes_only_exact_target_with_real_provider_output`）+ 本包 `store.test.mjs`（`STRENGTH_007_promotion_without_prepared_is_missing_parent`、`STRENGTH_007_integrator_Current_reflects_Promoted_without_history_scan`） | MOVE + REUSE | 对应文件 `node --test` |
| SPEC-INV-008 Replay 与 XTrace closure | `requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs`（`STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor`、`STRENGTH_008_compaction_does_not_retire_raw_replay_without_xtrace_coverage`、`STRENGTH_008_trace_recovery_requires_one_exact_contiguous_canonical_match`）+ 本包 `store.test.mjs`（`STRENGTH_008_integrator_Current_reflects_Traced_range_without_history_scan`）+ `requirements/speculative-investigation/tests/projection-algebra.test.mjs`（`STRENGTH_008_009_multiple_promoted_absolute_anchors_are_registration_order_independent`）+ `requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs::STRENGTH_INTEGRATION_Prepared_candidate_consumption_Promoted_restart_replay_Traced` | REUSE | `node --test requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs`；`node --test requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs` |
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

### GAP

- `GAP-015` —— **CLOSED**：production DryRun 已改为 distinct `StartDryRun`：真实 `CreateChildSession` / `registerReplica` / Detached OpenCode execution，owner 只等待物理 child bootstrap 后立即继续；terminal/deadline 在独立 observation task 中结束；DryRun 不 Prepared/Promoted、不映射回 owner。落点 `dry-run-shadow.test.mjs` 已执行并通过。

### Semantic anchor ids

本包在 `scripts/checks/semantic-anchors.mjs` 中**当前无已声明 anchor 组**（catalog 的
inquiry 组归 `epistemic-reasoning`；Strength 无对应 anchor id）。如未来为 speculation 增加
anchor，应在 `ROLE_SEMANTIC_ANCHORS` 声明并在此登记。

### SPLIT@cutover 待办

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

### 验证状态

- 既有历史验证记录保留：4 个 MOVE 文件曾单跑绿（`authority-policy` 13、`commit-promotion` 3、`host-policy` 5、`turn-evidence` 1；2026-08-14）。
- `SPEC-INV-013` DryRun oracle 已在 2026-08-15 本轮收敛中执行：`dry-run-shadow.test.mjs` 四条均通过；其结果只证明该包的 DryRun 合同，全仓结论仍以完整门禁为准。
