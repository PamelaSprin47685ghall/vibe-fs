# PROOF —— 测试落点表（durable-convergence）

## 运行方式

```bash
node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs   # 本包 NEW
node requirements/verification-system/tests/run.mjs                                                          # 全量
```

本包 1 个 NEW 测试文件（5 断言）单独跑绿；物理律的既有证明 REUSE 原位
（`requirements/durable-convergence/tests/event-store-merge.test.mjs`、`event-store-converge.test.mjs`、
`requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs`，以及已迁入 `durable-events/tests/`
的 fold/append 文件）。

## 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| DURABLE-CONVERGENCE-001 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::set_union_never_drops_concurrent_events`（两个并发 EventId 都存活）+ `requirements/durable-convergence/tests/event-store-merge.test.mjs::merge_identity_collision_fail_closed`（同 id 异 bytes 之外的一切都保留） | NEW + REUSE | `node --test requirements/durable-convergence/tests/replica-merge-laws.test.mjs` / `node --test requirements/durable-convergence/tests/event-store-merge.test.mjs` |
| DURABLE-CONVERGENCE-002 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::merge_is_commutative_associative_idempotent`（commutative/associative/idempotent/deterministic 四律）+ `requirements/durable-convergence/tests/event-store-merge.test.mjs::merge_spec_oracle_associative_commutative_idempotent_deterministic`、`merge_production_associative_commutative_idempotent_deterministic` | NEW + REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-003 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::production_merge_matches_the_set_union_spec_oracle`（production ≡ materialize(union) ≡ spec oracle）+ `requirements/durable-convergence/tests/event-store-merge.test.mjs::merge_production_matches_materialize_of_union`、`CompareAndSwapRef_Absent_then_expected_oid` | NEW + REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-004 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::concurrent_heads_fold_to_DomainConflict_and_resolution_collapses`（fork 可 fold、Conflict 表达、非 StorageInvalid）+ `requirements/durable-events/tests/event-store-fold.test.mjs::fold_concurrent_heads_are_DomainConflict_not_StorageInvalid`（跨包 REUSE：反向钉死非 StorageInvalid） | NEW + REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-005 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::concurrent_heads_fold_to_DomainConflict_and_resolution_collapses`（resolution 以全部 heads 为 parents → Unique）+ `requirements/durable-events/tests/event-store-fold.test.mjs::fold_resolution_with_all_competing_heads_as_parents_leaves_conflict`（跨包 REUSE） | NEW + REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-006 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::convergence_is_a_function_of_the_event_set_not_arrival_order`（同 event set、不同到达顺序 → 同 root/同事件集）+ `requirements/durable-convergence/tests/event-store-converge.test.mjs::ConvergeStoreWithObservedRemote_skips_fetch_and_merges`（merge 无时间参数） | NEW + REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-007 | `requirements/durable-convergence/tests/replica-merge-laws.test.mjs::convergence_is_a_function_of_the_event_set_not_arrival_order`（同 set → 同 merged 世界）+ `requirements/durable-events/tests/event-store-fold.test.mjs::fold_deterministic_topological_order_with_EventId_tiebreak`（同输入同 FoldOrder，跨包 REUSE） | NEW + REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-008 | `requirements/durable-convergence/tests/event-store-converge.test.mjs`：`StoreRef_remoteTracking_helper`、`ConvergeStore_lease_reject_retries_then_ok`、`ConvergeStore_retry_exhausted`、`ConvergeStore_cas_rejected_when_maxRetries_zero`、`EventStore_createWithConverge_delegates_to_gateway`、`GitGateway_bindEventStore_wires_Converge` + `requirements/durable-events/tests/event-store-append.test.mjs::Converge_unbound_without_gateway`（跨包 REUSE：无 gateway → Transport，绝不假装同步成功） | REUSE | 各文件 `node --test` |
| DURABLE-CONVERGENCE-009 | `requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs`：`dumb_remote_helper_does_not_import_Domain_codecs`、`object_upload_to_bare_remote_via_GitGateway_converge`、`object_fetch_from_bare_remote_into_second_client`、`two_clients_merge_through_dumb_remote`、`lease_rejection_refetches_and_bounded_retry_succeeds`、`lease_rejection_bounded_retry_exhausted` | REUSE | `node --test requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs` |

## 统计

- 命题 9 条；落点行 9；NEW 1 文件（`replica-merge-laws.test.mjs`，5 断言）+ REUSE
  `requirements/durable-convergence/tests/event-store-merge.test.mjs`、`requirements/durable-convergence/tests/event-store-converge.test.mjs`、
  `requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs` + 跨包 REUSE
  `requirements/durable-events/tests/event-store-fold.test.mjs`、
  `requirements/durable-events/tests/event-store-append.test.mjs`。
- GAP：0。

## SPLIT@cutover 清单

1. `requirements/durable-convergence/tests/event-store-merge.test.mjs` / `event-store-converge.test.mjs`：
   单-owner（本包）但**暂不物理移动**——`requirements/knowledge-reuse/PROOF.md` 已按
   当前路径引用其落点 token，移动会破坏 meta-verifier 的 landing-file 检查。cutover 时
   移入本包 `tests/` 并同步更新 knowledge-reuse 的 PROOF 落点路径。
2. `requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs`：integration 本轮不迁；cutover 时随
   integration 层处理，落点仍归本包。
3. `tests/unit/casebook/`：CASE-011 的对象冲突断言（若有）按「general 律归本包、
   Case 对象语义归 knowledge-reuse」拆分；当前 casebook 测试无 DomainConflict 断言。

## 本包拥有的 semantic anchor id

空。`scripts/checks/semantic-anchors.mjs` 无 durable-convergence 语义 ID。
