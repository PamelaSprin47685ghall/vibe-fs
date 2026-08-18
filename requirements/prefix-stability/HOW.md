# prefix-stability — HOW（实现模型与约束；非 normative）

## 1. 实现模型

### 1.1 前缀快照与候选（`Domain/PrefixCandidate.fs`）

```fsharp
type PrefixSnapshot =
    { FrozenRecordPrefixRef: BlobRef
      FrozenRecordPrefixDigest: BlobDigest
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

type PrefixProbe = { ProbeId: string; BasedOnEpochId: PrefixEpochId; Candidate: PrefixSnapshot }

[<RequireQualifiedAccess>]
type XProjectionChoice = UseCommittedEpoch | UsePrefixProbe of PrefixProbe
```

- `CutoffExclusive` 是 X provider-visible messages 的 index，只在产生 `CoveredPrefixDigest`
  的编号下有意义——两者同生共死（COMPANION-011）。
- 类型放 Domain 而非 Journal：`AttemptExecutionProfile` 携带候选（PROMPT-008）、fold 校验
  （PERSIST-010）、selector 构造（CTX-011）——一个类型三处共用，保证 profile 副本与
  committed 副本可比（CTX-012 要求 promoted snapshot 与成功请求用的 byte-identical）。
- `ProviderRequestKind.mayCarryProbe`：只有 WorkMain。

### 1.2 投影意图（`Domain/XPrefixProjection.fs`）

- `forSnapshot`：`None → KeepPhysicalPrefix`；`Some → ActivatePrefixEpoch
  { SyntheticMessageId; Memory; DropLeading = CutoffExclusive }`。
- `forChoice`：probe 与 committed 走同一函数（probe 不是另一种请求，CTX-012 要求
  promoted 与 sent byte-identical）。
- `requiredBlob`：probe 候选必须读**候选**的 blob，读 committed 的 blob 会把旧 prefix
  配到新 synthetic id 下——fold 检测不到，因为是两个各自合法的半套。

### 1.3 epoch 投影（`Context/Prefix/Epoch.fs`）

```fsharp
type ActivePrefixEpoch =
    { EpochId: PrefixEpochId
      Snapshot: PrefixSnapshot option
      ReanchoredRuns: Set<ProviderRunIdentity> }
```

- `Snapshot=None` 是两种历史的诚实合一：从未 promote 与 compaction 已退休（HOST-006）——
  两者行为相同（发 raw history），一个状态。
- `applyRebase`：校验 `previousEpoch = current`、`nextEpoch = successor`、cutoff 不后退、
  candidate 不是 identical（`CandidateNotNew`——不烧 epoch 换零变化）。
- `applyReanchor`：`ReanchoredRuns` 集合防同一 compaction 重锚两次。**epoch check 与
  ReanchoredRuns 防不同失败**：前者防 replay line（crash 在 append 与 fold 之间），
  后者防 repeated decision（同一观察被消费两次，epoch 已前进）。
- `sameCandidate` 排除 SealRoot/SyntheticMessageId（COMPANION-013 由前三字段派生，包含会
  循环比较）。

### 1.4 fold 接线（`Context/Companion/Blogger/ContextFactFold.fs`）

- `PrefixRebaseCommitted` → `tryUpdatePrefix` + `PrefixEpochProjection.applyRebase`。
- `ContextReanchored` → **一个** session 级更新原子完成一个冷边界：prefix 退休 +
  PrefixCoverage 归零（`BlogProjection.applyReanchor`）+ 当前 auxiliary-injection visibility 退休
  （`GuidelineProjection.applyReanchor` / `RequirementGroundingProjection.applyReanchor` /
  `TipDeliveryProjection.applyReanchor`）。Frames / RecordCoverage / XTrace 与 durable injection
  occurrence history 存活（COMPANION-008 / CTX-019）。原子性结构性保证，不靠读者追踪多步。
- TodoCheckpoint（`PrefixRebaseCommittedV2`，`EvidenceKind=TodoCheckpoint`）经同一
  `tryUpdatePrefix` 路径——无第二 SSOT（CTX-015）。

### 1.5 权威判定（`Domain/ProviderProjection.isAppendOnlyPrefix`）

- 比较 `Tools`（相等非前缀）、`System`、`ProviderId`、`ModelId`、`Variant` 与完整 message
  前缀（`next.Messages |> List.truncate (length previous) = previous.Messages`）。
- 生产前置 proof（`Context/Prefix/XWire.fs`）与回归测试共用同一函数
  （cache.md §11：`assertPrefix` fail fast）。

### 1.6 TodoCheckpoint（`Domain/MagicTodoPrefixEpoch.fs`）

- 输入只接受 obligation-ledger 投影出的 committed Accepted 子链：Pre-T1 `planComplete=false` checkpoints
  不进入 prefix rebase。`desiredCutoff(T1)=None`；后续 committed checkpoint 返回 previous committed Accepted；
  `requiresLag1Rebase` 在 committed 子链长度 ≥2 时为真。
- `buildTodoCheckpointCommit`：与 probe 共用 `PrefixRebaseCommittedV2` 形状，
  `EvidenceKind = TodoCheckpoint(Tk, coveredBefore)`；`SolvingProviderRun = None`
  （seal 前提交，provider 结局无关）。

## 2. 与相邻包的分工

| 机制 | owner |
|---|---|
| candidate 何时有资格（CTX-011） | context-compression |
| 压缩结果如何渲染（TOML / wire） | provider-projection |
| 何时观察到 compaction（containment 决策） | context-compression（HOST-006 收容层） |
| desired cutoff 的 committed Accepted 子链（从首次 accepted planComplete=true 起） | obligation-ledger |
| system prompt / Persona 内容 | participant-identity / provider-language |

## 3. 已知非目标（HOW 层）

- `ReanchoredRuns` 集合的持久化（compaction 消息永远留在 transcript，epoch check 不够）是
  实现机制；「同 compaction 只重锚一次」才是命题（PREFIX-STABILITY-006）。
- ordinary synthetic `skill({ name: "" })` + `<skill_content name="">…</skill_content>` / Cursor `NUL+BOM+MarkerText`
  是 HOST-013 的 wire HOW（card 明确可整体替换）；新 occurrence 的 MarkerText 已是最终 skill-content payload。
- `CoverableFrameCount`（vs 存储 CoverableBRef）是等价压缩 HOW（context-compression 侧）。

## 4. 历史与弃权

### 4.1 弃权（GARBAGE / 明确不归本包）

- **Pair Hint 正文（简体中文思考纪律、parallel wave craft）**：属 `cognitive-environment`
  （CHANGES-AUDIT：pair-parallel-tools → cognitive-environment）。本包只拥有「若属于 prefix
  identity 则稳定」。
- **elapsed 采样（`SessionStartedAt → now`）**：HOST-013 的 wall-clock 计量归 `time-capability` TIME-007；
  本包只拥有「历史 marker 永不重算 elapsed」（PREFIX-STABILITY-011 边界）。
- **`PairProgrammingGuidelineAppended` legacy 无 anchor 事实**：是 migration sediment
  （fail closed 不迁移）；本包以 WHAT-010 的 fail-closed 表述，不立「如何迁移」命题。
- **`NeedRebase` / `RebaseRequested` Stage**：被拒方案（TODO-009/012 GARBAGE）；本包
  以「唯一 SSOT」表述（WHAT-004）。
- **按容量切 epoch**：被拒方案（COMPANION-009 考古）；本包以「三证据源」表述（WHAT-002）。

## 5. 依赖理由（DEPENDS ON）

- `provider-projection`：prefix 是 projection 产物；本包只拥有稳定性合同，不拥有意图/表示。
- `context-compression`：候选的资格判定（CTX-011）是替换前提。
- `provider-language` / `participant-identity`：identity/language 材料若进入 prefix identity
  必须稳定；内容本身归它们（INDEX.md 骨架四边）。

## 验证与测试落点

### 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PREFIX-STABILITY-001（同 epoch append-only prefix law） | `tests/prefix-append-only-law.test.mjs`（NEW）：`WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_append_only_law_holds_within_one_epoch`、`WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_modified_historical_bytes_break_the_law`；REUSE：`requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` `WHAT[PREFIX-STABILITY-001] H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix`、`WHAT[PREFIX-STABILITY-001] H13_08_n_round_property_prefix_law_holds`、`requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs`（PREFIX LAW on reused child）`WHAT[PREFIX-STABILITY-001] G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix` | NEW + REUSE | `node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-002（冷边界三证据源） | `tests/prefix-epoch.test.mjs`：`WHAT[PREFIX-STABILITY-002] COMPANION_009_initial_epoch_has_no_snapshot`、`WHAT[PREFIX-STABILITY-002] CTX_012_successful_probe_promotes_its_candidate_verbatim`、`WHAT[PREFIX-STABILITY-006] HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch`、`WHAT[PREFIX-STABILITY-002] CTX_012_probe_capability_returns_after_a_reanchor`；REUSE：`requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` `WHAT[PREFIX-STABILITY-002] COMPANION_009_no_snapshot_means_send_raw_history` | MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-003（candidate ≠ committed） | `tests/prefix-epoch.test.mjs`：`WHAT[PREFIX-STABILITY-003] CTX_010_a_failed_probe_leaves_no_trace_to_undo`；REUSE：`requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` `WHAT[PREFIX-STABILITY-003] CTX_010_a_discarded_probe_leaves_the_committed_epoch_in_place`、`WHAT[PREFIX-STABILITY-003] CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way`、`WHAT[PREFIX-STABILITY-003] CTX_010_the_required_blob_follows_the_choice_not_the_committed_state` | MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-004（ActivePrefixEpoch 唯一 SSOT） | `tests/prefix-epoch-todo-checkpoint.test.mjs`（NEW）：`WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_lag1_rebase_consumes_one_previous_committed_locator`、`WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract`；`tests/prefix-epoch.test.mjs`：`WHAT[PREFIX-STABILITY-004] PERSIST_010_rebase_epoch_must_be_the_successor`、`WHAT[PREFIX-STABILITY-004] CTX_011_an_identical_candidate_is_reported_as_not_new`、`WHAT[PREFIX-STABILITY-004] CTX_011_promoted_cutoff_may_not_retreat`、`WHAT[PREFIX-STABILITY-004] CTX_011_same_cutoff_with_a_tighter_B_is_a_new_candidate`；REUSE：`requirements/durable-events/tests/fold-context-recovery.test.mjs` `CTX_012_rebase_folds_into_the_prefix_projection_only` | NEW + MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch-todo-checkpoint.test.mjs requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-005（seal 后不因 provider 成败回滚） | `tests/prefix-epoch.test.mjs`：`WHAT[PREFIX-STABILITY-005] CTX_012_a_replayed_rebase_is_reported_as_stale`；`tests/prefix-epoch-todo-checkpoint.test.mjs`（NEW）：`WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract`（SolvingProviderRun=None）；REUSE：`requirements/durable-events/tests/fold-context-recovery.test.mjs` `CTX_012_a_replayed_rebase_is_absorbed_so_crash_recovery_is_idempotent` | MOVE + NEW + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-006（ContextReanchored 重锚语义） | `tests/prefix-epoch.test.mjs`：`WHAT[PREFIX-STABILITY-006] HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch`、`WHAT[PREFIX-STABILITY-006] HOST_006_reanchoring_a_session_that_never_promoted_still_advances`、`WHAT[PREFIX-STABILITY-006] PERSIST_010_reanchor_epoch_must_be_the_successor`、`WHAT[PREFIX-STABILITY-006] HOST_006_the_same_compaction_is_never_reanchored_twice`、`WHAT[PREFIX-STABILITY-006] HOST_006_a_recorded_compaction_stays_refused_after_the_epoch_moves_on`、`WHAT[PREFIX-STABILITY-006] HOST_006_a_genuinely_new_compaction_reanchors_again`；REUSE：`requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` `WHAT[PREFIX-STABILITY-006] HOST_006_a_retired_snapshot_and_a_never_promoted_one_produce_the_same_plan` | MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-007（system prompt byte-identical） | REUSE：`requirements/prefix-stability/tests/system-prompt-stability.test.mjs` `WHAT[PREFIX-STABILITY-007] PROMPT_STABILITY_gate_d_is_wired_in_verify_contract`、`WHAT[PREFIX-STABILITY-007] PROMPT_STABILITY_fallback_peer_switch_keeps_system_prompt_bytes`、`WHAT[PREFIX-STABILITY-007] PROMPT_STABILITY_t1_review_reanchor_keep_system_prompt_bytes`；跨包：`requirements/finality/tests/lifecycle.test.mjs` `WHAT[PREFIX-STABILITY-007] manager system prompt stable role law` | REUSE + 跨包 | `node --test requirements/prefix-stability/tests/system-prompt-stability.test.mjs` |
| PREFIX-STABILITY-008（FrozenRecordPrefix 明确标记 + 冻结） | 跨包：`requirements/context-compression/tests/companion-projection.test.mjs`（context-compression 包）`COMPANION_010_memory_block_marks_the_body_as_low_trust_context`、`COMPANION_009_the_same_epoch_and_frames_produce_byte_identical_messages`；REUSE：`requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` `WHAT[PREFIX-STABILITY-008] COMPANION_010_the_memory_is_wrapped_as_low_trust_context` | 跨包 + REUSE | `node --test requirements/context-compression/tests/companion-projection.test.mjs` |
| PREFIX-STABILITY-009（cutoff 完整 turn + digest fail closed） | `tests/projection-algebra-step5-digest.test.mjs`：`WHAT[PREFIX-STABILITY-009] CTX_011_step5_cutoff_digest_truncates_exactly_at_the_cutoff`、`WHAT[PREFIX-STABILITY-009] CTX_011_step5_the_proof_reads_the_SNAPSHOT_not_a_stale_closure`；跨包：`requirements/context-compression/tests/probe-selection.test.mjs`（context-compression 包）`COMPANION_011_a_digest_mismatch_fails_closed`、`COMPANION_011_the_proof_hashes_exactly_the_clamped_cutoff`、`CTX_011_the_candidate_never_swallows_the_message_being_answered` | MOVE + 跨包 | `node --test requirements/prefix-stability/tests/projection-algebra-step5-digest.test.mjs` |
| PREFIX-STABILITY-010（历史 pair 原位 replay，anchor 缺失不重定位） | REUSE：`requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` `WHAT[PREFIX-STABILITY-010] H13_02_historical_pair_never_relocates_to_current_batch`、`WHAT[PREFIX-STABILITY-010] H13_02b_pre_skill_history_replays_its_original_hyphen_wire`、`WHAT[PREFIX-STABILITY-010] H13_03_same_placement_reentry_appends_no_pair`、`WHAT[PREFIX-STABILITY-010] H13_04_restart_replay_is_byte_identical`、`WHAT[PREFIX-STABILITY-010] H13_05_missing_anchor_pair_is_omitted_not_relocated`、`WHAT[PREFIX-STABILITY-010] H13_05b_xwire_drop_leading_continue_still_commits`、`WHAT[PREFIX-STABILITY-010] H13_06_prior_tip_only_affects_the_new_pair`；REUSE：`requirements/prefix-stability/tests/pair-thought-transform.test.mjs` `WHAT[PREFIX-STABILITY-010] PPT_tryInject_appends_pair_on_empty_history_at_start_gap`、`WHAT[PREFIX-STABILITY-010] PPT_tryInject_places_pair_before_trailing_user`、`WHAT[PREFIX-STABILITY-010] PPT_tryInject_places_pair_before_trailing_user_with_prior_assistant`、`WHAT[PREFIX-STABILITY-010] PPT_tryInject_merges_into_tool_batches_before_user`、`WHAT[PREFIX-STABILITY-010] PPT_tryInject_second_pass_of_same_placement_replays_existing_pair`、`WHAT[PREFIX-STABILITY-010] PPT_skip_auto_injected_env_blocks_new_pair_but_replays_history`、`WHAT[PREFIX-STABILITY-010] PPT_skip_auto_injected_env_keeps_empty_transcript_without_pair`、`WHAT[PREFIX-STABILITY-010] C_PH_ordinary_cursor_ordinary_suppresses_then_restores_same_occurrence` | REUSE | `node --test requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-011（冷边界由事实驱动） | `tests/prefix-append-only-law.test.mjs`（NEW）：`WHAT[PREFIX-STABILITY-011] PREFIX_STABILITY_epoch_switches_are_fact_driven_not_estimate_driven`；REUSE：`requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` `WHAT[PREFIX-STABILITY-001] H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix`、`WHAT[PREFIX-STABILITY-001] H13_08_n_round_property_prefix_law_holds`；跨包：`requirements/context-compression/tests/host-compaction-policy.test.mjs`（context-compression 包）`HOST_006_containment_keys_on_the_folded_predicate_not_raw_fields` | NEW + REUSE + 跨包 | `node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-012（reanchor/rebase 提交后不回滚） | `tests/prefix-epoch.test.mjs`：`WHAT[PREFIX-STABILITY-012] PREFIX_STABILITY_committed_reanchor_survives_subsequent_failure`（NEW contract test）、`WHAT[PREFIX-STABILITY-005] CTX_012_a_replayed_rebase_is_reported_as_stale`、`WHAT[PREFIX-STABILITY-006] HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch`；REUSE：`requirements/durable-events/tests/fold-context-recovery.test.mjs` `HOST_006_a_replayed_reanchor_leaves_rebuilt_coverage_alone` | NEW + MOVE + REUSE | `node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs` |
| PREFIX-STABILITY-013（prefix identity 范围） | `tests/prefix-append-only-law.test.mjs`（NEW）：`WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_tool_set_change_breaks_the_law_even_if_messages_prefix`、`WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_identity_or_system_change_breaks_the_law`、`WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_reverse_order_is_not_a_prefix` | NEW | `node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs` |
| PREFIX-STABILITY-014（synthetic 正文不进 trace 系） | `tests/pair-thought-anchored.test.mjs`（NEW contract test）：`WHAT[PREFIX-STABILITY-014] PREFIX_STABILITY_pair_body_stays_out_of_the_trace_projections`（fold anchored pair 事实只长出 Guidelines，XTrace/Blog 不创建）；REUSE：`requirements/prefix-stability/tests/pair-thought-transform.test.mjs` `WHAT[PREFIX-STABILITY-014] PPT_source_is_the_frozen_side_channel_identity`、`WHAT[PREFIX-STABILITY-014] PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker`；跨包：`requirements/semantic-trace/tests/x-trace-capture.test.mjs`（semantic-trace 包）`COMPANION_012_*`（capture 边界无 synthetic 输入） | NEW + REUSE + 跨包 | `node --test requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` |
| PREFIX-STABILITY-015（synthetic id 确定性派生） | 跨包：`requirements/context-compression/tests/companion-projection.test.mjs`（context-compression 包）`COMPANION_013_seal_root_is_derived_from_exactly_the_candidate_identity`、`COMPANION_013_seal_root_changes_when_any_identity_field_changes`、`COMPANION_013_seal_root_is_stable_across_calls`；REUSE：`requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` `WHAT[PREFIX-STABILITY-015] COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id`、`requirements/prefix-stability/tests/pair-thought-transform.test.mjs` `WHAT[PREFIX-STABILITY-015] PPT_tryInject_call_id_is_stable_per_session_and_ordinal`、`WHAT[PREFIX-STABILITY-015] PPT_tryInject_without_session_id_still_appends_stable_pair` | 跨包 + REUSE | `node --test requirements/context-compression/tests/companion-projection.test.mjs` |

### GAP

- `GAP-009` — **CLOSED**：正向义务已恢复为 `time-capability` TIME-007，并由 `requirements/time-capability/tests/pair-session-elapsed.test.mjs` 独立 frozen oracle 承载（首次 prompt durable bind-once、no-scan/no-mutable、fresh elapsed、historical MarkerText immutable）。production 以 `SessionStartedAtBound` → bounded session projection → injected `IClockPort` → `PairProgrammingCalibration.composeWithElapsed` 接入 HOST-013；历史 replay 仍只读已存 `MarkerText`。按用户要求 oracle 冻结后未执行；相关静态 gates 绿，full build 被 unrelated Fission parse error 阻塞。

### 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/prefix-epoch.test.mjs` | MOVE `requirements/prefix-stability/tests/prefix-epoch.test.mjs` | 已跑绿（16 pass，含 PREFIX-STABILITY-012 contract test） |
| `tests/prefix-append-only-law.test.mjs` | NEW | 已跑绿（6 pass，含 PREFIX-STABILITY-011 contract test） |
| `tests/prefix-epoch-todo-checkpoint.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/attempt-plan-prefix.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（7 pass） |
| `tests/pair-thought-anchored.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（9 pass，含 PREFIX-STABILITY-014 contract test） |
| `tests/pair-thought-transform.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（12 pass） |
| `tests/g2-inspector-provider-wire-prefix.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（1 pass） |
| `tests/projection-algebra-step5-digest.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（2 pass） |
| `tests/system-prompt-stability.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（3 pass） |

### 3. 单跑命令

```text
node --test requirements/prefix-stability/tests/prefix-epoch.test.mjs
node --test requirements/prefix-stability/tests/prefix-append-only-law.test.mjs
node --test requirements/prefix-stability/tests/prefix-epoch-todo-checkpoint.test.mjs
node --test requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs
node --test requirements/prefix-stability/tests/pair-thought-anchored.test.mjs
node --test requirements/prefix-stability/tests/pair-thought-transform.test.mjs
node --test requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs
node --test requirements/prefix-stability/tests/projection-algebra-step5-digest.test.mjs
node --test requirements/prefix-stability/tests/system-prompt-stability.test.mjs
```

### 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/prefix-stability/tests/system-prompt-stability.test.mjs` | `WHAT[PREFIX-STABILITY-007] PROMPT_STABILITY_*`（Gate D byte invariants） | SPLIT@cutover：participant-identity（Persona 绑定）+ prefix-stability（system 字节）+ provider-language（语言绑定）三分 |
| `requirements/prefix-stability/tests/pair-thought-anchored.test.mjs` | `WHAT[PREFIX-STABILITY-001] H13_01/08`、`WHAT[PREFIX-STABILITY-010] H13_02/03/04/05/05b/06`（PREFIX LAW + 原位 replay）、`WHAT[PREFIX-STABILITY-014] PREFIX_STABILITY_pair_body_stays_out_of_the_trace_projections` | SPLIT@cutover：前缀律/锚点语义归本包；wire 渲染归 provider-projection；marker 正文归 cognitive-environment |
| `requirements/prefix-stability/tests/pair-thought-transform.test.mjs` | `WHAT[PREFIX-STABILITY-010] PPT_tryInject_*`（placement/replay）、`WHAT[PREFIX-STABILITY-015] PPT_tryInject_call_id_*`、`WHAT[PREFIX-STABILITY-014] PPT_source_*` | SPLIT@cutover：placement/replay 语义归本包；cursor wire 渲染归 provider-projection；PAIR_HINT marker 正文归 cognitive-environment |
| `requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs` | `WHAT[PREFIX-STABILITY-001] G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix`（PREFIX LAW on reused child） | SPLIT@cutover：sync-delegate 生命周期归 delegation；prefix 断言归本包 |
| `requirements/prefix-stability/tests/attempt-plan-prefix.test.mjs` | `WHAT[PREFIX-STABILITY-003] CTX_010_*`、`WHAT[PREFIX-STABILITY-002] COMPANION_009_*`、`WHAT[PREFIX-STABILITY-006] HOST_006_*`、`WHAT[PREFIX-STABILITY-008] COMPANION_010_*`、`WHAT[PREFIX-STABILITY-015] COMPANION_013_*`（profile 选择、frozen prefix、synthetic id） | SPLIT@cutover：AttemptExecutionProfile 归 provider-attempt-recovery；epoch 相关归本包 |
| `requirements/prefix-stability/tests/projection-algebra-step5-digest.test.mjs` | `WHAT[PREFIX-STABILITY-009] CTX_011_step5_*`（cutoff digest + fail-closed） | SPLIT@cutover：digest 字节计算归 provider-projection；边界条件归本包 |
| `requirements/durable-events/tests/fold-context-recovery.test.mjs` | `CTX_012_rebase_folds_into_the_prefix_projection_only`、`HOST_006_*` | 归 durable-events（fold 语义） |
| ~~`tests/unit/enforcer/blogger-convergence-gaps.test.mjs`~~ 等（已迁 context-compression） | C0 观察（squash 不破坏 prefix 前提） | SPLIT@cutover：enforcer 协议面归 behavior-diagnosis |

### 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（PREFIX LAW 由
`ProviderProjection.isAppendOnlyPrefix` + fold 测试承担）。若 cutover 后需要散文 canary，
建议增加 `PREFIX_STABILITY_*` 锚点并声明 owner 为本包。
