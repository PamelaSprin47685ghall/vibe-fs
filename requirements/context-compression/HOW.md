# context-compression — HOW

## 架构与核心机制

### 重试策略与候选机制

1. **ProviderFailure 预算先裁决**：真实失败先经 `ProviderFailureLedger` 记账并推进 `ProviderFailureBudget` 连续失败计数；`verdict` 为 `Exhausted` 时不再派发下一物理请求。预算是纯值 (`initial` / `recordFailure` / `recordSuccess` / `verdict`)，durable 侧由 `ProviderFailureProjection` (newest-covers + `AlreadyObserved` 去重 + `applyExhausted`) 承载，单一 `DefaultBudget` (12) 全局生效。
2. **BloggerRetryPolicy 按材料分型**：`nextRequest failedKind hasSquashMaterial` 是纯函数，不设第二个开关：失败的 `BloggerMain` 有 squash 材料则选 `BloggerSquash`，无材料则重发 `BloggerMain`；失败的 `BloggerSquash` 永远回到 `BloggerMain`；非 Blogger 运行分别返回 `MissingProjection` / `NoActiveBloggerRun`。X 的 `PrefixProbeSelection` 返回候选或精确 `NoCandidateReason`，Y 直接从 typed request + durable frames 判定 squash 材料。
3. **单次 attempt 内决策**：材料决策只属于当前物理 attempt。NoCoverage / stale candidate 发送普通主请求，不跨 attempt 携带，不等待未来 X material。
4. **分派与提交**：按 RequestKind 分派后继；Probe 成功原子提升 ActivePrefixEpoch，Squash 成功则压缩前半段 frames 并递增 FrameEpoch；只有 WorkMain/BloggerMain 的有效成功清零失败计数。
5. **成功后的 retry transport row 不再授权 probe**：`settleVisibleToolContinuations` 先完成 prefix 提交、成功记账及 plan 消费。随后 `XWire.mayProbe` 读取已提交的连续失败计数；零失败只使用 committed prefix，不再物化 canonical X、读取候选 frames 或写入候选 blob。已有 frozen plan 仍原样复用；只有后续真实失败重新记账后才能选择新候选。`attempt-plan-probe-eligibility` 的回归通过 production budget 与 XWire decision Surface 重放 failure → tool success → coverage growth → new failure，证明成功后输出不变、再次失败恢复候选资格。

该回归来自保留世界 `/tmp/oc-e2e-CEqj6W` 的 `seal-undeclared`：已提交 blob `f6067d123fb8…` 经 production memoryBlock 渲染的 wire digest 为 `28e287c40f4e`，新增 frame 的 blob `eb76afd20daa…` 渲染为 `a9cc2a3038ea`，分别精确匹配失败前后的 prefix。提交代码原样提升 candidate；缺陷是成功消费 plan 后，以仍存在的 failure projection 和 retry row 重新选出候选，而不是中断消息插入或提交时重算。旧实现确定性回归返回 cutoff 2 的新 probe（预期 `null`），修复后保持 committed output；此纯决策证明不替代真实 Host 的物理验收。

### Blogger 压缩与连续追平

1. **Delta 分块**：依据 200 KiB 上限按语义 part 边界切分，cutoff 仅在完整 turn 推进。
2. **唯一 canonical-X Main 重建**：`XTraceMaterialization.currentProjection` 先从 durable current-generation XTrace 物化 canonical X；`BloggerMainContext` 再作为 normal catch-up、crash refresh、Squash 后 Main 与失败 retry 的唯一 next-main 公式，统一 Opening floor、ingest 位置、chunk 与 digest。request-local provider presentation 永不参与 coverage proof。
3. **失败本地 Squash**：`BloggerMain` 的已确认失败在重试时，若 `BloggerRetryPolicy` 选中 Squash（存在可 squash frames），则 retry workflow 当场关闭失败 request、materialize Squash、绑定新 PromptKey 并发送；不依赖未来主 X transform。
4. **typed park event**：caught-up 不启动 timer。`AwaitMaterial` 只返回 `MaterialAvailable BloggerRequestContext | Cancelled`；offer-first material 被下一次 await 直接消费，parked offer 直接完成当前 waiter。durable open producer 即使恰处于 provider-step 间隙也只接收 staged offer；producer 已结算但 linked Blogger authority 仍 active 时，后续 material 经该 profile 的 `ManagedDelegationAssignment` continuation 进入。两条路径都不创建第二 Authority Root。wake 自带 material，不再返回 bool 后访问第二份 pending 状态。
5. **durable recovery event**：WorkMain recovery 只读取 `snapshotWithRevision`。若 linked Blogger 有 durable open request 且 coverage 尚未严格前进，则通过 `AgentJournal.awaitChangeFromOrCancel` 订阅下一 committed fact；commit/abandon/coverage fact 到达后重算，plugin shutdown 则注销订阅并结束等待。没有 open producer 立即 retry。process-local flight/pending 与 wall clock 不参与 correctness。
6. **连续 catch-up**：每次 cycle 提交后直接从当前 canonical coverage 与 XTrace Current 重新派生下一块；暂时无材料时等待 typed material/cancel event，不设置冻结上限或超时。
7. **Cold-horizon auxiliary retirement**：观察到外部 compaction 时，`ContextReanchored` 推进 epoch 并清空旧 auxiliary visibility；成功 prefix probe 的 `PrefixRebaseCommitted` 在同一个 projection fold 内完成 PrefixEpoch 提升与同样的 visibility retirement。probe 尚未提交时，XWire 以 `PrefixPresentationHorizon.TentativeCold` 作为当前 transform 的 typed 返回值，使 composition root 跳过后置 historical auxiliary projectors，避免旧 horizon 先把 probe 请求重新灌胖。
8. **Opening floor**：Manager Life 只以真实 Opening 后的 `WorkRecordStart` 作为压缩下界；T1 commitment 仍可属于 WorkRecord 的 constitutive Opening，但不再获得 provider-context 的 raw 常驻权。
9. **Stable-identity X 穿透**：X-wire 的 cutoff 是 canonical XTrace semantic-turn boundary，不是本次 provider 数组下标。写回时由 XTrace provenance 解析被 coverage 证明覆盖的 Host message id，明确排除 raw Opening，并在这些覆盖消息中保留 `todowrite` call/result 原始回合；request-local synthetic/presentation row 不在 covered id set 中，因此不会移动 cutoff 或被误删。
10. **Blogger materialization admission + terminal owner fence**：同一 Blogger 的 materialize / PromptKey bind / abandon 先取得 process-wide、跨 plugin instance 的 keyed admission；取得后再读 durable projection 并执行 open-request 转换。normal start 持有 admission 直到 durable materialize、原子 flight claim 与 send/bind 完成，provider retry 的 stage/bind/abandon 也复用同一 admission。flight claim 只允许无 owner 时建立或同 RequestId 刷新；不同 RequestId 返回 conflict，不覆盖 owner。`BloggerRequestOwnership` 是 terminal→request ownership 的唯一纯 decision；Enforcer 与 reconciled-idle repair 只负责把 assistant `parentID` 解析为 exact `PhysicalUserMessageId`，再从 PromptAuthority accepted-dispatch evidence 与 durable open PromptKey/RequestId 组装 evidence 并调用该 decision。base attempt 或 request-scoped `InteractionRepair` 属于当前 request 才可继续；positive supersession 直接 no-op。不得用 latest-user 位置、文本内容或 process-local flight presence 猜 terminal owner。该 admission/flight 都是物理资源，不参与 retry correctness proof。

`ContextFactFold` 在同一次 fact fold 内直接调用纯 `EnforcementProjection.applyFromEntry`／`applySquash`，使用具名 `EnforcementCycleRecord` 保留类型检查；不存在动态模块查找、手写 union tag 或模块缺失时的默认状态。该依赖指向 `enforcer-projection` 的纯投影分片，不指向 Enforcer runtime。Blogger Coordinator 所需 Nudge 工作流由独立 `dispatch/session-nudge` 分片提供，repair decision 与 Blogger evidence reader 由 `enforcer/repair` 提供，避免经 ingress 或 enforcer-codec 大分片形成编译环。

2026-09-12：`ContextFactFold` 交出聚合写入后，它对 `EnforcementProjection` 的调用形态没变（仍是纯投影 `applyFromEntry`／`applySquash`，值由 `Composition/Durable/DomainFamilyBridge.ContextProjectionBridge` 注入的窄查询提供），但 fold 不再持有 `AgentProjectionSet`：它只认识 `BloggerCycleProjectionState`／`EnforcementProjectionState`／`BlogProjectionState`／`ActivePrefixEpoch` 四个切片，按 `ContextProjectionChange` 列表表达六种事实要写的切片，拒绝文本与 fact 名放进闭合 `ContextFoldRejection`。前缀观测的吸收判定改由 `PrefixEpochProjection.describe` 提供，与 `ProjectionUpdate.prefixOutcome` 共用一份策略。

`XWire.materializeFrozenRecordPrefix` 在读取并校验 coverable frame blobs 后，直接调用纯 `LifecycleWorkRecord.materialize opening frameBodies "" false`。同 session 不重复渲染 Opening，也不纳入 live RawGap；删除动态模块查找及手写 Chronicle fallback，frame 读取失败仍沿原 taskResult 传播。`lifecycle-work-record.test.mjs` 证明 canonical renderer，`prefix-stability/tests/prefix-writeback.test.mjs` 证明真实写回保留 raw Opening 对象与顺序；原 `ctx-opening-floor` 中只匹配源码／注释的 same-session 用例已删除。这些分别成立的证明尚不覆盖 journal → coverable frames → frozen blob 的完整物化路径，不宣称该集成缺口关闭。

## 依赖关系

DEPENDS ON:
- `semantic-trace`
- `provider-projection`

## Task 8 final-path closure

`scripts/checks/release-closure-nodes.json` 的 DONE 节点
`context-compression-blogger-compaction-keep` 是本包 Blogger/runtime/Host-compaction
闭包的唯一清单：其中恰有 25 个 final production paths，全部由
`context-compression` 单一拥有。Host-boundary 与 provider-attempt-recovery 只消费
compiler-observed contract；`HostCompactionGate`、`HostCompactionObserver`、
`CompactionPolicy`、`CompactionPolicySurface` 与 `TerminalValidity` 不存在共同 owner。
本次为 PROVEN-KEEP，没有删除、移动或重命名 production path，因此没有 deleted
alias、旧 namespace 或兼容 facade。

该节点绑定本包现存全部 24 个 `requirements/context-compression/tests/*.test.mjs`
proof files，并补充跨包的 repair、Host adapter 与 external-adapter proofs：
`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs`、
`requirements/host-boundary/tests/host-capability-observation.test.mjs`、
`requirements/effect-accounting/tests/external-adapter-boundary.test.mjs`。因此下表的
24 条 WHAT 语义与 crash reconciliation、typed park、Host compaction 和 terminal
validity 证明共同落在同一个 closure 上。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CONTEXT-COMPRESSION-001 | `requirements/context-compression/tests/ctx-capacity-observation-forbidden.test.mjs::WHAT[CONTEXT-COMPRESSION-001] CTX_001_context_compression_owner_never_observes_forbidden_capacity_synonyms` |
| CONTEXT-COMPRESSION-002 | `requirements/context-compression/tests/retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-002] retry dispatch reacts only to confirmed failure material`；`requirements/context-compression/tests/attempt-plan-probe-eligibility.test.mjs::WHAT[CONTEXT-COMPRESSION-002] successful retry tool steps keep the committed prefix despite new coverage` |
| CONTEXT-COMPRESSION-003 | `requirements/context-compression/tests/blogger-delta.test.mjs::WHAT[CONTEXT-COMPRESSION-003] CTX_003_no_chunk_exceeds_the_limit` |
| CONTEXT-COMPRESSION-004 | `requirements/context-compression/tests/terminal-validity.test.mjs::WHAT[CONTEXT-COMPRESSION-004] CTX_004_empty_terminal_is_not_a_result` |
| CONTEXT-COMPRESSION-005 | `requirements/context-compression/tests/retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-005] every recorded failure consumes exactly one budget unit` |
| CONTEXT-COMPRESSION-006 | `requirements/context-compression/tests/retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-006] consecutive failures consume the failure budget and exhaustion halts auto-retry`；`requirements/context-compression/tests/retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-006] prefix probe is selected when policy allows and candidate exists` |
| CONTEXT-COMPRESSION-007 | `requirements/context-compression/tests/companion-retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-007] same_failed_kind_with_same_material_always_selects_the_same_next_request` |
| CONTEXT-COMPRESSION-008 | `requirements/context-compression/tests/retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-008] only work_main requests may carry prefix probe` |
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
| CONTEXT-COMPRESSION-021 | `requirements/context-compression/tests/companion-retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-021] CTX_021_failed_blogger_main_with_material_dispatches_squash` |
| CONTEXT-COMPRESSION-022 | `requirements/context-compression/tests/companion-retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-022] CTX_022_durable_projection_keeps_newest_failure_count`；`requirements/context-compression/tests/companion-retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-022] CTX_022_retry_sequence_returns_to_main_through_one_formula` |
| CONTEXT-COMPRESSION-023 | `requirements/context-compression/tests/parked-transform.test.mjs::WHAT[CONTEXT-COMPRESSION-023] CTX_023_park_has_no_clock_or_timeout_dependency` |
| CONTEXT-COMPRESSION-024 | `requirements/context-compression/tests/parked-transform.test.mjs::WHAT[CONTEXT-COMPRESSION-024] CTX_024_materialization_admission_is_cross_instance_single_flight`；`requirements/context-compression/tests/companion-retry-policy.test.mjs::WHAT[CONTEXT-COMPRESSION-024] CTX_024_request_scoped_repair_continues_only_for_the_current_request`；`requirements/context-compression/tests/enforcer-cycle-convergence.test.mjs::WHAT[CONTEXT-COMPRESSION-024] stale_terminal_cannot_reclaim_a_new_Blogger_request` |
| CONTEXT-COMPRESSION-025 | `requirements/context-compression/tests/m6-fatal-boundary.test.mjs::WHAT[CONTEXT-COMPRESSION-025] Blogger fatal binds exact request settlement and one injected fuse` |

## GAP

- CONTEXT-COMPRESSION-017/020：`ctx-opening-floor.test.mjs` 证明 pre/post-T1 floor 等价与 todo round retention 纯判定；`provider-projection/tests/projection.test.mjs` 证明真实 Y prefix write-back 越过 `todowrite` 时 call/result 仍以原始 X Host 消息存在。CLOSED。
- CONTEXT-COMPRESSION-021/022：失败本地 Y retry 与唯一 `BloggerMainContext` 已进入 production graph；`BloggerRetryPolicy` 纯材料分派与 `ProviderFailureProjection` newest-covers 去重已取代旧等待未来 X 的入口。CLOSED。
- CONTEXT-COMPRESSION-023：park 与 recovery wait 均只由 typed/durable event 推动；correctness path 无 timer/deadline/timeout。CLOSED。
- CONTEXT-COMPRESSION-024：同一 Blogger 的 materialize / bind / abandon 已由跨 plugin instance admission 串行；normal start 在 admission 内重检 exact shared flight ownership，retry 对 foreign flight fail-before-write；进程重启不重建本地 repair episode。flight claim / release 均 RequestId-aware，拒绝跨 owner 覆盖或删除。terminal→request owner fence 由 `BloggerRequestOwnership` 统一判定，并以 assistant `parentID → PhysicalUserMessageId → PromptAuthority accepted dispatch → durable open RequestId/PromptKey` 为证据；旧 terminal 已被新 RequestId 取代时为 `Superseded`，Enforcer continuation 与 reconciled-idle repair 均 no-op，不消费或改写新 owner。CLOSED。
