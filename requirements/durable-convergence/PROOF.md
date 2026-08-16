# PROOF —— 测试落点表（durable-convergence）

> 2026-08-14 shock cut。所有本轮新/改写 oracle 按用户要求 **FROZEN，未执行**。
> Git snapshot merge / product-process `ConvergeStore` 的旧 proof 已废弃；同步宿主现在是独立 Git hook 进程。

## 运行方式（解冻后）

```bash
node --test requirements/durable-convergence/tests/event-store-merge.test.mjs
node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs
node --test requirements/durable-convergence/tests/writer-stream-sync.test.mjs
node --test requirements/durable-convergence/tests/event-store-converge.test.mjs
node --test requirements/durable-convergence/tests/dumb-remote-no-domain.test.mjs
node --test requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs
```

## 命题 → 落点

| 命题 | 落点测试 | 类型 |
|---|---|---|
| DURABLE-CONVERGENCE-001 | `tests/event-store-merge.test.mjs::set union never drops distinct events` + `tests/replica-merge-laws.test.mjs::set union never drops concurrent events` | NEW/FROZEN |
| DURABLE-CONVERGENCE-002 | `tests/writer-stream-sync.test.mjs::one k-way primitive is shared by integrator and sync` + `tests/replica-merge-laws.test.mjs::merge is commutative associative idempotent at writer stream level` + `tests/event-store-merge.test.mjs::writer enumeration is commutative` + `tests/event-store-merge.test.mjs::duplicate stream input is idempotent by EventId` | NEW/FROZEN |
| DURABLE-CONVERGENCE-003 | `tests/writer-stream-sync.test.mjs::sync blobifies each complete writer file once without segments or index` + `tests/writer-stream-sync.test.mjs::runtime append and external hook share one physical store gate` + `tests/event-store-merge.test.mjs::identity collision is fail closed not LWW` | NEW/FROZEN |
| DURABLE-CONVERGENCE-004 | `tests/replica-merge-laws.test.mjs::concurrent heads are preserved as structural DomainConflict frontier` | NEW/FROZEN |
| DURABLE-CONVERGENCE-005 | `tests/replica-merge-laws.test.mjs::resolution with all competing heads collapses structural frontier` | NEW/FROZEN |
| DURABLE-CONVERGENCE-006 | `tests/replica-merge-laws.test.mjs::convergence is a function of event truth not arrival wall clock` | NEW/FROZEN |
| DURABLE-CONVERGENCE-007 | `tests/writer-stream-sync.test.mjs::sync does not integrate business history` + `requirements/durable-events/tests/canonical-integrator.test.mjs` | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-CONVERGENCE-008 | `tests/event-store-converge.test.mjs::reference-transaction and pre-push both call the same full bidirectional converge` + `tests/event-store-converge.test.mjs::reference-transaction observed root changes discovery only not sync direction` + `tests/event-store-converge.test.mjs::lease race refetches and repeats the same k-way sync boundedly` + `tests/event-store-converge.test.mjs::product process has no fetch pull push remote API` + `tests/event-store-converge.test.mjs::hook-internal Git commands are recursion guarded and pre-push is not reentered` + `tests/writer-stream-sync.test.mjs::activation only ensures hooks and user Git process runs full sync` | NEW/FROZEN |
| DURABLE-CONVERGENCE-009 | `tests/dumb-remote-no-domain.test.mjs::dumb remote fixture has no Wanxiang domain or server-side logic` + `tests/integration/persist/dumb-server.test.mjs::dumb_remote_helper_has_no_Wanxiang_domain_or_projection_logic` + `tests/integration/persist/dumb-server.test.mjs::pre_push_hook_process_uploads_one_local_writer_file_to_bare_remote_store_ref` + `tests/integration/persist/dumb-server.test.mjs::second_machine_hook_imports_remote_writer_truth_without_any_running_Wanxiang_process` + `tests/integration/persist/dumb-server.test.mjs::two_offline_clients_converge_by_whole_writer_files_and_repeat_is_idempotent` | NEW/FROZEN |

## 统计

- WHAT 命题：9；PROOF 行：9。
- 统一 k-way primitive：`Infrastructure/Persist/EventKWayMerge.fs`，由 `CanonicalIntegrator` 与 `WriterStreamSync` 共同调用。
- remote sync trigger：plugin Load Phase 零 Git mutation；durability activation 才 `HookDispatcher.ensure`；实际执行由 `resources/git/wanxiang-hook.mjs` → `HookSync` 独立进程完成。
- GAP：0。
