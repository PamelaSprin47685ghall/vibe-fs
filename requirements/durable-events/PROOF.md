# PROOF —— 测试落点表（durable-events）

## 运行方式

```bash
node --test requirements/durable-events/tests/<file>          # 单文件
node tests/unit/run.mjs                                      # 全量（自动包含 requirements/**/tests/*.test.mjs）
```

本包 8 个测试文件（7 MOVE + 1 NEW）+ fixtures 目录，72 条断言全部单独跑绿。

## 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| DURABLE-EVENTS-001 | `requirements/durable-events/tests/append-only-laws.test.mjs::committed_event_bytes_are_never_rewritten`（重写被拒 + 原 bytes 不变）+ `same_event_set_same_root_regardless_of_batch_grouping` + `requirements/durable-events/tests/event-store-append.test.mjs::Append_idempotent_when_EventIds_already_committed` | NEW + MOVE | `node --test requirements/durable-events/tests/append-only-laws.test.mjs` / `node --test requirements/durable-events/tests/event-store-append.test.mjs` |
| DURABLE-EVENTS-002 | `requirements/durable-events/tests/append-only-laws.test.mjs::canonical_envelope_bytes_carry_no_version_tokens`（六键、零版本 token）+ `requirements/durable-events/tests/event-store-fold.test.mjs::fold_unknown_authoritative_event_type_fail_closed` + `tests/unit/verify/unified-store-gate.test.mjs::always-forbidden store version tokens are RED without extra context` | NEW + MOVE + REUSE | 各文件 `node --test` |
| DURABLE-EVENTS-003 | `requirements/durable-events/tests/event-store-identity-collision.test.mjs`：`same_EventId_different_canonical_bytes_fail_closed`、`same_EventId_same_canonical_bytes_dedupe_ok`、`canonical_bytes_are_utf8_json_plus_single_LF_with_sorted_keys`、`distinct_EventIds_are_both_retained` + `tests/unit/codec/misc-codecs.test.mjs::MISC_canonical_json_sorts_keys_recursively` | MOVE + REUSE | `node --test requirements/durable-events/tests/event-store-identity-collision.test.mjs` / `node --test tests/unit/codec/misc-codecs.test.mjs` |
| DURABLE-EVENTS-004 | `requirements/durable-events/tests/event-store-append.test.mjs`：`Append_Absent_CAS_publishes_canonical_ref`、`Publish_writes_payloads_then_CAS_appends`、`Append_CAS_conflict_retries_on_fresh_root` + `requirements/durable-events/tests/append-only-laws.test.mjs::one_event_one_blob_no_partial_write` | MOVE + NEW | 各文件 `node --test` |
| DURABLE-EVENTS-005 | `requirements/durable-events/tests/event-store-append.test.mjs`：`Append_CAS_conflict_retries_on_fresh_root`、`Append_idempotent_when_EventIds_already_committed`、`Append_retry_exhausted_when_CAS_always_rejects`、`Append_CAS_rejected_when_maxRetries_zero` + `requirements/durable-events/tests/append-only-laws.test.mjs::cas_not_witnessed_is_not_committed` | MOVE + NEW | 各文件 `node --test` |
| DURABLE-EVENTS-006 | `requirements/durable-events/tests/append-only-laws.test.mjs::cas_not_witnessed_is_not_committed`（CAS 未见证 → ref 缺席）+ `requirements/durable-events/tests/event-store-append.test.mjs::Refresh_observes_published_root` + `requirements/durable-events/tests/event-store-journal-writer.test.mjs::create_publishes_RuntimeStarted_under_refs_wanxiang_store` | NEW + MOVE | 各文件 `node --test` |
| DURABLE-EVENTS-007 | `requirements/durable-events/tests/event-store-fold.test.mjs`：`validate_matches_fold_StorageInvalid`、`fold_unknown_authoritative_event_type_fail_closed`、`fold_missing_parent_fail_closed`、`fold_cyclic_parents_fail_closed` + `requirements/durable-events/tests/event-store-append.test.mjs::Append_missing_parent_fail_closed_without_history_reload`、`Append_batch_cycle_fail_closed`、`Append_identity_collision_fail_closed` + `requirements/durable-events/tests/event-store-journal-boot.test.mjs::resumeOrCreate_malformed_journal_envelope_returns_Boot_FoldRejection` | MOVE | 各文件 `node --test` |
| DURABLE-EVENTS-008 | `requirements/durable-events/tests/event-store-fold.test.mjs::fold_concurrent_heads_are_DomainConflict_not_StorageInvalid`（本包钉「非 StorageInvalid」半边；正向收敛律归 `durable-convergence`） | MOVE | `node --test requirements/durable-events/tests/event-store-fold.test.mjs` |
| DURABLE-EVENTS-009 | `tests/unit/verify/unified-store-gate.test.mjs`：`fixture unified-store-no-migrator.mjs is RED for no-migrator`、`synthetic LegacyProjection≡NewProjection claim is RED for no-migrator`、`fixture unified-store-dual-write.fs is RED for dual-write`、`Journal-only or EventStore-only modules are not dual-write` + `tests/unit/journal/workspace-event-store-host.test.mjs::host_SharedAgentJournal_boots_EventStore_and_leaves_planted_ndjson_unread` + `tests/integration/persist/leave-unread.test.mjs::EventStore_open_append_leaves_stale_ndjson_and_blobs_unread` + `tests/unit/journal/fact-codec.test.mjs::PERSIST_005_modern_json_has_no_legacy_markers` | REUSE | `node --test tests/unit/verify/unified-store-gate.test.mjs` 等（SPLIT@cutover：gate 测试随 gate 一起迁入本包并更新 scripts/checks 扫描根） |
| DURABLE-EVENTS-010 | `tests/unit/verify/unified-store-gate.test.mjs`：`canonical refs/wanxiang/store is allowed only under Persist/Git ownership`、`owner remote-tracking store ref is allowed; other feature refs stay RED`、`fixture unified-store-feature-ref.fs is RED for feature-ref` + `requirements/durable-events/tests/event-store-append.test.mjs::OpenSnapshot_Absent_returns_empty_root_without_publishing_ref` + `requirements/durable-events/tests/hook-dispatcher.test.mjs::SYNC_ENV_name_matches_shared_literal` | REUSE + MOVE | 各文件 `node --test` |
| DURABLE-EVENTS-011 | `requirements/durable-events/tests/append-only-laws.test.mjs::materialized_root_is_a_plain_tree_with_no_commit_history`（root 只含 events/+payloads/）+ `tests/unit/persist/event-store-merge.test.mjs::EventId_shard_path_is_events_hex_prefix_EventId_jsonl`、`WriteBlob_matches_git_hash_object_sha1` | NEW + REUSE | 各文件 `node --test` |
| DURABLE-EVENTS-012 | `requirements/durable-events/tests/event-store-append.test.mjs`：`Publish_writes_payloads_then_CAS_appends`、`Publish_IncompletePayloadClosure_when_payload_missing` + `tests/unit/persist/event-store-merge.test.mjs::materializeSnapshot_payload_closure_and_missing_payload_fail_closed` | MOVE + REUSE | 各文件 `node --test` |
| DURABLE-EVENTS-013 | `requirements/durable-events/tests/event-store-append.test.mjs`：`Append_incremental_validation_object_traffic_does_not_scale_with_history`、`Append_parent_in_tip_accepted_without_reloading_siblings` + `tests/unit/journal/envelope.test.mjs`：`PERSIST_008_one_session_projection_is_reached_by_a_keyed_lookup`、`PERSIST_008_projection_size_tracks_distinct_sessions_not_history_length`、`PERSIST_008_folding_is_incremental_so_one_envelope_needs_no_replay`、`PERSIST_002_a_committed_envelope_replays_into_the_same_projection` | MOVE + REUSE | 各文件 `node --test` |
| DURABLE-EVENTS-014 | `requirements/durable-events/tests/event-store-fold.test.mjs`：`fold_deterministic_topological_order_with_EventId_tiebreak`、`fold_empty_history_ok` + `tests/unit/persist/event-store-merge.test.mjs::merge_production_associative_commutative_idempotent_deterministic` | MOVE + REUSE | 各文件 `node --test` |
| DURABLE-EVENTS-015 | `requirements/durable-events/tests/event-store-journal-boot.test.mjs`：`resumeOrCreate_malformed_journal_envelope_returns_Boot_FoldRejection`（不满足 → 拒载）、`resumeOrCreate_continues_LocalSeq_and_preserves_prior_projection`、`resumeOrCreate_replays_many_prior_journal_envelopes` + 各 domain 不变量语义 → 各 domain owner 的 fold 测试（SPLIT@cutover 清单见下） | MOVE | 各文件 `node --test` |
| DURABLE-EVENTS-016 | `tests/unit/verify/unified-store-gate.test.mjs`：`canonical refs/wanxiang/store is allowed only under Persist/Git ownership`、`fixture unified-store-git-bypass.fs is RED for git-bypass`、`git-bypass allowlist is empty; only Persist/Git ownership may invoke git` + `requirements/durable-events/tests/event-store-identity-collision.test.mjs::StoreTypes_exposes_canonical_store_ref_and_error_DUs` | REUSE + MOVE | 各文件 `node --test` |

## 统计

- 命题 16 条；落点行 16；MOVE 7 文件 + NEW 1 文件（`append-only-laws.test.mjs`，6 断言）+ REUSE 8 个现有文件（`tests/unit/verify/unified-store-gate.test.mjs`、`tests/unit/journal/envelope.test.mjs`、`tests/unit/journal/fact-codec.test.mjs`、`tests/unit/codec/misc-codecs.test.mjs`、`tests/unit/journal/workspace-event-store-host.test.mjs`、`tests/unit/persist/event-store-merge.test.mjs`、`tests/integration/persist/leave-unread.test.mjs`、`scripts/checks/unified-store-gate.mjs`）。
- GAP：0。

## SPLIT@cutover 清单

1. `tests/unit/verify/unified-store-gate.test.mjs` + `tests/unit/verify/fixtures/unified-store-*`：单-owner（本包）但「不宜移动」——gate 脚本 `scripts/checks/unified-store-gate.mjs` 的 `no-migrator` 扫描硬编码 `tests/`/`scripts/` 根与 allowlist（含当前文件路径与 fixtures 目录）。cutover 时 gate+test+fixtures 一起物理移入本包 `tests/`，并同步更新 `scripts/checks/unified-store-gate.mjs` 的 walk roots + `NO_MIGRATOR_PATH_ALLOWLIST`；在此之前 REUSE 原位。
2. `tests/integration/persist/leave-unread.test.mjs`（+ `object-identity.test.mjs`）：integration 本轮不迁；cutover 时随 integration 层处理，落点仍归本包。
3. `tests/unit/journal/envelope.test.mjs` / `fact-codec.test.mjs`：journal/NDJSON 折叠与迁移域；本包引用其 PERSIST-001/005/008 锚点；cutover 时按断言级拆分（PERSIST-001/008 部分入本包，domain 部分归 `semantic-trace`/`work-record` 等）。
4. `tests/unit/persist/event-store-merge.test.mjs`：merge 物理律由 `durable-convergence` 拥有（知识复用包 PROOF 已按当前路径引用）；cutover 时若移入 `durable-convergence/tests/` 需同步更新 `requirements/knowledge-reuse/PROOF.md` 落点路径。

## 本包拥有的 semantic anchor id

空。`scripts/checks/semantic-anchors.mjs` 无 durable-events 语义 ID。
