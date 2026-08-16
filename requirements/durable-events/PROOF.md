# PROOF —— 测试落点表（durable-events）

> 2026-08-14 shock cut。新/改写 oracle 已按用户要求 **FROZEN，未执行**；本文件记录可红落点，
> 不声称当前测试结果。旧 online-Git EventStore（segment/index/OpenSnapshot/CAS）的 proof 已废弃。

## 运行方式（解冻后）

```bash
node --test requirements/durable-events/tests/local-process-event-log.test.mjs
node --test requirements/durable-events/tests/canonical-integrator.test.mjs
node --test requirements/durable-events/tests/event-store-append.test.mjs
node --test requirements/durable-events/tests/event-store-journal-writer.test.mjs
node --test requirements/durable-events/tests/journal-payload-closure.test.mjs
node --test requirements/durable-events/tests/event-store-journal-boot.test.mjs
node --test requirements/durable-events/tests/workspace-event-store-host.test.mjs
node --test requirements/durable-events/tests/hook-dispatcher.test.mjs
node --test requirements/durable-events/tests/integration/persist/leave-unread.test.mjs
```

## 命题 → 落点

| 命题 | 落点测试 | 类型 |
|---|---|---|
| DURABLE-EVENTS-001 | `append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-001] append_only_prior_writer_bytes_are_a_strict_prefix_after_new_fact` + `event-store-append.test.mjs::WHAT[DURABLE-EVENTS-001] append_commits_complete_canonical_line_then_updates_Current` + `envelope.test.mjs::WHAT[DURABLE-EVENTS-001] PERSIST_002_a_committed_envelope_replays_into_the_same_projection` | NEW/FROZEN |
| DURABLE-EVENTS-002 | `envelope.test.mjs::WHAT[DURABLE-EVENTS-002] PERSIST_001_an_envelope_serializes_to_exactly_one_line` + `fact-codec.test.mjs::WHAT[DURABLE-EVENTS-002] MIGRATION_*`（additive codec / 冻结 payload shape）+ `event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] EventType_is_exactly_JournalEnvelope`（JournalEnvelope 单一 event_type）+ `host-turn-observed.test.mjs::WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_*` + `unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-002] *`（store 无版本 token） | REUSE + NEW/FROZEN |
| DURABLE-EVENTS-003 | `event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-003] *` + `event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-003] DURABLE_EVENTS_003_same_EventId_same_bytes_dedupes` / `DURABLE_EVENTS_003_same_EventId_different_bytes_fail_closed` + `misc-codecs-canonical-json.test.mjs::WHAT[DURABLE-EVENTS-003] *` + `envelope.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_*`（canonical bytes 稳定） | REUSE + NEW/FROZEN |
| DURABLE-EVENTS-004 | `event-store-append.test.mjs::WHAT[DURABLE-EVENTS-004] append_task_does_not_return_until_the_cross_process_store_lock_is_released` + `local-process-event-log.test.mjs`（完整 NDJSON 行 append） | NEW/FROZEN |
| DURABLE-EVENTS-005 | `local-process-event-log.test.mjs::{WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments,WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity}` + `append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-005] one_writer_is_one_file_regardless_of_history_size` | NEW/FROZEN |
| DURABLE-EVENTS-006 | `event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-006] append_adds_one_local_line_and_Current_is_already_integrated` + `append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-006] duplicate_same_identity_is_idempotent_but_collision_is_rejected` | NEW |
| DURABLE-EVENTS-007 | `event-store-append.test.mjs::{WHAT[DURABLE-EVENTS-007] append_rejects_missing_parent_without_writing_bytes,WHAT[DURABLE-EVENTS-007] append_rejects_cycle_in_one_batch_before_durability,WHAT[DURABLE-EVENTS-007] append_rejects_unknown_event_type_fail_closed}` + `event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-007] *`（merge 层 fail closed）+ `event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_missing_parent_fails_closed` + `envelope.test.mjs::WHAT[DURABLE-EVENTS-007] PERSIST_005_malformed_json_is_an_error_value_not_an_exception` + `fact-codec.test.mjs::WHAT[DURABLE-EVENTS-007] PERSIST_005_*`（decode error 不 throw） | NEW/FROZEN |
| DURABLE-EVENTS-008 | `event-store-fold.test.mjs::{WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_concurrent_heads_remain_distinct_in_structural_Current,WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_resolution_naming_all_heads_collapses_structural_Current}` + `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::concurrent_heads_are_preserved_as_structural_DomainConflict_frontier` | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-EVENTS-009 | `integration/persist/leave-unread.test.mjs::{local_EventStore_never_reads_or_rewrites_any_legacy_layout,shock_cut_source_has_no_legacy_shape_detection_migration_or_reset}` + `unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] *`（no-migrator/dual-write/student-qa 门禁）+ `envelope.test.mjs::WHAT[DURABLE-EVENTS-009] PERSIST_005_*`（pre-0.5.0 拒绝）+ `workspace-event-store-host.test.mjs::WHAT[DURABLE-EVENTS-009] SharedAgentJournal_cache_hit_returns_same_instance_without_rereading_retired_path` | NEW/FROZEN + REUSE |
| DURABLE-EVENTS-010 | `workspace-event-store-host.test.mjs::WHAT[DURABLE-EVENTS-010] SharedAgentJournal_boots_local_EventStore_and_leaves_retired_RuntimePath_ndjson_unread` + `local-process-event-log.test.mjs`（`.git/wanxiang/events/<WriterId>.ndjson` 为 runtime substrate） | NEW/FROZEN |
| DURABLE-EVENTS-011 | `local-process-event-log.test.mjs::WHAT[DURABLE-EVENTS-011] one_complete_writer_file_is_one_blob_only_at_remote_sync_boundary`（一 writer 文件 = 一 blob；无 chunk/segment/delta；主进程不 fetch/pull/push）+ `requirements/durable-convergence/tests/writer-stream-sync.test.mjs::DURABLE_CONVERGENCE_003_sync_blobifies_each_complete_writer_file_once_without_segments_or_index` | NEW + CROSS/FROZEN |
| DURABLE-EVENTS-012 | `journal-payload-closure.test.mjs::WHAT[DURABLE-EVENTS-012] *` + `event-store-journal-writer.test.mjs::{WHAT[DURABLE-EVENTS-012] appended_fact_lifts_real_blob_digest_into_persisted_payload_refs,WHAT[DURABLE-EVENTS-012] closure_fails_closed_when_a_real_content_address_is_missing,WHAT[DURABLE-EVENTS-012] BlobWriter_uses_local_content_addressed_payloads_not_workspace_blobs_or_Git_ODB,WHAT[DURABLE-EVENTS-012] journal_writer_source_has_no_snapshot_CAS_or_Git_raw_store}` + `requirements/speculative-investigation/tests/store.test.mjs::STRENGTH_006_payload_bytes_are_local_content_addressed_payloads` | NEW + CROSS |
| DURABLE-EVENTS-013 | `canonical-integrator.test.mjs::WHAT[DURABLE-EVENTS-013] DURABLE_EVENTS_013_boot_and_live_share_the_same_single_event_integration_program` + `event-store-journal-boot.test.mjs::{WHAT[DURABLE-EVENTS-013] restart_replays_prior_writer_files_then_fresh_runtime_starts_LocalSeq_at_1,WHAT[DURABLE-EVENTS-013] boot_and_live_use_one_CanonicalIntegrator_program}` + `journal-subscription.test.mjs::WHAT[DURABLE-EVENTS-013] *` + `envelope.test.mjs::WHAT[DURABLE-EVENTS-013] PERSIST_008_*` + `session-association-keyed-lookup.test.mjs::WHAT[DURABLE-EVENTS-013] *`（keyed lookup 不扫描历史） | NEW/FROZEN |
| DURABLE-EVENTS-014 | `event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_writer_enumeration_independent` + `event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_deterministic_with_EventId_tiebreak` + `envelope.test.mjs::WHAT[DURABLE-EVENTS-014] PERSIST_001_*`（merge 总序）+ `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-EVENTS-015 | `fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] *`（11 个：PERSIST_010 / CTX_011 / CTX_012 / HOST_006 fold 不变量） | REUSE |
| DURABLE-EVENTS-016 | `unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] *`（Domain 禁 Git physical types；feature backend/history-reader gate）+ `event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-016] StoreTypes_exposes_canonical_store_ref_and_error_DUs` | REUSE |
| DURABLE-EVENTS-017 | `local-process-event-log.test.mjs::WHAT[DURABLE-EVENTS-017] DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies` + `event-store-append.test.mjs::WHAT[DURABLE-EVENTS-017] append_cost_contract_is_independent_of_history_and_EventId_distribution` + `append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-017] append_path_has_no_Git_object_or_ref_capability` | NEW/FROZEN |
| DURABLE-EVENTS-018 | `hook-dispatcher.test.mjs::WHAT[DURABLE-EVENTS-018] *`（activation ensure / 独立 hook 运行时 / 不运行 sync）+ `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` | NEW/FROZEN |
| DURABLE-EVENTS-019 | `canonical-integrator.test.mjs::{WHAT[DURABLE-EVENTS-019] DURABLE_EVENTS_019_canonical_integrator_is_an_FSharp_CE_with_registered_business_rules,WHAT[DURABLE-EVENTS-019] DURABLE_EVENTS_013_019_business_modules_do_not_own_history_read_or_replay_loops,WHAT[DURABLE-EVENTS-019] DURABLE_EVENTS_019_only_CanonicalIntegrator_may_derive_Current_from_event_history}` | NEW/FROZEN |
| DURABLE-EVENTS-020 | `event-store-journal-boot.test.mjs::WHAT[DURABLE-EVENTS-020] empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation`；`event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-020] create_is_read_only_until_the_first_business_append` + first business append 两行顺序；REUSE `requirements/host-boundary/tests/plugin-load-purity.test.mjs` | NEW + CROSS |
| DURABLE-EVENTS-021 | `event-store-append.test.mjs::WHAT[DURABLE-EVENTS-021] semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next`（bad fact + ProjectionCutTail 同批 durable；typed cut receipt；same feature next success；reopen replay）；`canonical-integrator.test.mjs` source gate（PlanCut/ApplyCut + one full replay budget） | NEW |

## GAP

- `GAP-013` —— **CLOSED**：production append 已切为 `.git/wanxiang/events/<WriterId>.ndjson`；Git blob/tree/ref 只在独立 remote-hook sync；一 writer 文件一 blob；旧 segment/index/CAS 实现已移出编译图并标 GARBAGE。落点：`local-process-event-log.test.mjs`、`event-store-append.test.mjs`、`requirements/durable-convergence/tests/writer-stream-sync.test.mjs`（均 FROZEN 未执行）。
- `GAP-014` —— **CLOSED**：`CanonicalIntegrator` 是唯一 history enumerator，以 F# `IntegratorBuilder` CE 注册 Structural/Journal/Strength/Casebook/JsTransaction 单-event oracle；business modules 已无 `loadEvents`/history project API。落点：`canonical-integrator.test.mjs` + feature Current tests（FROZEN 未执行）。

## 统计

- WHAT 命题：21；PROOF 行：21。
- 本包 GAP：0（GAP-013 / GAP-014 已关闭；测试仍按用户要求冻结，未执行）。
