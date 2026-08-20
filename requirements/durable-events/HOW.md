# HOW —— durable-events（实现模型与约束）

> 本文件**非 normative**。行为合同在 `WHAT.md`；本文件回答「代码在哪里、怎么工作」，
> 并收纳历史与弃权裁决。

## 生产装配链

```text
ProcessEventLog(commonDir, WriterId)         // Persistence/EventStore/ProcessEventLog.fs
  → .git/wanxiang/events/<WriterId>.ndjson   // one process = one file; never sliced
  → CanonicalIntegrator CE                   // the only history consumer
  → EventStore.createLocal                   // append + payload closure + Current
  → WorkspaceEventStore.acquire（process-local refcount）
  → IJournalEventStoreBoot.ResumeOrCreate / EventStoreJournalWriter
  → AgentJournal.createFromProjection

JS semantic boundary (requirements/durable-events/tests)
  → EventStore/CodecSurface.js       // canonical bytes + identity result objects
  → EventStore/MergeSurface.js       // deterministic merge of plain events
  → EventStore/Surface.js             // opaque EventStoreHandle lifecycle + append/read
  → Journal/CodecSurface.js          // plain journal envelope codec
  → Journal/FactCodecSurface.js      // decode-only fact compatibility
  → Journal/Surface.js               // opaque JournalHandle + plain projection summary

Wanxiangshu/OpenCode Load Phase
  → parse module/resources/config + validate/read durable bytes
  → best-effort fold may populate Current, but semantic rejection cannot escape load
  → no HookDispatcher.ensure / recovery / Host re-entry / durable business write
  → return hooks/tools

first durable capability activation
  → open/fold Current as demanded
  → append RuntimeStarted lazily before first durable business fact
  → HookDispatcher.ensure：install/refresh reference-transaction + pre-push
  → ensure each remote fetches refs/wanxiang/store into remote-tracking ref
  → stop（产品进程不主动 fetch/pull/push）

later, user Git process (Wanxiangshu may be absent)
  → installed hook launches resources/git/wanxiang-hook.mjs
  → HookSync + GitGateway physical transport
  → local writer files + remote writer blobs
  → EventKWayMerge
  → one local writer file = one Git blob snapshot
  → replace local truth + publish remote EventStore snapshot
```

运行时 append/replay 不调用 Git object API。`HookDispatcher` 只在 durable activation ensure hook/refspec，绝不进入 plugin Load Phase；`HookSync` 才是独立 Git hook 子进程的同步入口，并以 `WANXIANG_GIT_SYNC_ACTIVE` 防递归。

## 分层与所有权（shape/persist.md PERSIST-006 / storage.md §45）

| 层 | 拥有 | 不得拥有 |
|---|---|---|
| Domain | `EventEnvelope` / `PayloadRef` / 因果与业务语义 | `GitObjectId` / `RootOid` / `StoreSnapshot` / Git 操作 |
| Persist | canonical JSON、本地 process NDJSON、payload closure、k-way input、唯一 `CanonicalIntegrator` CE、sync materialization | 领域业务判断、feature-owned history reader、按 domain 拆 backend |
| HookDispatcher / HookSync / GitGateway | activation-time hook/refspec ensure；独立 Git-hook 进程 transport、writer-file blobification、remote snapshot replace | plugin Load Phase mutation、产品进程主动 fetch/pull/push、Domain reducer、后台同步状态机、第二套业务积分器 |
| Semantic owner surfaces | `EventStore/CodecSurface`, `EventStore/MergeSurface`, `EventStore/Surface`, `Journal/CodecSurface`, `Journal/FactCodecSurface`, `Journal/Surface`; each names one law family and translates to JS-native values | Fable list/union values, typed-ID constructors, test-side interop facades |

红线：`src/Wanxiangshu/Persistence/EventStore/` 模块不引用 `OpenCode`/`Process` 物理层；Git object identity 只存在于
sync membrane；业务模块不能取得 `IEventHistoryReader`/本地 writer 文件路径。

## 核心机制（逐概念）

### 1. Canonical JSON（`Persistence/EventStore/CanonicalEventCodec.fs` + `CodecSurface.fs`）

```text
normalizeJson：递归 Object.keys 排序
envelopeObject：event_id / stream_id / event_type / parents / payload / payload_refs（固定六键）
encode：JSON.stringify(normalizeJson) + "\n"        // 恰好一个 LF
checkIdentity：同 id 同 bytes → Ok；同 id 异 bytes → StorageInvalid.IdentityCollision
mergeByIdentity：set-union by EventId，输出按 EventId 排序
tryDecode：null/非单 LF/形状错/重编码不等 → StorageInvalid（NonCanonical | MalformedEnvelope）
```

### 2. Store / local-log 类型（`Persistence/EventStore/StoreTypes.fs` / `ProcessEventLog.fs`）

```text
WriterId：process-start fresh globally-unique physical writer identity
LocalFrontier：每个 WriterId 当前完整 byte length / last EventId（仅 Integrator/Persist 可见）
StoreSnapshot：仅 remote-sync membrane 的 Git root snapshot；不是本地 Current/frontier witness
StorageInvalid = IdentityCollision | NonCanonical | MalformedEnvelope | MissingParent
              | CyclicParents | MissingPayload | UnknownEventType
DomainConflict = ConcurrentHeads of streamId * heads
GitObjectId / RootOid / StoreRef：只在 remote sync materialization/transport 使用
```

### 3. Local Append / Publish（`Persistence/EventStore/Store.fs` / `ProcessEventLog.fs` + `Surface.fs`）

```text
createLocal：生成 fresh WriterId；打开 .git/wanxiang/events/<WriterId>.ndjson（append-only）
boot：CanonicalIntegrator 独占读取 .git/wanxiang/events/*.ndjson → k-way merge → Current
validateAppendSet：只向 Integrator 查询 Current/indexed identity/parents；不自行扫描历史
append：acquire cross-process `.git/wanxiang` physical gate
        → structural validate → CanonicalIntegrator.prepareLive
        → rule success: event 正常推进 Current
        → rule semantic error: bad event 保留 + rule-owned reset patch → 追加 ProjectionCutTail
        → bad event / cut reset / 其余 events 按原时序 append complete JSON+LF → durability barrier
        → commit 已准备好的 Current/frontier；返回 AppendReceipt.Cuts → release gate
publish：先在同一 physical gate 下把新增 payload bytes 写 .git/wanxiang/payloads/<PayloadRef>；
        event append 再独立取得 gate、验证 closure 后提交
```

同一 gate 也由 standalone Git-hook sync 在整个 local snapshot/import + remote publication 窗口持有，
所以运行中的 writer 与外部 `git fetch/pull/push` 不会互相观察半截文件；它只是跨进程物理资源锁，
不表达业务 stage/Current，也不构成状态机。运行时等待该物理资源不使用业务 10s watchdog。

没有 `SegmentMaxBytes`、rotation、tail rewrite、EventId→blob index、Git tree/CAS retry。
`WriterId.ndjson` 从进程开始一直增长到进程退出；新进程只创建新 WriterId。

### 4. K-way merge / Sync（`Persistence/EventStore/EventKWayMerge.fs` / `CanonicalIntegrator.fs` + `MergeSurface.fs` / independent hook）

```text
local source：每个 WriterId 一个天然有序 NDJSON stream
remote source：remote root 中每个 WriterId 对应一个完整-file Git blob
merge：existing deterministic k-way merge over ordered streams
       same EventId + same canonical bytes → dedupe
       same EventId + different canonical bytes → IdentityCollision
sync materialization：统一 history → 替换本地 writer-file snapshot + remote writer→blob snapshot
reference-transaction：observed remote root → 同一个 full bidirectional converge
pre-push：discover remote root → 同一个 full bidirectional converge
```

单机多进程与多机没有不同算法；machine 不进入 identity。hook 进程不拥有 Integrator/Current；
sync 可以重写本地**同步快照文件集合**，
但不能修改已经存在 event 的 canonical bytes，也不能把业务 state 计算塞进 sync。

### 5. 唯一 Canonical Integrator CE（`Persistence/EventStore/CanonicalIntegrator.fs`）

```text
integrator {
    register JournalIntegration.rule
    register StrengthIntegration.rule
    register CasebookIntegration.rule
    register JsTransactionIntegration.rule
    register ...future business rules
}

boot:  history streams → k-way merge → integrateOne in canonical order → Current
live:  new EventEnvelope → same integrateOne; semantic failure may synthesize immediate durable cut reset
```

Integrator 拥有：history reader、k-way frontier、identity dedupe、parent/vocabulary structural validation、
Current、scoped faulted-rule tail 与 process-wide one-shot full-replay budget。注册 rule 接受“当前槽位 + 单个 EventEnvelope”，并提供纯 `FaultScope` oracle；Integrator 以 `(rule.Name, FaultScope envelope)` 作为 quarantine key。Journal 的 `FaultScope` = outer `EventStreamId`，其它当前 rule 仍使用 global scope。正常返回新槽位，语义拒绝时由 rule 的 `PlanCut` 推断最小 reset 参数，由 `ApplyCut` 解释该参数。Integrator 不保存 old-state snapshot；失败 event 不改 last-good Current，同 scope 后续 event 在 cut 前跳过，但其它 scope 继续积分。rule 不得读取文件、枚举历史、不得自己建立 replay loop。

`ProjectionCutTail` 是 authoritative EventEnvelope，不是日志：payload 至少含 `rule / failed_event_id / reason / reset_json`，和其它 writer fact 一起 sync。scope 不复制进 payload；replay 从 Integrator 已保存的 `failed_event_id → EventEnvelope` 推回同一个 `FaultScope`，只清除对应 quarantine key。live 发现新 semantic failure 时 bad fact 与 cut fact 同批持久化；replay 不预扫 cut index，并在**同 scope**内严格经历“bad → faulted tail → cut reset → continue”。这也保证旧 current-layout history 若存在缺 cut 的 Journal fault，只会冻结那个 Journal stream，不会把后来独立 session 的 lifecycle 从 Current 抹掉。如果 `PlanCut` 无法 O(1) 推断，Integrator 最多允许全进程一次 full-log replay 后重试推断。

### 6. Local files 与 remote Git 编码

```text
.git/wanxiang/events/<WriterId>.ndjson      // runtime truth; one process, one unbounded file
.git/wanxiang/payloads/<PayloadRef>          // local content-addressed large material

remote sync only:
  each <WriterId>.ndjson full bytes → exactly one Git blob
  each payload file                 → Git blob
  remote root                       → writer name / payload ref to blob OID
```

Wanxiangshu 不创建 event chunk/segment DAG，不维护 EventId→Git OID index，不自己做 delta。
Git pack/delta 是 Git 内部优化；sync 每次可以为增长后的 writer file 产生新 OID，旧 OID 不具有领域意义。

### 7. Journal / feature 适配（`Persistence/Journal/CodecSurface.fs`, `FactCodecSurface.fs`, `Surface.fs`）

```text
EventStoreJournalWriter（`Persistence/Journal/EventStoreJournalWriter.fs`）：只负责把 Journal Envelope 编成 universal EventEnvelope 并 append；
                         boot projection 直接读取 CanonicalIntegrator.Current.Journal；writer 生命周期为 Open → Poisoned/Closing → Closed，保留首个 physical failure；ReleaseAsync 关闭新准入并 drain 已准入 serial prefix
Strength/Casebook/JsTransaction：只生产 EventEnvelope、注册 Integration.rule、读取自己的 Current 槽位
AgentJournal.AppendEnvelope：local commit → Integrator integration；自己的 EventId 若出现在 cut receipt，则先 `FatalProcess.trip("journal-semantic-cut", ...)` 再返回 typed `FactRejected`（仅 node:test 能继续观察）；durable writer 不 poison，但生产当前进程不得继续 append/effect；writer 生命周期拒绝映射为 `WriterUnavailable`（known-not-attempted），不映射成 `WriteUnknown`
```

`Journal/CodecSurface.js` accepts a plain envelope descriptor and returns plain event/decode results;
`FactCodecSurface.js` keeps decoded facts internal and exposes only normalized bytes, case, and error text.
`Journal/Surface.js` returns an opaque `JournalHandle`; `appendAgent`, `appendManagerLifecycle`, payload
read/write, and projection snapshot return plain objects. Callers release the handle with `dispose`.

Journal 的 `payload_refs` 不再是空数组：`JournalPayloadClosure.ofFact`（`Persistence/Journal/EventStoreJournalWriter.fs`）
是唯一派生点，把 fact 中所有真实 sha256 content-address（`BlobRef`/`BlobDigest`）映射为 `PayloadRef`；
`MagicTodoFactCodec.payloadRefs` 对 `Fact.MagicTodo` 的 typed fact 做同样映射。非 content-address 的
占位值（如测试里的 `blob-1`/`digest:base`）不是 payload reference。append 前 closure 校验（
`Store.fs validatePayloadClosure`）因此对 Journal 真正生效：引用缺失的真实 payload → StorageInvalid。

任何 `loadEvents(raw,snapshot)` / `EventStoreMergeSpec.merge(...history...)` 出现在业务模块都属于结构性 RED。

## Business fold 不变量与 cut-tail（PERSIST-010）

不变量权威由 WHAT 015/021 承接；逐 fact 校验仍在 `Composition/Durable/Fold.fs` + 各 domain fold。区别是拒绝不再把 durable history 变成 unfoldable：functional reducer 在错误前没有修改 Current，Integrator 记录该 rule faulted，并由业务 rule 生成最小 reset 参数持久化 `ProjectionCutTail`。但 live `AgentJournal` 收到 cut receipt 后立即 trip process fatal；只有**下一进程** replay bad fact + cut/reset 后才可从 reset Current 继续。测试可屏蔽 kill 以检查 typed receipt，不代表生产允许 same-process continuation。

## 已知边界与相关实现

- `EventStoreJournalWriter` 的 `CommitUnknown` 与 `AgentJournal.JournalAppendFailure.WriteUnknown`
  只表示**物理 append 已进入、但返回失败后结局需要 durable witness 判定**；`WriterPoisoned` /
  `WriterClosing` / `WriterDisposed` 是 `NotAttempted → WriterUnavailable`，事件未进入 append boundary，
  属 known-not-committed。poison 必须保留首个 physical failure 文本，后续拒绝不得把首错降级为
  “poisoned or disposed”。**重试/reconcile 政策**（先核物理证据、禁盲重试）归 `effect-accounting`。
- merge 的并发/收敛面（k-way merge 代数、DomainConflict 表达、dumb remote）归
  `durable-convergence`；本文件只描述单一 store 内的 append/CAS/fold。
- 崩溃中的 tool 不自动重入；未完成 facts 保持坏历史。未来只有显式 `/continue` 可建立 session resume workflow，归 `crash-reconciliation`。

## 历史与弃权

1. **所有旧物理布局 —— shock cutover / migration absence**：pre-unified `.git/wanxiangshu-next`、
   RuntimePath blobs、Student QA 私有文件，以及 unified-store 曾经写出的 `events/<hex>/<EventId>.jsonl`
   都不迁移、不双读、不识别 shape、不 reset/CAS。旧布局完全 leave-unread；这个切换明确丢历史。
2. **`logs/<ReplicaId>/<segment>.ndjson` + `index/` —— GARBAGE**：这是本次性能根因对应的在线 Git
   物理布局；新实现不读、不迁、不双写。新 runtime truth 只有 `.git/wanxiang/events/<WriterId>.ndjson`。
3. **`CommitUnknown → 永久无法确定` —— 弃权**：storage.md §9 重审为「canonical root 即 durable
   witness」，被 WHAT 005/006 取代。
4. **migration 的 bytes 级确定性 —— HOW/迁移期**：`LegacyProjection == NewProjection` 且
   EventId/parents 排序/canonical bytes/closure/root OID 全确定才算迁移完成；这是
   one-shot 迁移工具的义务，不进入 runtime（`no-migrator`）。
5. **快照/进程模型（storage.md §10.1/10.2）—— HOW**：「Process 是 Replica、Snapshot 是冻结
   观察、不建 process registry」是并发模型；正向 replica 收敛律归 `durable-convergence`。
6. **`schemaVersion` 例外站点 —— HOW**：`NON_STORE_SCHEMA_VERSION_SITES`（HandleCompletionCodec
   等）是产品发布面字段，不是 durable store 版本；unified-store-gate 只拦 store 上下文。
7. **FactCodec 历史输入诊断 —— decode-only bounded compat（CLN-07 census / CLN-08..N 裁决）**：
   当前 writer 只产生完整的 canonical facts；已删除缺字段注入与旧 observation-tag
   rewrite。保留的历史路径只在 persistence ingress 对已知旧 bytes 做**拒绝并给出明确处置
   信息**，绝不把旧 bytes 迁移为新 facts，也没有旧 writer。census 2026-08-16：
   retention-horizon 证据不得手抄 journal 数；使用 tracked `scripts/checks/legacy-horizon-census.mjs --roots-file <inventory>` 对显式 inventory 重放。inventory 为空、声明 workspace 缺失、events 目录/NDJSON 读取失败均 fail closed；输出包含 workspace/journal/line 数、四类 detector counts 与 roots digest。历史 dated census 只作为当时 inventory 的证据，不代表“当前所有受支持 workspace”。

   | decode-only ingress | 真实债权人 | Exit condition |
   |---|---|---|
   | `containsLegacyFallbackFields` → `pre050MigrationMessage` | 仍可能被操作者提交的 pre-0.5.0 runtime journal；需要可操作的 archive-or-remove 诊断 | retention horizon + 外部 workspace census 证明无 pre-0.5.0 bytes → 删除检测与诊断测试 |
   | `containsLegacyScoreVectorEntry` → `tipV2CleanBreakMessage` | 历史 tip-v1 observation/entry bytes；不能无损猜成单一 tip | 所有受支持 workspace 完成 tip-v2 clean break 且无旧 bytes → 删除检测与诊断测试 |
   | `containsLegacyUnanchoredGuideline` → `legacyGuidelineCleanBreakMessage` | 历史未锚定 guideline bytes；ordinal 无法恢复 transcript position | HOST-013 retention horizon + census 无旧 bytes → 删除检测与诊断测试 |
   | `containsHandleCompletedMissingCompletionFields` → explicit refusal | 历史缺少 `CompletionRef` / `CompletionDigest` 的 `HandleCompleted` bytes；完成身份无法安全重建 | EXEC-009 retention horizon + census 无旧 bytes → 删除检测与诊断测试 |

   **禁止**：把任何 decode-only refusal 升级为双向 adapter、old writer、migrator 或 fallback-to-old-store shim。

## 验证与测试落点

> 2026-08-14 shock cut。新/改写 oracle 按用户要求 **FROZEN，未执行**；本文件记录可红落点，
> 不声称当前测试结果。旧 online-Git EventStore（segment/index/OpenSnapshot/CAS）的 proof 已废弃。

### 运行方式（解冻后）

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

### 命题 → 落点

| 命题 | 落点测试 | 类型 |
|---|---|---|
| DURABLE-EVENTS-001 | `tests/append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-001] append_only_prior_writer_bytes_are_a_strict_prefix_after_new_fact` + `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-001] append_commits_complete_canonical_line_then_updates_Current` | NEW/FROZEN |
| DURABLE-EVENTS-002 | `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-002] PERSIST_001_an_envelope_serializes_to_exactly_one_line` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] EventType_is_exactly_JournalEnvelope` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] encode_preserves_EventId` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] encodeStreamId_scheme_is_stable_and_deterministic` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] round_trip_preserves_fold_relevant_fields` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] round_trip_fold_equates_with_journal_fold` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] tryDecode_rejects_wrong_EventType` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-002] workspace_child_process_streams_round_trip` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-002] handle_completed_with_completion_fields_round_trips_canonically` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-002] handle_completed_missing_completion_fields_is_rejected_without_decode_migration` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-002] malformed_completion_and_ownership_labels_fail_closed` + `tests/host-turn-observed.test.mjs::WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_serializes_round_trip_with_provider_run` + `tests/host-turn-observed.test.mjs::WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_serializes_round_trip_without_provider_run` + `tests/host-turn-observed.test.mjs::WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_fold_is_noop_on_agent_projection` + `tests/host-turn-observed.test.mjs::WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_identity_key_is_session_plus_provider_run` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-002] fixture unified-store-schema-version.fs is RED for schema-version-in-store-context` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-002] schemaVersion without store context is not flagged (host/authored allow)` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-002] always-forbidden store version tokens are RED without extra context` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-002] production scan keeps store context free of version tokens` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-002] documented non-store schemaVersion sites remain unflagged in production text` | REUSE + NEW/FROZEN |
| DURABLE-EVENTS-003 | `tests/event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-003] same_EventId_different_canonical_bytes_fail_closed` + `tests/event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-003] same_EventId_same_canonical_bytes_dedupe_ok` + `tests/event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-003] canonical_bytes_are_utf8_json_plus_single_LF_with_sorted_keys` + `tests/event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-003] distinct_EventIds_are_both_retained` + `tests/event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-003] DURABLE_EVENTS_003_same_EventId_same_bytes_dedupes` + `tests/event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-003] DURABLE_EVENTS_003_same_EventId_different_bytes_fail_closed` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-003] parents_are_accepted_and_canonicalized` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-003] payloadRefs_are_accepted_and_canonicalized_without_RuntimePath_IO` + `tests/event-store-journal-codec.test.mjs::WHAT[DURABLE-EVENTS-003] canonical_identity_bytes_stable_under_section_5_0` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_serialization_is_deterministic_for_one_envelope` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_an_absent_provider_run_is_omitted_rather_than_written_null` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_an_envelope_survives_a_round_trip_unchanged` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_serialized_bytes_do_not_depend_on_the_writers_utc_offset` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_parents_and_payload_refs_are_canonicalized_at_the_codec_boundary` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_runtime_started_pins_offset_on_serialize_and_deserialize` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-003] PERSIST_001_handle_abandoned_pins_abandoned_at_offset` + `tests/misc-codecs-canonical-json.test.mjs::WHAT[DURABLE-EVENTS-003] MISC_canonical_json_sorts_keys_recursively` + `tests/misc-codecs-canonical-json.test.mjs::WHAT[DURABLE-EVENTS-003] MISC_canonical_json_equal_ignores_key_order` + `tests/misc-codecs-canonical-json.test.mjs::WHAT[DURABLE-EVENTS-003] MISC_without_keys_drops_named_fields_only` + `tests/integration/persist/object-identity.test.mjs::WHAT[DURABLE-EVENTS-003] canonical_event_bytes_are_stable_under_object_and_set_order` + `tests/integration/persist/object-identity.test.mjs::WHAT[DURABLE-EVENTS-003] same_event_id_different_canonical_bytes_is_identity_collision` + `tests/integration/persist/object-identity.test.mjs::WHAT[DURABLE-EVENTS-003] canonical_event_bytes_decode_to_the_same_plain_event` + `tests/integration/persist/object-identity.test.mjs::WHAT[DURABLE-EVENTS-003] merge_by_identity_dedupes_equal_bytes_and_rejects_collisions` | REUSE + NEW/FROZEN |
| DURABLE-EVENTS-004 | `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-004] append_task_does_not_return_until_the_cross_process_store_lock_is_released` | NEW/FROZEN |
| DURABLE-EVENTS-005 | `tests/local-process-event-log.test.mjs::WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments` + `tests/local-process-event-log.test.mjs::WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity` + `tests/append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-005] one_writer_is_one_file_regardless_of_history_size` | NEW/FROZEN |
| DURABLE-EVENTS-006 | `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-006] append_adds_one_local_line_and_Current_is_already_integrated` + `tests/append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-006] duplicate_same_identity_is_idempotent_but_collision_is_rejected`；交叉 `requirements/verification-system/tests/temporal-harness.test.mjs` `journal release drains accepted append prefix and rejects later admission` / `journal poison preserves the first physical failure and stops storage traffic`（真实 Task race：NotAttempted ≠ CommitUnknown、release drain、首错保真） | NEW/FROZEN + CROSS |
| DURABLE-EVENTS-007 | `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-007] append_rejects_missing_parent_without_writing_bytes` + `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-007] append_rejects_cycle_in_one_batch_before_durability` + `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-007] append_rejects_unknown_event_type_fail_closed` + `tests/event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_k_way_merge_rejects_missing_parent_fail_closed` + `tests/event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_k_way_merge_rejects_backward_or_cyclic_writer_frontier` + `tests/event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_007_unknown_authoritative_event_type_is_rejected_before_durability` + `tests/event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_missing_parent_fails_closed` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-007] PERSIST_005_malformed_json_is_an_error_value_not_an_exception` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-007] PERSIST_005_unparseable_json_is_a_decode_error_not_a_throw` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-007] PERSIST_005_unknown_case_is_a_decode_error` | NEW/FROZEN |
| DURABLE-EVENTS-008 | `tests/event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_concurrent_heads_remain_distinct_in_structural_Current` + `tests/event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_resolution_naming_all_heads_collapses_structural_Current` + `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` (cross-owner DomainConflict frontier) | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-EVENTS-009 | `tests/integration/persist/leave-unread.test.mjs::WHAT[DURABLE-EVENTS-009] local_EventStore_never_reads_or_rewrites_any_legacy_layout` + `tests/integration/persist/leave-unread.test.mjs::WHAT[DURABLE-EVENTS-009] shock_cut_source_has_no_legacy_shape_detection_migration_or_reset` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-009] PERSIST_005_legacy_fallback_counters_and_model_ids_are_fatal` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-009] PERSIST_005_replaced_fact_names_produce_the_migration_message_not_a_codec_error` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-009] PERSIST_005_the_migration_message_tells_the_operator_what_to_do` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-009] PERSIST_005_a_current_fact_is_not_mistaken_for_a_legacy_one` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-009] PERSIST_005_modern_json_has_no_legacy_markers` + `tests/fact-codec.test.mjs::WHAT[DURABLE-EVENTS-009] historical_unanchored_guideline_is_refused_without_rewrite` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] fixture unified-store-student-qa-revival.fs is RED for student-qa-revival` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] fixture unified-store-no-migrator.mjs is RED for no-migrator` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] synthetic LegacyProjection≡NewProjection claim is RED for no-migrator` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] fixture unified-store-dual-write.fs is RED for dual-write` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] Journal-only or EventStore-only modules are not dual-write` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] dual-write allowlist is empty (no parked bridges)` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] e2e journal observers that only read wanxiangshu-next are not no-migrator` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-009] production scan has no legacy dual-write migrator or student-qa residue` + `tests/workspace-event-store-host.test.mjs::WHAT[DURABLE-EVENTS-009] SharedAgentJournal_cache_hit_returns_same_instance_without_rereading_retired_path` | NEW/FROZEN |
| DURABLE-EVENTS-010 | `tests/workspace-event-store-host.test.mjs::WHAT[DURABLE-EVENTS-010] SharedAgentJournal_boots_local_EventStore_and_leaves_retired_RuntimePath_ndjson_unread`（boot via `JournalSurface.acquireSharedForWorkspace` + real `appendAgent` CompanionBloggerClosed → `.git/wanxiang/events/*.ndjson`；retired RuntimePath poison bytes byte-for-byte 未读未改） | NEW/FROZEN |
| DURABLE-EVENTS-011 | `tests/local-process-event-log.test.mjs::WHAT[DURABLE-EVENTS-011] one_complete_writer_file_is_one_blob_only_at_remote_sync_boundary` + `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` (cross-owner complete-writer-file blobification) | NEW + CROSS/FROZEN |
| DURABLE-EVENTS-012 | `tests/journal-payload-closure.test.mjs::WHAT[DURABLE-EVENTS-012] closure_lifts_a_content_addressed_digest_into_payload_refs` + `tests/journal-payload-closure.test.mjs::WHAT[DURABLE-EVENTS-012] closure_dedupes_a_matching_blob_ref_and_digest_pair` + `tests/journal-payload-closure.test.mjs::WHAT[DURABLE-EVENTS-012] closure_ignores_non_content_addressed_placeholder_handles` + `tests/journal-payload-closure.test.mjs::WHAT[DURABLE-EVENTS-012] closure_is_empty_for_a_fact_without_blob_fields` + `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-012] BlobWriter_uses_local_content_addressed_payloads_not_workspace_blobs_or_Git_ODB` + `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-012] appended_fact_lifts_real_blob_digest_into_persisted_payload_refs` + `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-012] closure_fails_closed_when_a_real_content_address_is_missing` + `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-012] journal_writer_source_has_no_snapshot_CAS_or_Git_raw_store` + `requirements/speculative-investigation/tests/store.test.mjs` (cross-owner local payload closure) | NEW + CROSS |
| DURABLE-EVENTS-013 | `tests/canonical-integrator.test.mjs::WHAT[DURABLE-EVENTS-013] DURABLE_EVENTS_013_boot_and_live_share_the_same_single_event_integration_program` + `tests/event-store-journal-boot.test.mjs::WHAT[DURABLE-EVENTS-013] restart_replays_prior_writer_files_then_fresh_runtime_starts_LocalSeq_at_1` + `tests/event-store-journal-boot.test.mjs::WHAT[DURABLE-EVENTS-013] boot_and_live_use_one_CanonicalIntegrator_program` + `tests/journal-subscription.test.mjs::WHAT[DURABLE-EVENTS-013] EXEC_journal_revision_advances_only_on_successful_fold` + `tests/journal-subscription.test.mjs::WHAT[DURABLE-EVENTS-013] EXEC_AwaitChangeFrom_after_append_returns_promptly` + `tests/journal-subscription.test.mjs::WHAT[DURABLE-EVENTS-013] EXEC_AwaitChangeFrom_before_append_waits_then_completes` + `tests/session-association-keyed-lookup.test.mjs::WHAT[DURABLE-EVENTS-013] PERSIST_008_both_directions_answer_from_one_map_without_a_scan` + `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-013] journal_surface_does_not_mint_terminal_proof_from_forged_strings` | NEW/FROZEN |
| DURABLE-EVENTS-014 | `tests/event-store-merge.test.mjs::WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_writer_enumeration_independent` + `tests/event-store-fold.test.mjs::WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_deterministic_with_EventId_tiebreak` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-014] PERSIST_001_ordering_is_by_local_seq_inside_a_runtime_and_by_time_across` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-014] PERSIST_001_same_instant_across_runtimes_breaks_the_tie_by_runtime_id` + `tests/envelope.test.mjs::WHAT[DURABLE-EVENTS-014] PERSIST_001_k_way_merge_is_a_total_order_regardless_of_input_order` + `requirements/durable-convergence/tests/replica-merge-laws.test.mjs` (cross-owner deterministic convergence) | NEW/FROZEN + CROSS/FROZEN |
| DURABLE-EVENTS-015 | `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] PERSIST_010_entry_and_squash_fold_into_the_blog_projection` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] CTX_012_rebase_folds_into_the_prefix_projection_only` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] PERSIST_010_a_stale_frame_epoch_fails_the_fold_closed` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] CTX_012_a_replayed_rebase_is_absorbed_so_crash_recovery_is_idempotent` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] CTX_011_a_not_new_candidate_is_absorbed_by_the_fold` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] PERSIST_010_a_non_sequential_prefix_epoch_fails_the_fold_closed` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] CTX_011_a_retreating_cutoff_fails_the_fold_closed` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] HOST_006_reanchor_retires_the_prefix_and_zeroes_prefix_coverage_in_one_fact` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] HOST_006_a_replayed_reanchor_leaves_rebuilt_coverage_alone` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] HOST_006_coverage_and_probes_both_recover_after_a_reanchor` + `tests/fold-context-recovery.test.mjs::WHAT[DURABLE-EVENTS-015] PERSIST_010_context_recovery_facts_survive_NDJSON_and_still_fold` | REUSE |
| DURABLE-EVENTS-016 | `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] scanner ids cover Phase 1–3 and P4U2 clean-break rules` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] fixture unified-store-feature-ref.fs is RED for feature-ref` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] fixture unified-store-git-bypass.fs is RED for git-bypass` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] canonical refs/wanxiang/store is allowed only under Persist/Git ownership` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] owner remote-tracking store ref is allowed; other feature refs stay RED` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] git-bypass allowlist is empty; only Persist/Git ownership may invoke git` + `tests/unified-store-gate.test.mjs::WHAT[DURABLE-EVENTS-016] production scan is GREEN under gate rules (empty git-bypass allowlist)` + `tests/event-store-identity-collision.test.mjs::WHAT[DURABLE-EVENTS-016] StoreTypes_exposes_canonical_store_ref` | REUSE |
| DURABLE-EVENTS-017 | `tests/local-process-event-log.test.mjs::WHAT[DURABLE-EVENTS-017] DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies` + `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-017] append_cost_contract_is_independent_of_history_and_EventId_distribution` + `tests/append-only-laws.test.mjs::WHAT[DURABLE-EVENTS-017] append_path_has_no_Git_object_or_ref_capability` | NEW/FROZEN |
| DURABLE-EVENTS-018 | `tests/hook-dispatcher.test.mjs::WHAT[DURABLE-EVENTS-018] HOOK_activation_ensure_installs_both_hooks_and_remote_fetch_refspec_without_running_sync` + `tests/hook-dispatcher.test.mjs::WHAT[DURABLE-EVENTS-018] HOOK_shim_resolves_node_from_environment_not_installer_host_execPath` + `tests/hook-dispatcher.test.mjs::WHAT[DURABLE-EVENTS-018] HOOK_reference_transaction_and_pre_push_launch_the_same_independent_full_converge_runtime` + `tests/hook-dispatcher.test.mjs::WHAT[DURABLE-EVENTS-018] HOOK_classification_preserves_foreign_hooks` + `tests/hook-dispatcher.test.mjs::WHAT[DURABLE-EVENTS-018] HOOK_install_refreshes_owned_hook_but_never_overwrites_foreign_hook` + `requirements/durable-convergence/tests/writer-stream-sync.test.mjs` (cross-owner hook transport) | NEW/FROZEN |
| DURABLE-EVENTS-019 | `tests/canonical-integrator.test.mjs::WHAT[DURABLE-EVENTS-019] DURABLE_EVENTS_019_canonical_integrator_is_an_FSharp_CE_with_registered_business_rules` + `tests/canonical-integrator.test.mjs::WHAT[DURABLE-EVENTS-019] DURABLE_EVENTS_013_019_business_modules_do_not_own_history_read_or_replay_loops` + `tests/canonical-integrator.test.mjs::WHAT[DURABLE-EVENTS-019] DURABLE_EVENTS_019_only_CanonicalIntegrator_may_derive_Current_from_event_history` | NEW/FROZEN |
| DURABLE-EVENTS-020 | `tests/event-store-journal-boot.test.mjs::WHAT[DURABLE-EVENTS-020] empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation` + `tests/event-store-journal-writer.test.mjs::WHAT[DURABLE-EVENTS-020] create_is_read_only_until_the_first_business_append` + `requirements/host-boundary/tests/plugin-load-purity.test.mjs` (cross-owner load purity) | NEW + CROSS |
| DURABLE-EVENTS-021 | `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-021] semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next` + `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-021] an uncut historical Journal fault suppresses only its own journal stream` + `tests/event-store-append.test.mjs::WHAT[DURABLE-EVENTS-021] every_live_semantic_cut_boundary_trips_process_fatal_instead_of_returning_a_normal_error` + `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` (cross-owner fatal semantic-cut boundary) | NEW + CROSS |

### GAP

- `GAP-013` —— **CLOSED**：production append 已切为 `.git/wanxiang/events/<WriterId>.ndjson`；Git blob/tree/ref 只在独立 remote-hook sync；一 writer 文件一 blob；旧 segment/index/CAS 实现已移出编译图并标 GARBAGE。落点：`local-process-event-log.test.mjs`、`event-store-append.test.mjs`、`requirements/durable-convergence/tests/writer-stream-sync.test.mjs`（均 FROZEN 未执行）。
- `GAP-014` —— **CLOSED**：`CanonicalIntegrator` 是唯一 history enumerator，以 F# `IntegratorBuilder` CE 注册 Structural/Journal/Strength/Casebook/JsTransaction 单-event oracle；business modules 已无 `loadEvents`/history project API。落点：`canonical-integrator.test.mjs` + feature Current tests（FROZEN 未执行）。

### 统计

- WHAT 命题：21；PROOF 行：21。
- 本包 GAP：0（GAP-013 / GAP-014 已关闭；测试仍按用户要求冻结，未执行）。
