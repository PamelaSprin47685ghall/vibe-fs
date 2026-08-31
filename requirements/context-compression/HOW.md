# context-compression — HOW

## 架构与核心机制

### 恢复槽与候选机制

1. **FallbackLedger 先裁决**：真实失败先由唯一写入口推进 cursor；只有 `RecoveryAdvanced` 才有资格派发下一物理请求，Host observer 不提前 arm。
2. **RecoveryOpportunity 分型**：`armedByFailure ∧ primed` 先归约为 `OrdinaryAttempt | RecoveryAttempt`。材料资格不是第二个布尔开关：X 的 `PrefixProbeSelection` 返回候选或精确 `NoCandidateReason`，Y 直接从 typed request + durable frames 构造 Squash。
3. **一次性消费**：RecoveryOpportunity 只属于紧随 advance 的一个 attempt。NoCoverage / stale candidate 发送普通主请求，不跨 attempt re-arm。
4. **分派与提交**：按 RequestKind 分派后继；Probe 成功原子提升 ActivePrefixEpoch，Squash 成功则压缩前半段 frames 并递增 FrameEpoch；只有 WorkMain/BloggerMain 的有效成功清零失败计数。

### Blogger 压缩与连续追平

1. **Delta 分块**：依据 200 KiB 上限按语义 part 边界切分，cutoff 仅在完整 turn 推进。
2. **唯一 canonical-X Main 重建**：`XTraceMaterialization.currentProjection` 先从 durable current-generation XTrace 物化 canonical X；`BloggerMainContext` 再作为 normal catch-up、AABB/crash refresh、Squash 后 Main 与失败 retry 的唯一 next-main 公式，统一 Opening floor、cursor、chunk 与 digest。request-local provider presentation 永不参与 coverage proof。
3. **失败本地 Squash**：BloggerMain 失败并 advance 到 primed 槽时，若 durable frames 可 squash，则 recovery workflow 当场关闭失败 request、materialize Squash、绑定新 PromptKey 并发送；不依赖未来主 X transform。
4. **typed park event**：caught-up 不启动 timer。`AwaitMaterial` 只返回 `MaterialAvailable BloggerRequestContext | Cancelled`；offer-first material 被下一次 await 直接消费，parked offer 直接完成当前 waiter。durable open producer 即使恰处于 provider-step 间隙也只接收 staged offer；producer 已结算但 linked Blogger authority 仍 active 时，后续 material 经该 profile 的 `ManagedDelegationAssignment` continuation 进入。两条路径都不创建第二 Authority Root。wake 自带 material，不再返回 bool 后访问第二份 pending 状态。
5. **durable recovery event**：WorkMain recovery 只读取 `snapshotWithRevision`。若 linked Blogger 有 durable open request 且 coverage 尚未严格前进，则通过 `AgentJournal.awaitChangeFromOrCancel` 订阅下一 committed fact；commit/abandon/coverage fact 到达后重算，plugin shutdown 则注销订阅并结束等待。没有 open producer 立即 retry。process-local flight/pending 与 wall clock 不参与 correctness。
6. **连续 catch-up**：每次 cycle 提交后直接从当前 canonical coverage 与 XTrace Current 重新派生下一块；暂时无材料时等待 typed material/cancel event，不设置冻结上限或超时。
7. **Cold-horizon auxiliary retirement**：观察到外部 compaction 时，`ContextReanchored` 推进 epoch 并清空旧 auxiliary visibility；成功 prefix probe 的 `PrefixRebaseCommitted` 在同一个 projection fold 内完成 PrefixEpoch 提升与同样的 visibility retirement。probe 尚未提交时，XWire 以 `PrefixPresentationHorizon.TentativeCold` 作为当前 transform 的 typed 返回值，使 composition root 跳过后置 historical auxiliary projectors，避免旧 horizon 先把 probe 请求重新灌胖。
8. **Opening floor**：Manager Life 只以真实 Opening 后的 `WorkRecordStart` 作为压缩下界；T1 commitment 仍可属于 WorkRecord 的 constitutive Opening，但不再获得 provider-context 的 raw 常驻权。
9. **Stable-identity X 穿透**：X-wire 的 cutoff 是 canonical XTrace semantic-turn boundary，不是本次 provider 数组下标。写回时由 XTrace provenance 解析被 coverage 证明覆盖的 Host message id，明确排除 raw Opening，并在这些覆盖消息中保留 `todowrite` call/result 原始回合；request-local synthetic/presentation row 不在 covered id set 中，因此不会移动 cutoff 或被误删。
10. **Blogger materialization admission + terminal owner fence**：同一 Blogger 的 materialize / PromptKey bind / abandon 先取得 process-wide、跨 plugin instance 的 keyed admission；取得后再读 durable projection 并执行 open-request 转换。normal start 持有 admission 直到 durable materialize、原子 flight claim 与 send/bind 完成，provider retry 的 stage/bind/abandon 也复用同一 admission。flight claim 只允许空槽建立或同 RequestId 刷新；不同 RequestId 返回 conflict，不覆盖 owner。`BloggerRequestOwnership` 是 terminal→request ownership 的唯一纯 decision；Enforcer 与 reconciled-idle repair 只负责把 assistant `parentID` 解析为 exact `PhysicalUserMessageId`，再从 PromptAuthority accepted-dispatch evidence 与 durable open PromptKey/RequestId 组装 evidence 并调用该 decision。base attempt 或 request-scoped `InteractionRepair` 属于当前 request 才可继续；positive supersession 直接 no-op。不得用 latest-user 位置、文本内容或 process-local flight presence 猜 terminal owner。该 admission/flight 都是物理资源，不参与 recovery correctness proof。

## 依赖关系

DEPENDS ON:
- `semantic-trace`
- `provider-projection`

## Task 8 final-path closure

`scripts/checks/migration-ledger.json` 的 DONE 节点
`context-compression-blogger-compaction-keep` 是本包 Blogger/runtime/Host-compaction
闭包的唯一清单：其中恰有 25 个 final production paths，全部由
`context-compression` 单一拥有。Host-boundary 与 provider-attempt-recovery 只消费
compiler-observed contract；`HostCompactionGate`、`HostCompactionObserver`、
`CompactionPolicy`、`CompactionPolicySurface` 与 `TerminalValidity` 不存在共同 owner。
本次为 PROVEN-KEEP，没有删除、移动或重命名 production path，因此没有 deleted
alias、旧 namespace 或兼容 facade。

该节点绑定本包现存全部 24 个 `requirements/context-compression/tests/*.test.mjs`
proof files，并补充跨包的 crash、Host adapter 与 external-adapter proofs：
`requirements/crash-reconciliation/tests/blogger-crash-recovery.test.mjs`、
`requirements/host-boundary/tests/host-capability-observation.test.mjs`、
`requirements/effect-accounting/tests/external-adapter-boundary.test.mjs`。因此下表的
24 条 WHAT 语义与 crash reconciliation、typed park、Host compaction 和 terminal
validity 证明共同落在同一个 closure 上。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CONTEXT-COMPRESSION-001 | `requirements/context-compression/tests/ctx-capacity-observation-forbidden.test.mjs::WHAT[CONTEXT-COMPRESSION-001] CTX_001_forbidden_capacity_synonyms_never_appear_in_production_source` |
| CONTEXT-COMPRESSION-002 | `requirements/context-compression/tests/recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_only_a_failure_advance_arms_the_next_slot` |
| CONTEXT-COMPRESSION-003 | `requirements/context-compression/tests/blogger-delta.test.mjs::WHAT[CONTEXT-COMPRESSION-003] CTX_003_no_chunk_exceeds_the_limit` |
| CONTEXT-COMPRESSION-004 | `requirements/context-compression/tests/terminal-validity.test.mjs::WHAT[CONTEXT-COMPRESSION-004] CTX_004_empty_terminal_is_not_a_result` |
| CONTEXT-COMPRESSION-005 | `requirements/context-compression/tests/recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-005] CTX_005_Failed_and_Aborted_take_the_identical_path` |
| CONTEXT-COMPRESSION-006 | `requirements/context-compression/tests/recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-006] CTX_006_recovery_needs_arming_a_primed_offset_and_material` |
| CONTEXT-COMPRESSION-007 | `requirements/context-compression/tests/recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-007] CTX_007_a_failed_squash_fails_the_slot_without_sending_the_main_request` |
| CONTEXT-COMPRESSION-008 | `requirements/context-compression/tests/recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-008] CTX_010_only_the_work_main_request_may_carry_a_prefix_probe` |
| CONTEXT-COMPRESSION-009 | `requirements/context-compression/tests/probe-selection.test.mjs::WHAT[CONTEXT-COMPRESSION-009] CTX_010_the_probe_records_the_epoch_it_was_built_from` |
| CONTEXT-COMPRESSION-010 | `requirements/context-compression/tests/probe-selection.test.mjs::WHAT[CONTEXT-COMPRESSION-010] CTX_011_no_completed_turn_yet_means_no_candidate` |
| CONTEXT-COMPRESSION-011 | `requirements/context-compression/tests/blog-projection.test.mjs::WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_replaces_the_oldest_frames_and_leaves_the_covered_range_alone` |
| CONTEXT-COMPRESSION-012 | `requirements/context-compression/tests/blogger-delta.test.mjs::WHAT[CONTEXT-COMPRESSION-012] CTX_013_a_small_transcript_becomes_one_chunk` |
| CONTEXT-COMPRESSION-013 | `requirements/context-compression/tests/ctx014.test.mjs::WHAT[CONTEXT-COMPRESSION-013] CTX_014_diagnostic_emit_is_structured_and_redacted` |
| CONTEXT-COMPRESSION-014 | `requirements/context-compression/tests/blog-projection.test.mjs::WHAT[CONTEXT-COMPRESSION-014] COMPANION_006_squash_rewrites_first_half_of_frames_permanently` |
| CONTEXT-COMPRESSION-015 | `requirements/context-compression/tests/blog-projection.test.mjs::WHAT[CONTEXT-COMPRESSION-015] COMPANION_008_entry_appends_frame_and_advances_coverage_together` |
| CONTEXT-COMPRESSION-016 | `requirements/context-compression/tests/probe-selection.test.mjs::WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_candidate_never_swallows_the_message_being_answered` |
| CONTEXT-COMPRESSION-017 | `requirements/context-compression/tests/ctx-opening-floor.test.mjs::WHAT[CONTEXT-COMPRESSION-017] CTX_016_t1_does_not_change_the_compression_floor` |
| CONTEXT-COMPRESSION-018 | `requirements/context-compression/tests/companion-ordinary-material-surface.test.mjs::WHAT[CONTEXT-COMPRESSION-018] CompanionTransform owns ordinary-material entry and consumes Host suppression as a capability` |
| CONTEXT-COMPRESSION-019 | `requirements/context-compression/tests/injected-context-reanchor.test.mjs::WHAT[CONTEXT-COMPRESSION-019] CTX_019_prefix_rebase_is_the_same_auxiliary_cold_boundary_as_host_reanchor` |
| CONTEXT-COMPRESSION-020 | `requirements/context-compression/tests/ctx-opening-floor.test.mjs::WHAT[CONTEXT-COMPRESSION-020] todowrite call and matching result are retained across a Y cutoff` |
| CONTEXT-COMPRESSION-021 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-021] CTX_021_primed_blogger_main_with_frames_dispatches_squash_first` |
| CONTEXT-COMPRESSION-022 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-022] CTX_022_all_production_main_rebuilds_share_BloggerMainContext` |
| CONTEXT-COMPRESSION-023 | `requirements/context-compression/tests/parked-transform.test.mjs::WHAT[CONTEXT-COMPRESSION-023] CTX_023_park_has_no_clock_or_timeout_dependency`；`requirements/context-compression/tests/companion-recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-023] recovery_wait_has_no_clock_or_process_local_correctness_state` |
| CONTEXT-COMPRESSION-024 | `requirements/context-compression/tests/parked-transform.test.mjs::WHAT[CONTEXT-COMPRESSION-024] CTX_024_materialization_admission_is_cross_instance_single_flight`；`requirements/context-compression/tests/companion-recovery-slot.test.mjs::WHAT[CONTEXT-COMPRESSION-024] CTX_024_all_materialization_owners_share_admission_and_nonoverwrite_flight`；`requirements/context-compression/tests/enforcer-cycle-convergence.test.mjs::WHAT[CONTEXT-COMPRESSION-024] stale_terminal_cannot_reclaim_a_new_Blogger_request` |

## GAP

- CONTEXT-COMPRESSION-017/020：`ctx-opening-floor.test.mjs` 证明 pre/post-T1 floor 等价与 todo round retention 纯判定；`provider-projection/tests/projection.test.mjs` 证明真实 Y prefix write-back 越过 `todowrite` 时 call/result 仍以原始 X Host 消息存在。CLOSED。
- CONTEXT-COMPRESSION-021/022：failure-local Y recovery 与唯一 `BloggerMainContext` 已进入 production graph；旧 recovery waiter / future-X squash 入口已删除。CLOSED。
- CONTEXT-COMPRESSION-023：park 与 recovery wait 均只由 typed/durable event 推动；correctness path 无 timer/deadline/timeout。CLOSED。
- CONTEXT-COMPRESSION-024：同一 Blogger 的 materialize / bind / abandon 已由跨 plugin instance admission 串行；normal start 在 admission 内重检 `HasFlight`，retry 对 foreign flight fail-before-write，crash recovery 取得 admission 后重读 durable open；flight claim / release 均 RequestId-aware，拒绝跨 owner 覆盖或删除。terminal→request owner fence 由 `BloggerRequestOwnership` 统一判定，并以 assistant `parentID → PhysicalUserMessageId → PromptAuthority accepted dispatch → durable open RequestId/PromptKey` 为证据；旧 terminal 已被新 RequestId 取代时为 `Superseded`，Enforcer recovery 与 reconciled-idle repair 均 no-op，不消费或改写新 owner。验证：Fable build 与 `scripts/check.mjs` 全绿；context-compression 242/242、behavior-diagnosis 147/147、dispatch/interaction/crash 228/228；权威 verification suite 3523/3523。CLOSED。
