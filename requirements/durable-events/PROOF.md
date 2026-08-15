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
| DURABLE-EVENTS-001 | `append-only-laws.test.mjs::append_only_prior_writer_bytes_are_a_strict_prefix_after_new_fact` + `event-store-append.test.mjs::append_commits_complete_canonical_line_then_updates_Current` | NEW/FROZEN |
| DURABLE-EVENTS-002 | `envelope.test.mjs` / `fact-codec.test.mjs`（无版本 canonical envelope / additive codec）+ `event-store-append.test.mjs::append_rejects_unknown_event_type_fail_closed` | REUSE + NEW/FROZEN |
| DURABLE-EVENTS-003 | `event-store-identity-collision.test.mjs` + `event-store-merge.test.mjs::{same_EventId_same_bytes_dedupes,same_EventId_different_bytes_fail_closed}` | REUSE + NEW/FROZEN |
| DURABLE-EVENTS-004 | `local-process-event-log.test.mjs::DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments` + `append-only-laws.test.mjs` | NEW/FROZEN |
| DURABLE-EVENTS-005 | `local-process-event-log.test.mjs::{DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments,DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity}` | NEW/FROZEN |
| DURABLE-EVENTS-006 | `event-store-journal-writer.test.mjs::{create_is_read_only_until_the_first_business_append,append_adds_one_local_line_and_Current_is_already_integrated}` | NEW |
| DURABLE-EVENTS-007 | `event-store-append.test.mjs::{append_rejects_missing_parent_without_writing_bytes,append_rejects_cycle_in_one_batch_before_durability,append_rejects_unknown_event_type_fail_closed}` + `event-store-merge.test.mjs::same_EventId_different_bytes_fail_closed` | NEW/FROZEN |
| DURABLE-EVENTS-008 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::concurrent_heads_are_preserved_as_structural_DomainConflict_frontier`（合法 fork 保留，不是 StorageInvalid） | CROSS/FROZEN |
| DURABLE-EVENTS-009 | `integration/persist/leave-unread.test.mjs::{local_EventStore_never_reads_or_rewrites_any_legacy_layout,shock_cut_source_has_no_legacy_shape_detection_migration_or_reset}` + `unified-store-gate.test.mjs` no-migrator gate | NEW/FROZEN + REUSE |
| DURABLE-EVENTS-010 | `local-process-event-log.test.mjs` + `workspace-event-store-host.test.mjs`（`.git/wanxiang/events/<WriterId>.ndjson` 为 runtime substrate） | NEW/FROZEN |
| DURABLE-EVENTS-011 | `requirements/durable-convergence/tests/writer-stream-sync.test.mjs::DURABLE_CONVERGENCE_003_sync_blobifies_each_complete_writer_file_once_without_segments_or_index` | CROSS/FROZEN |
| DURABLE-EVENTS-012 | `journal-payload-closure.test.mjs` + `event-store-journal-writer.test.mjs::{appended_fact_lifts_real_blob_digest_into_persisted_payload_refs,closure_fails_closed_when_a_real_content_address_is_missing,BlobWriter_uses_local_content_addressed_payloads_not_workspace_blobs_or_Git_ODB}` + `requirements/speculative-investigation/tests/store.test.mjs::STRENGTH_006_payload_bytes_are_local_content_addressed_payloads` | NEW + CROSS |
| DURABLE-EVENTS-013 | `canonical-integrator.test.mjs::DURABLE_EVENTS_013_boot_and_live_share_the_same_single_event_integration_program` + Casebook/Strength/JsTransaction Current tests | NEW/FROZEN |
| DURABLE-EVENTS-014 | `event-store-merge.test.mjs::DURABLE_EVENTS_014_k_way_merge_is_writer_enumeration_independent` + `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-EVENTS-015 | `fold-context-recovery.test.mjs` + Journal one-event fold suites (`host-turn-observed.test.mjs`, domain fact fold tests)；history iteration 已从 `Composition/Durable/Fold.fs` 移除 | REUSE |
| DURABLE-EVENTS-016 | `unified-store-gate.test.mjs`（Domain 禁 Git physical types；feature backend/history-reader gate） | REUSE |
| DURABLE-EVENTS-017 | `local-process-event-log.test.mjs::DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies` + `event-store-append.test.mjs::append_cost_contract_is_independent_of_history_and_EventId_distribution` | NEW/FROZEN |
| DURABLE-EVENTS-018 | `hook-dispatcher.test.mjs::{HOOK_activation_ensure_installs_both_hooks_and_remote_fetch_refspec_without_running_sync,HOOK_reference_transaction_and_pre_push_launch_the_same_independent_full_converge_runtime}` + `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` | NEW/FROZEN |
| DURABLE-EVENTS-019 | `canonical-integrator.test.mjs::{DURABLE_EVENTS_019_canonical_integrator_is_an_FSharp_CE_with_registered_business_rules,DURABLE_EVENTS_013_019_business_modules_do_not_own_history_read_or_replay_loops,DURABLE_EVENTS_019_only_CanonicalIntegrator_may_derive_Current_from_event_history}` | NEW/FROZEN |
| DURABLE-EVENTS-020 | `event-store-journal-boot.test.mjs::empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation`；`event-store-journal-writer.test.mjs::create_is_read_only_until_the_first_business_append` + first business append 两行顺序；REUSE `requirements/host-boundary/tests/plugin-load-purity.test.mjs` | NEW + CROSS |
| DURABLE-EVENTS-021 | `event-store-append.test.mjs::semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next`（bad fact + ProjectionCutTail 同批 durable；typed cut receipt；same feature next success；reopen replay）；`canonical-integrator.test.mjs` source gate（PlanCut/ApplyCut + one full replay budget） | NEW |

## GAP

- `GAP-013` —— **CLOSED**：production append 已切为 `.git/wanxiang/events/<WriterId>.ndjson`；Git blob/tree/ref 只在独立 remote-hook sync；一 writer 文件一 blob；旧 segment/index/CAS 实现已移出编译图并标 GARBAGE。落点：`local-process-event-log.test.mjs`、`event-store-append.test.mjs`、`requirements/durable-convergence/tests/writer-stream-sync.test.mjs`（均 FROZEN 未执行）。
- `GAP-014` —— **CLOSED**：`CanonicalIntegrator` 是唯一 history enumerator，以 F# `IntegratorBuilder` CE 注册 Structural/Journal/Strength/Casebook/JsTransaction 单-event oracle；business modules 已无 `loadEvents`/history project API。落点：`canonical-integrator.test.mjs` + feature Current tests（FROZEN 未执行）。

## 统计

- WHAT 命题：21；PROOF 行：21。
- 本包 GAP：0（GAP-013 / GAP-014 已关闭；测试仍按用户要求冻结，未执行）。
