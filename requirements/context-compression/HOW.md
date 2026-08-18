# context-compression — HOW（实现模型与约束；非 normative）

## 1. 实现模型

### 1.1 恢复槽（`Domain/RecoverySlot.fs`）

- `SlotArming = NotArmed | ArmedByAdvance`：**不是**持久状态、不写 journal；是单次自动恢复
  序列的局部控制流事实。没有「offset N 是否 armed」的查询——那正是 parked-cursor 缺陷。
- `AttemptOutcome = Completed | CompletedInvalid | Failed | Aborted`：无 `Overflow` case
  （CTX-005 的结构表达）。
- `mayRecover arming offset hasMaterial`：`isArmed ∧ isRecoverySlot offset ∧ hasMaterial`
  （CTX-006 三合取）。
- `onSquashOutcome` / `onMainOutcome kind aabbConsumed` / `advancesCursor` / `nextArming`：
  RequestKind 分派（CTX-007/008）；squash 成功不推进 cursor（同一 slot 内至多一次
  `FallbackCursorAdvanced`）。

### 1.2 候选（`Domain/PrefixCandidate.fs` + `Domain/PrefixProbeSelection.fs`）

- `PrefixProbe { ProbeId; BasedOnEpochId; Candidate }` 只存在于 attempt-local 的
  `AttemptExecutionProfile.ProjectionChoice`（`UseCommittedEpoch | UsePrefixProbe`）。
  DU 而非 option：`UseCommittedEpoch` 永不能 promote，option 会把「无候选」与「槽未 armed」
  混成一个值。
- `ProviderRequestKind.mayCarryProbe`：只有 `WorkMain` 可携带 probe（CTX-009）。
- 选择（CTX-011）：候选 cutoff 严格新于 committed；identical candidate 拒绝；digest 失配
  fail closed；`requiredBlob` 按 choice 取 blob（probe 候选的 blob ≠ committed blob）。

### 1.3 Blog 投影（`Context/Companion/Blogger/Projection.fs` + `Domain/BloggerDelta.fs`）

- `BlogFrameKind = Entry | Squash`；`BlogFrame { Kind; Digest; TextRef; CoveredFrom; CoveredThrough }`。
- `BlogCoverage` 双字段：`IngestedThroughSequence`（RecordCoverage，可 mid-turn）与
  `CoverableTurnCutoffExclusive` + `CoveredPrefixDigest` + `CoverableFrameCount`
  （PrefixCoverage，完整 turn 边界）——两种证明量纲分离（CTX-015/COMPANION-011）。
- `applyEntry` / `applySquash`：squash 覆盖前半 frames（ceil half），级联可继续；
  fold 拒绝 stale frame epoch / non-sequential epoch / ingest 不前进 / coverage 回退 /
  frame count 越界（PERSIST-010，经 ContextFactFold.blogOutcome）。
- 200 KiB 分块：`BloggerDeltaLimitBytes = 200*1024`；chunker 按语义 part 边界切；
  cutoff 只在完整 turn 推进；单 part 超限硬截断并标记；omission marker 永不截断；
  instruction header 不计入 chunk 字节（CTX-013）。

### 1.4 压缩输入投影（`Domain/BloggerRequestContext.fs`、`Session/{Companion,CompanionHost,BloggerCoordinator,CompanionHostBlogger}.fs`）

- delta 可含 tool 作压缩输入；LWR gap 剔 raw tool（COMPANION-007，同源不同投影）。
- BloggerRequestMaterialized / BloggerRequestAbandoned / BlogObservationCommitted /
  BlogObservationsSquashed 四事实构成 Y 的 request cycle；`BloggerCycleProjection` 记录 receipt。
- X 的 same-session prefix replacement 不走 `ForkChildPayload`：`Context/Prefix/Wire.fs` 以本 X 的
  Opening + coverable Y frames 直接 `LifecycleWorkRecord.render true`，随后
  `CompanionPrompt.companionMemoryBlock` 包成 memory preamble + `<work-log>…</work-log>`。
  `commissioner_record` / `attached_work_record` 是父→子 delegation envelope 的字段，不是
  self-memory 的字段。

### 1.4.1 连续 catch-up：live Current → refresh → park → wake → live Current

- `BlogObservationCommitted` 后先从 canonical Blog coverage + XTrace Current 重新 `nextChunk`；有 material
  立即继续下一 ≤200 KiB cycle。不得保存 wake-time/head-time `DrainThroughSequence`、`DrainFrontier`、
  target head 或等价 frozen upper bound。
- 当前 refresh 返回 None 只说明**此刻** caught-up。若 main 未合法终止，`ParkTransform` 保持当前
  continuation 悬挂；`PendingOffer` 只负责唤醒，不作为下一块内容权威。
- wake 后丢弃 stale offer，重新读取 live Current 并 `RefreshMainContext`。因此 park 期间新增、sequence
  超过 park 前 XTrace head 的 material 仍属于同一连续 catch-up，必须立即进入下一 cycle。
- 这条路径使用 F# CE `let!`/`match!` 直接表达等待与继续；不维护 Stage/PC，不构造 drain state machine，
  不扫描/重放 Journal。业务读取只用 canonical Integrator 已维护的 Current（DURABLE-EVENTS-019）。
- quiet 不是直接 stop：normal commit、idempotent receipt、stale catch-up、protocol-repair re-entry 必须汇合到
  同一个 `ParkTransform` 边界。在同一存活执行内先 park，只有 durable seal / cancel 或 park waiter 既有
  physical lifetime 才能解除等待；这些是既存终止/物理边界，不得被解释成“caught-up 已完成”的业务判据。
- waiter/drain/flight 三者分权：`CancelParked` 只取消 waiter 并清 PendingOffer；`forceSealRuntime` 只关闭
  drain、清 PendingOffer、取消 waiter。二者都保留已有 `CurrentRequest` flight，直到 commit、abandon/fail
  或 session disposal 显式 `ClearCurrentRequest`/删除 SharedState flight。
- process death 直接中断旧 tool/continuation；普通 Host restart 不重新挂起这个 waiter、不 replay 旧 cycle、
  不补 terminal。跨进程语义完全服从 CRASH-017/018；显式 `/continue` 也不续跑旧 Blogger invocation。

### 1.5 Host compaction containment（`Domain/HostCompactionPolicy.fs`）

- 预防层：`compaction.auto` / `compaction.prune` / `compaction.autocontinue` 必须为 false，
  无法证明关闭 → `HostContractUnsupported` 启动失败。
- 收容层：任意观察到的 compaction pseudo-run → 原子 `ContextReanchored`（HOST-006）；
  `nextReanchor` 消费 `PrefixEpochProjection.isReanchored`（同 compaction 只重锚一次）。
- 同一 `ContextReanchored` fold 同时退休旧 auxiliary-injection visibility：
  `GuidelineProjection.applyReanchor` 清当前 horizon pair replay set；
  `RequirementGroundingProjection.applyReanchor` 清当前 grounded/visible read set；
  `TipDeliveryProjection.applyReanchor` 清 tip semantic coverage。三者都保留 durable occurrence 历史，
  所以普通 restart 仍可审计历史，而 Y 后 provider wire 不会恢复旧辅助注入。
- post-reanchor 新 pair / tip / requirement read 只能由之后的普通触发重新产生；不得在 reanchor fold 中
  eager 重注入旧全集。
- `prune` 特殊：绕过 transform 直接删行，收容层无法修复 → 必须预防关闭。

### 1.6 诊断边界（`Kernel/Diagnostic`，见 ctx014 测试）

- `Diagnostic.emit` 只接受白名单字段；未知字段 → fatal。
- 禁止字段一旦出现在 production source → `ctx014.test.mjs` tombstone 拦截。

## 2. 与相邻包的分工

| 机制 | owner |
|---|---|
| 候选 epoch 提升（`PrefixRebaseCommitted`/`ActivePrefixEpoch`） | prefix-stability |
| XTrace 事实源 / cursor | semantic-trace |
| TOML 布局渲染 | provider-projection |
| armed/primed（FALLBACK-012）、失败预算 | provider-attempt-recovery |
| `ContextReanchored` 的 epoch 语义 | prefix-stability（本包只拥有「什么观察触发重锚」） |

## 3. 已知非目标（HOW 层）

- 200 KiB 数值当前是合同（CTX-003），但 card 注明「只有被证明是产品合同的上界才进入未来
  WHAT」——若未来有更强合同，数值可演进。
- squash 的「前半 frames」策略（ceil half）是当前算法；「squash 只处理本 X frames」
  才是命题（CONTEXT-COMPRESSION-014）。
- `CoverableFrameCount` 是对 `CoverableBRef` 的等价压缩（append-only frame 列表内可推导），
  是 HOW；「probe 只用 cutoff 前覆盖 frames」才是命题。

## 4. 历史与弃权

### 4.1 弃权（GARBAGE / 明确不归本包）

- **`session.compacted` 冒充 TodoCheckpoint**：shape/context.md 明确禁止；epoch 语义归
  prefix-stability，本包不重复。
- **`NeedRebase` / `RebaseRequested` Stage 与 todo-only 平行 epoch**：CTX-015/TODO-009 拒；
  是 GARBAGE（被拒方案），本包不立命题。
- **`PrefixProbeRolledBack`**：被拒方案（CTX-010）；本包以「失败无事实」表述，不发明回滚事实。
- **「按容量切 epoch」**：被拒方案（COMPANION-009 考古）；由 prefix-stability 的冷边界
  三证据源覆盖，本包不重复。
- **`context_ratio` 式诊断字段**：X9 已删；ctx014 tombstone 保留为机制（本包 CONTEXT-COMPRESSION-013）。

## 5. 依赖理由（DEPENDS ON）

- `semantic-trace`：ingest cursor 是 XTrace 游标；delta 与 gap 同源。
- `provider-projection`：压缩结果（TOML delta、prefix projection）是 provider 表示；
  本包不拥有渲染。

## 验证与测试落点

### 1. 命题 → 测试

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
| CONTEXT-COMPRESSION-017（Opening floor / same-session self-memory identity） | `tests/ctx-opening-floor.test.mjs`（NEW）：`CTX_016_pre_t1_floor_is_the_xtrace_head_not_an_activation_cursor`、`CTX_016_work_activated_is_inert_and_does_not_move_the_floor`、`CTX_016_blogger_effective_start_is_max_of_record_coverage_and_floor`；`tests/companion-projection.test.mjs`：`COMPANION_010_same_session_memory_is_work_log_not_a_delegation_record`；跨包：`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs`（semantic-trace 包）`COMPANION_003_capture_opening_takes_authoritative_requirements` | NEW + MOVE + 跨包 | `node --test requirements/context-compression/tests/ctx-opening-floor.test.mjs requirements/context-compression/tests/companion-projection.test.mjs` |
| CONTEXT-COMPRESSION-018（连续 catch-up：无 frozen frontier；所有 quiet re-entry 先 park，wake 后读 live Current） | `tests/enforcer-cycle-commit-convergence.test.mjs`：`ENFORCER_same_run_after_squash_rejected_as_known_not_committed`（idempotent receipt quiet 必须实际调用 ParkTransform 后才可因模拟 physical expiry stop）/ `ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier`（park 前 head=2，park 期间新增 3..4，同一 continuation 立即派生 2→4 下一块）；`tests/blogger-convergence-gaps.test.mjs`：`C0_caught_up_is_parked_not_completed_and_wake_rechecks_live_Current`、`C0_commit_drains_via_tryRefresh_before_park`；跨包边界 REUSE `requirements/crash-reconciliation/tests/explicit-continue.test.mjs`：`CRASH_017_new_process_runtime_dispose_does_not_claim_or_abort_old_active_handle`、`CRASH_018_continue_discloses_restart_keeps_broken_tool_visible_and_process_locally_reenlists_survivor` | NEW + REUSE + 跨包 | `node --test requirements/context-compression/tests/enforcer-cycle-commit-convergence.test.mjs requirements/context-compression/tests/blogger-convergence-gaps.test.mjs requirements/crash-reconciliation/tests/explicit-continue.test.mjs` |
| CONTEXT-COMPRESSION-019（Y 后辅助注入 coverage 归零，历史 occurrence 保留） | `tests/injected-context-reanchor.test.mjs`：pair old synthetic 不 replay + 新 occurrence fresh call id；requirement old reads 不 replay + 同 digest 可重新 request；REUSE `requirements/guidance-delivery/tests/tip-guidance-delivery.test.mjs::ENFORCER_TIP_DELIVERY_006_context_reanchor_clears_full_so_next_is_full_again` | NEW + REUSE | `node --test requirements/context-compression/tests/injected-context-reanchor.test.mjs requirements/guidance-delivery/tests/tip-guidance-delivery.test.mjs` |

### 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/blog-projection.test.mjs` | MOVE `requirements/context-compression/tests/blog-projection.test.mjs` | 已跑绿（20 pass） |
| `tests/companion-projection.test.mjs` | MOVE `requirements/context-compression/tests/companion-projection.test.mjs` | 已跑绿（28 pass） |
| `tests/blogger-delta.test.mjs` | MOVE `requirements/context-compression/tests/blogger-delta.test.mjs` | 已跑绿（19 pass） |
| `tests/probe-selection.test.mjs` | MOVE `requirements/context-compression/tests/probe-selection.test.mjs` | 已跑绿（13 pass） |
| `tests/recovery-slot.test.mjs` | MOVE `requirements/context-compression/tests/recovery-slot.test.mjs` | 已跑绿（20 pass） |
| `tests/host-compaction-policy.test.mjs` | MOVE `requirements/context-compression/tests/host-compaction-policy.test.mjs` | 已跑绿（14 pass） |
| `tests/ctx014.test.mjs` | MOVE `requirements/context-compression/tests/ctx014.test.mjs` | 已跑绿（7 pass） |
| `tests/terminal-validity.test.mjs` | MOVE `requirements/context-compression/tests/terminal-validity.test.mjs` | 已跑绿（6 pass） |
| `tests/ctx-capacity-observation-forbidden.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/ctx-opening-floor.test.mjs` | NEW | 已跑绿（3 pass） |

### 3. 单跑命令

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
node --test requirements/context-compression/tests/injected-context-reanchor.test.mjs
```

### 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/context-compression/tests/attempt-plan-probe-eligibility.test.mjs` | `CTX_010_*`（probe 只在 armed work-main slot） | SPLIT@cutover：AttemptExecutionProfile 归 provider-attempt-recovery；本包引用 probe 资格 |
| `requirements/durable-events/tests/fold-context-recovery.test.mjs` | `PERSIST_010_*`（fold 语义） | 归 durable-events |
| `requirements/provider-projection/tests/synthetic-toml.test.mjs`、`requirements/provider-projection/tests/blogger-toml.test.mjs` | TOML 布局/转义渲染 | 归 provider-projection（CTX-013 的渲染半边）；blogger-toml 待 provider-projection cutover 迁移 |
| ~~`tests/unit/enforcer/blogger-convergence-gaps.test.mjs`~~、~~`blogger-runtime.test.mjs`~~ | Blogger request-cycle 收敛（C0/ENFORCER-047；已迁本包 tests/） | SPLIT@cutover：enforcer 协议面归 behavior-diagnosis；压缩输入面归本包 |
| `requirements/semantic-trace/tests/x-trace-locality.test.mjs`（semantic-trace 包） | `TODO-004/008` XTrace range 与 LWR 交叉 | 本包引用 effectiveStart/floor |

### 5. semantic anchor id

本包未在 `scripts/checks/semantic-anchors.mjs` 声明独立 anchor（CTX 语义由 F# 类型 + fold
测试承担）。若 cutover 后需要散文 canary，建议增加 `CONTEXT_COMPRESSION_*` 锚点并声明
owner 为本包（CTX-001/002 的墓碑扫描已是机器可执行证明）。
