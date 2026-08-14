# PROOF —— 测试落点表（durable-convergence）

> 2026-08-14 shock cut。所有本轮新/改写 oracle 按用户要求 **FROZEN，未执行**。
> Git snapshot merge / product-process `ConvergeStore` 的旧 proof 已废弃；同步宿主现在是独立 Git hook 进程。

## 运行方式（解冻后）

```bash
node --test requirements/durable-convergence/tests/event-store-merge.test.mjs
node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs
node --test requirements/durable-convergence/tests/writer-stream-sync.test.mjs
node --test requirements/durable-convergence/tests/event-store-converge.test.mjs
node --test requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs
```

## 命题 → 落点

| 命题 | 落点测试 | 类型 |
|---|---|---|
| DURABLE-CONVERGENCE-001 | `event-store-merge.test.mjs::DURABLE_CONVERGENCE_001_set_union_never_drops_distinct_events` + `replica-merge-laws.test.mjs::set_union_never_drops_concurrent_events` | NEW/FROZEN |
| DURABLE-CONVERGENCE-002 | `writer-stream-sync.test.mjs::DURABLE_CONVERGENCE_002_003_one_k_way_primitive_is_shared_by_integrator_and_sync` + `replica-merge-laws.test.mjs::merge_is_commutative_associative_idempotent_at_writer_stream_level` | NEW/FROZEN |
| DURABLE-CONVERGENCE-003 | `writer-stream-sync.test.mjs::{DURABLE_CONVERGENCE_002_003_one_k_way_primitive_is_shared_by_integrator_and_sync,DURABLE_CONVERGENCE_003_sync_blobifies_each_complete_writer_file_once_without_segments_or_index}` + `event-store-merge.test.mjs::DURABLE_CONVERGENCE_003_identity_collision_is_fail_closed_not_LWW` | NEW/FROZEN |
| DURABLE-CONVERGENCE-004 | `replica-merge-laws.test.mjs::concurrent_heads_are_preserved_as_structural_DomainConflict_frontier` | NEW/FROZEN |
| DURABLE-CONVERGENCE-005 | `replica-merge-laws.test.mjs::resolution_with_all_competing_heads_collapses_structural_frontier` | NEW/FROZEN |
| DURABLE-CONVERGENCE-006 | `replica-merge-laws.test.mjs::convergence_is_a_function_of_event_truth_not_arrival_wall_clock` + `event-store-merge.test.mjs::DURABLE_CONVERGENCE_003_identity_collision_is_fail_closed_not_LWW` | NEW/FROZEN |
| DURABLE-CONVERGENCE-007 | `writer-stream-sync.test.mjs::DURABLE_CONVERGENCE_007_sync_does_not_integrate_business_history` + `requirements/durable-events/tests/canonical-integrator.test.mjs` | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-CONVERGENCE-008 | `event-store-converge.test.mjs::{reference_transaction_and_pre_push_both_call_the_same_full_bidirectional_converge,product_process_has_no_fetch_pull_push_remote_api}` + `writer-stream-sync.test.mjs::DURABLE_CONVERGENCE_008_startup_only_ensures_hooks_and_user_Git_process_runs_full_sync` + `requirements/durable-events/tests/hook-dispatcher.test.mjs` | NEW/FROZEN |
| DURABLE-CONVERGENCE-009 | `integration/persist/dumb-server.test.mjs::{dumb_remote_helper_has_no_Wanxiang_domain_or_projection_logic,pre_push_hook_process_uploads_one_local_writer_file_to_bare_remote_store_ref,second_machine_hook_imports_remote_writer_truth_without_any_running_Wanxiang_process,two_offline_clients_converge_by_whole_writer_files_and_repeat_is_idempotent}` | NEW/FROZEN |

## 统计

- WHAT 命题：9；PROOF 行：9。
- 统一 k-way primitive：`Infrastructure/Persist/EventKWayMerge.fs`，由 `CanonicalIntegrator` 与 `WriterStreamSync` 共同调用。
- remote sync trigger：startup 只 `HookDispatcher.ensure`；实际执行由 `resources/git/wanxiang-hook.mjs` → `HookSync` 独立进程完成。
- GAP：0。
