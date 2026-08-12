# Enforcer / Blogger — 证明

行为见 `what/enforcer.md`，所有权见 `shape/enforcer.md`，程序见 `how/enforcer.md`。  
规则实例实现面：`resources/enforcer/<TipName>/{enforcer.md,main.md}`（目录名 = TipName；**无** `catalog.json`）。  
本文件只列证明项与真实证据路径；不重新定义任何 `ENFORCER-*` Clause。

> 提示：多调用 tip 选择按 **provider-visible `PartOrdinal` 最早**（`EnforcerCycle.fs` 的
> `mergeCalls`）证明，不是按 lexical ordinal。`docs/how/enforcer.md` ENFORCER-025 为
> PartOrdinal-first-only；`docs/shape/enforcer.md` 为物理所有权（`HasFlight` /
> `HasParked` / `PendingOffer` / `DrainWindow`）+ TipDelivery / Observation 投影边界。
> 此前的 baseline gap（catalog-ordinal tip 选择、纯 cell 转移、catalog.json SSOT）均已关闭。

## 资源与启动（folder SSOT / ENFORCER-170）

| 证明 | 证据路径 |
|------|----------|
| 打包 rulebook 目录可加载；缺失 / 非法目录 / Domain 校验失败 → 启动 fail fast，无代码内 fallback | `tests/unit/enforcer/catalog-validation.test.mjs`；`tests/unit/enforcer/catalog.test.mjs`；`tests/integration/resources/enforcer-rulebook.test.mjs`；资源 `resources/enforcer/*/` |
| 恰好 N 条规则（当前 120）；TipName/`field`/`id` 唯一且三者相等；lexical ordinal 连续 `1..N` | `tests/unit/enforcer/catalog.test.mjs`（`ENFORCER_170_*`）；`tests/unit/enforcer/catalog-validation.test.mjs`（`ENFORCER_170_validate_*`） |
| 每 tip 目录含非空 `enforcer.md` + `main.md`（EnforcerText/MainText）；无 catalog.json 作为装载输入 | `tests/unit/enforcer/catalog.test.mjs`；`src/Wanxiangshu/Infrastructure/Resources/EnforcerCatalogResource.fs` |
| `package.json` files 含 `resources/` → pack 后仍可加载 | integration 资源 / package 套件覆盖加载失败路径 |

## 领域与 codec / tip（ENFORCER-020..024）

| 证明 | 证据路径 |
|------|----------|
| missing / empty tip 失败 | `tests/unit/enforcer/codec.test.mjs`（`ENFORCER_023_missing_tip_fails`、`ENFORCER_023_empty_tip_fails`） |
| unknown tip 失败；无 fuzzy / 拼写修复 | `tests/unit/enforcer/codec.test.mjs`（`ENFORCER_023_unknown_tip_fails`、`ENFORCER_024_fuzzy_or_misspelled_tip_is_not_mapped`） |
| exact field → exact RuleId；trim 后查找；RuleId = FieldName = TipName | `tests/unit/enforcer/codec.test.mjs`（`ENFORCER_021_*`） |
| text / evidence 保留为独立字段；不是 tip | `tests/unit/enforcer/codec.test.mjs`（`ENFORCER_022_*`、`ENFORCER_020_*`） |
| 额外 numeric property 不复活 score path | `tests/unit/enforcer/codec.test.mjs`（`ENFORCER_024_extra_numeric_properties_are_ignored`） |
| 无 score 路径：decode 面无 `Scores` / `parseScore` | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_03_04_facade_surface_has_tip_not_numeric_scores`） |
| field 枚举与 tool enum 一致（runtime = package directory scan） | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_01`、`ENFORCER_TIP_02_and_16`） |

## Cycle 与多调用归并（ENFORCER-025/042/043/061）

| 证明 | 证据路径 |
|------|----------|
| text 按 `PartOrdinal` 稳定合并（`"\n\n"`） | `tests/unit/enforcer/cycle-nudge.test.mjs`（`ENFORCER_042_text_merges_in_part_ordinal_order`）；`src/Wanxiangshu/Domain/EnforcerCycle.fs`（`mergeCalls`） |
| canonical tip = **PartOrdinal 最早**，不合并 / 不 max / 不按 lexical ordinal | `tests/unit/enforcer/cycle-nudge.test.mjs`（`ENFORCER_025_canonical_tip_is_first_by_part_ordinal`、`ENFORCER_025_multi_call_does_not_merge_or_max_tips`）；`tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_15_multi_call_canonical_tip_is_first_by_part_ordinal`） |
| evidence 完全相同去重、`"; "` 拼接 | `tests/unit/enforcer/cycle-nudge.test.mjs`（`ENFORCER_042_evidence_dedupes_exact_duplicates`） |
| multi-call 仍提交单 cycle 并标记 protocol violation（`MultiCall=true`） | `tests/unit/enforcer/cycle-nudge.test.mjs`（`ENFORCER_042_single_call_is_not_multi_call`）；`tests/unit/enforcer/identity-fail-closed.test.mjs`（`ENFORCER_042_multi_call_commits_single_cycle_with_protocol_violation`）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`validateCycle`）→ `Diagnostic.emit "enforcer-protocol-violation"`（静默，HOST-007；字段白名单见 `tests/unit/context/ctx014.test.mjs` `CTX_014_enforcer_protocol_violation_fields_are_whitelisted`）；`EnforcerCycle.fs` |
| 重复 ToolCallId / identity 不成立 → fail closed（`ENFORCER-043`） | `tests/unit/enforcer/identity-fail-closed.test.mjs`（`ENFORCER_043_duplicate_tool_call_ids_fails_closed`）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`validateCycle`）→ `Diagnostic.fatal "enforcer-cycle-failed"` |
| 空 / 缺失 messageId（无 provable provider run）→ fail closed（`ENFORCER-043`） | `tests/unit/enforcer/identity-fail-closed.test.mjs`（`ENFORCER_043_no_provable_provider_run_fails_closed`）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`validateCycle`）→ `Diagnostic.fatal "enforcer-cycle-failed"` |
| 有效 cycle 要求非空 text | `tests/unit/enforcer/cycle-nudge.test.mjs`（`ENFORCER_043_valid_cycle_requires_nonempty_text`）；`EnforcerCycle.fs` `isValidCycle` |
| 空 text tool gate 拒绝 | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_061_blog_tool_rejects_empty_canonical_text`） |
| 合并 tool call 数 >32 → fail closed（`MaxMergedToolCalls=32`） | `tests/unit/enforcer/bounds.test.mjs`（`ENFORCER_042_more_than_32_merged_tool_calls_fails_closed`）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`validateCycle`，`MaxMergedToolCalls=32`，strict `>`）→ `Diagnostic.fatal "enforcer-cycle-failed"` |
| 合并 text >512 KiB UTF-8 → fail closed（`MaxBlogTextBytes`） | `tests/unit/enforcer/bounds.test.mjs`（`ENFORCER_042_merged_text_over_512KiB_fails_closed`）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`validateCycle`，`MaxBlogTextBytes=512*1024`，strict `>`，UTF-8 `SyntheticToml.byteCount`）→ `Diagnostic.fatal "enforcer-cycle-failed"` |
| 合并 evidence >128 KiB UTF-8 → fail closed（`MaxEvidenceBytes`） | `tests/unit/enforcer/bounds.test.mjs`（`ENFORCER_042_merged_evidence_over_128KiB_fails_closed`）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`validateCycle`，`MaxEvidenceBytes=128*1024`，strict `>`，UTF-8 `SyntheticToml.byteCount`）→ `Diagnostic.fatal "enforcer-cycle-failed"` |

## 原子提交 / 恢复（ENFORCER-045/152/153/154）

| 证明 | 证据路径 |
|------|----------|
| 一个 normal cycle 恰好追加一个 frame 并推进 RecordCoverage | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage`、`ENFORCER_host_completed_blog_second_window_advances_coverage_not_resend`） |
| duplicate ProviderRun 不产生第二条 | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_host_completed_blog_second_pass_same_run_is_idempotent`）；`tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_09_replay_preserves_tip`） |
| unowned 完成的 blog 不发明 `BlogEntryCommitted` | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_host_completed_blog_without_live_request_is_noop_not_commit`） |
| crash window 从 Materialized context + Host snapshot reconcile；读 `HasFlight` 非 cell State | `tests/unit/enforcer/blogger-crash-recovery.test.mjs`（`C5_crash_recovery_*`、`C5_crash_recovery_reads_HasFlight_not_cell_State`） |
| CommitUnknown 不盲重试模型（delta digest 不匹配 fail closed） | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_delta_digest_mismatch_is_fatal`） |
| 不存在 `EnforcementCycleCommitted` 独立事实 | `tests/unit/enforcer/blogger-convergence-gaps.test.mjs`（`C0_no_EnforcementCycleCommitted_fact`） |

## Observation / RecentTips / 配对（ENFORCER-070/071 Y 侧）

| 证明 | 证据路径 |
|------|----------|
| 每次已提交 cycle 记录恰好一个 tip | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_08_each_committed_cycle_records_exactly_one_tip`） |
| 上限 8（`RecentTipLimit = 8`） | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_10_recent_tips_cap_at_8`）；`src/Wanxiangshu/Journal/EnforcementProjection.fs` |
| oldest → newest 顺序 | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_11_recent_tips_order_oldest_to_newest`） |
| squash **co-truncate** RecentTips（Observation tip co-move；非「清空」） | `tests/unit/enforcer/tip-v2-contract.test.mjs`（squash/observation co-move 用例；`EnforcementProjection.applySquash`）；`src/Wanxiangshu/Journal/Fold.fs`（`BlogSquashCommitted` 同 session 应用 tips） |
| Companion 重建为 **配对** tip+frame units，不是 tips∥frames 平行流 | `tests/unit/enforcer/tip-v2-contract.test.mjs`（配对 observation 断言）；`src/Wanxiangshu/Domain/CompanionProjectionBuilder.fs`（`pairTipFrameUnits`）；`src/Wanxiangshu/Domain/RulebookObservation.fs`（`pairTipsAndFrames`） |
| `previous_enforcer_tip` 为 `[[do_not_exec]]` 低信任历史，role=assistant | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_13_work_record_contains_previous_enforcer_tip_blocks`） |
| prompt 反重复 + 严重例外，不复活 score 措辞 | `tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_14_prompt_has_anti_repeat_and_severe_exception`） |

## Main Full / Identity（ENFORCER-071 X 侧 / TipGuidanceDelivered）

| 证明 | 证据路径 |
|------|----------|
| 首次 resolve → `TipPresentation.Full`，正文含 main.md / tip header | `tests/unit/enforcer/tip-guidance-delivery.test.mjs`（Full 首次用例）；`src/Wanxiangshu/Session/EnforcerHost.fs`（`resolveTipGuidance`） |
| 同 tip 再次 resolve → `IdentityOnly`（`tip: <name>`），不重复 Full 正文 | `tests/unit/enforcer/tip-guidance-delivery.test.mjs`（Identity 重复用例） |
| Full 路径 append `TipGuidanceDelivered`；fold 进 `TipDeliveryProjection.FullDeliveredTips` | `tests/unit/enforcer/tip-guidance-delivery.test.mjs`；`src/Wanxiangshu/Journal/TipDeliveryProjection.fs`；`src/Wanxiangshu/Journal/Fold.fs`（`TipGuidanceDelivered`） |
| IdentityOnly 不扩展 Full 集合（audit-only） | `TipDeliveryProjection.apply`（`IdentityOnly -> state`）；对应 tip-guidance 测试 |
| Blogger satellite session id 经 association 解析到 owner Main tip | `tests/unit/enforcer/tip-guidance-delivery.test.mjs`（blogger id resolve） |
| 无 tip / 无 association → None，不发明 guidance | `tests/unit/enforcer/tip-guidance-delivery.test.mjs` |
| `ContextReanchored` 清空 FullDeliveredTips；其后 resolve 再发 Full main.md | `tests/unit/enforcer/tip-guidance-delivery.test.mjs`（reanchor 后再 Full）；`TipDeliveryProjection.applyReanchor`；`docs/what/persist.md` ContextReanchored 行 |
| `latestTipGuidance` / `latestTipNudge` 同义（Full/Identity 文本，非旧 Nudge 字段） | `tests/unit/enforcer/tip-guidance-delivery.test.mjs`（`ENFORCER_TIP_DELIVERY_003_*`） |

## 运行时所有权（ENFORCER-047/050）

| 证明 | 证据路径 |
|------|----------|
| `HasFlight` 是唯一 busy 定义；busy skip 不推进 coverage、不排队 | `tests/unit/enforcer/blogger-runtime.test.mjs`（`ENFORCER_047_inflight_plus_material_skips_without_queue`）；`tests/unit/enforcer/blogger-convergence-gaps.test.mjs`（`C0_physical_HasFlight_is_the_only_busy_definition`） |
| idle + material → Start；idle + parked → Offer | `tests/unit/enforcer/blogger-runtime.test.mjs`（`ENFORCER_047_idle_plus_material_starts`、`ENFORCER_047_idle_plus_parked_waiter_offers`）；`tests/unit/enforcer/parked-transform.test.mjs` |
| `CurrentRequest` 与 `PendingOffer` 是独立槽 | `tests/unit/enforcer/blogger-convergence-gaps.test.mjs`（`C0_CurrentRequest_and_PendingOffer_are_separate_slots`）；`tests/unit/enforcer/parked-transform.test.mjs`（`ENFORCER_050_*`） |
| 生命周期权威 = 物理所有权，非 cell | `tests/unit/enforcer/blogger-convergence-gaps.test.mjs`（`C0_blogger_lifecycle_authority_is_physical_ownership`）；`tests/unit/enforcer/blogger-seal-reactivate.test.mjs`（`BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth`） |
| 无 `BloggerRuntimeState` / `BloggerRuntimeCell` 生产引用；seal / teardown 经物理 registry + drain | `tests/unit/enforcer/blogger-seal-reactivate.test.mjs`（`HANDLE_lifecycle_*`、`BLOGGER_RUNTIME_*`）；`tests/unit/enforcer/blogger-runtime.test.mjs`（`ENFORCER_047_session_delete_is_registry_removal_not_a_cell_state`）；`src/Wanxiangshu/Session/BloggerRuntimeState.fs`（仅 `DrainWindow` 等物理定义，无 State DU） |

## Repair / fallback（ENFORCER-060..068）

| 证明 | 证据路径 |
|------|----------|
| pure prose / 空 text 只一次 InteractionRepair opportunity；相同 terminal 重放不重复 nudge | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_060_pure_prose_first_issues_interaction_nudge_not_aabb`、`ENFORCER_060_pure_prose_same_terminal_reentry_does_not_aabb`、`ENFORCER_060_already_claimed_pure_prose_is_nudge_not_aabb_no_second_send`） |
| 新无效 terminal 才证明 repair 失败 → AABB / fatal | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_060_pure_prose_second_terminal_triggers_aabb`、`ENFORCER_061_second_empty_text_exhausts_repair_and_fatals`） |
| tool failure / repair 耗尽进入统一 Fallback，主 cursor 经单一 writer 推进 | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`ENFORCER_068_aabb_repair_advances_primary_cursor_through_one_writer`、`ENFORCER_068_aabb_repair_path_advances_primary_cursor_once`、`ENFORCER_065_tool_execution_error_blog_advances_primary_cursor_once`） |
| abort 清理残留只注入 repair，不推进主 cursor / 不消耗预算 | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（`LOOP_006_interrupted_blog_repairs_without_advancing_primary_cursor`）；`tests/e2e/cases/fallback-aabb-trace.test.mjs`（`FallbackCursorAdvanced` 恰好 4 次） |
| recovery marker 不退化成整数 counter（从 durable claim + transcript 派生） | `tests/unit/enforcer/blogger-crash-recovery.test.mjs`（`ENFORCER_153_*`） |

## 明确删除的语义不复活

| 证明 | 证据路径 |
|------|----------|
| `ScoreVector` 路径不得复活 | ENFORCER-072/073；`tests/unit/enforcer/tip-v2-contract.test.mjs`（`ENFORCER_TIP_03_04`） |
| throttle / nudge 模块为 compiled tombstone，零生产调用 | `tests/unit/enforcer/throttle.test.mjs`；`src/Wanxiangshu/Domain/EnforcerThrottle.fs` / `EnforcerNudge.fs`（`Removed = true`） |
| `catalog.json` 不得作为规则 SSOT 或装载输入 | folder loader + catalog 测试仅扫目录；无 JSON catalog 装载路径 |

## 端到端

Blogger 路径 canary：工具必须调用、busy skip 不推进 coverage、成功提交推进 RecordCoverage。  
Main tip 路径 canary：首次 Full main.md、重复 Identity、compaction reanchor 后再 Full。  
Observation 路径 canary：提交 tip 入 RecentTips；squash tip co-move；Companion 配对 unit。  
与 CTX/HOST 交叉：compaction 后 reanchor 不把 Host 摘要当 BlogFrame；TipDelivery 与 Blog/Prefix 同原子清空 Full 集合。

## Playbook G7 Exit

**G7 Product Exit DONE**（2026-08-12 Amendment）：`enforcer-rulebook-gate --require-headings --strict` mechanical A37/A38 production 120 = machine Exit。  
`HUMAN_ONLY_RUBRIC_ITEMS`（paired-history 120 / A39 pair review / A40 tournament）诚实保留为目录质量过程：不伪造人工赛，也不再阻断 Playbook Gate。lexical `enforcer-cross-family-collision` ≠ human tournament，仅机械辅助。

## 发布面

规则正文变更 = 数据变更（`resources/enforcer/<TipName>/{enforcer.md,main.md}`）+ 对应测试；不得只改文档不改目录正文。  
`ScoreVector` 路径不得复活（ENFORCER-072/073）。  
不得重新引入 `catalog.json` 作为第二真相。
