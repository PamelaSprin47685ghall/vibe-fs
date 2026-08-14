# HOW —— durable-events（实现模型与约束）

> 本文件**非 normative**。行为合同在 `WHAT.md`；本文件回答「代码在哪里、怎么工作」，
> 并收纳历史与弃权裁决。

## 生产装配链

```text
ProcessGitRawStore(commonDir)             // Infrastructure/Persist/ProcessGitRawStore.fs
  → EventStore.create / createWithRetries(+Converge)   // Infrastructure/Persist/EventStore.fs
  → WorkspaceEventStore.acquire（process-local refcount）
  → IJournalEventStoreBoot.ResumeOrCreate / EventStoreJournalWriter
  → AgentJournal.createFromEventStore | createFromProjection
```

Git 传输唯一入口是 `Infrastructure/Git/GitGateway.fs`（Fetch/Pull/Push/ConvergeStore）。
`HookDispatcher` 提供 `reference-transaction` / `pre-push` shim + recursion guard
（`WANXIANG_GIT_SYNC_ACTIVE`），hook 自身不 fetch/merge，只把「store ref 变化」转成收敛机会。

## 分层与所有权（shape/persist.md PERSIST-006 / storage.md §45）

| 层 | 拥有 | 不得拥有 |
|---|---|---|
| Domain | `EventEnvelope` / `PayloadRef` / 因果与业务语义 | `GitObjectId` / `RootOid` / `StoreSnapshot` / `AppendCandidate` / Git 操作 |
| Persist | canonical JSON、CAS publish、`StoreSnapshot`、merge/fold、payload closure | 领域 event vocabulary、feature ref |
| GitGateway / HookDispatcher | Wanxiang-initiated Git transport、store converge、hook shim | Domain reducer、第二套 merge 运行时 |
| AgentJournal | 应用侧 journal 适配表面（append fact / fold projection） | 平行 NDJSON/Blob 后端、独立 canonical ref |

红线：`src/Wanxiangshu/Domain/EventStore.fs` 不引用 `Infrastructure`；`StoreSnapshot`/
`AppendCandidate`/`RootOid`/`GitObjectId` 定义只在 `Infrastructure/Persist`。

## 核心机制（逐概念）

### 1. Canonical JSON（`CanonicalEventCodec.fs`）

```text
normalizeJson：递归 Object.keys 排序
envelopeObject：event_id / stream_id / event_type / parents / payload / payload_refs（固定六键）
encode：JSON.stringify(normalizeJson) + "\n"        // 恰好一个 LF
checkIdentity：同 id 同 bytes → Ok；同 id 异 bytes → StorageInvalid.IdentityCollision
mergeByIdentity：set-union by EventId，输出按 EventId 排序
tryDecode：null/非单 LF/形状错/重编码不等 → StorageInvalid（NonCanonical | MalformedEnvelope）
```

### 2. Store 类型（`StoreTypes.fs`）

```text
GitObjectId / RootOid / StoreSnapshot { RootOid } / AppendCandidate { BaseSnapshot; NewEvents; NewPayloads }
MergeInput = StoreSnapshot list；TreeEntry = { Mode; Name; Oid }
StoreRef.canonical = "refs/wanxiang/store"；StoreRef.remoteTracking = "refs/wanxiang/remotes/<remote>/store"
StorageInvalid = IdentityCollision | NonCanonical | MalformedEnvelope | MissingParent
              | CyclicParents | MissingPayload | UnknownEventType     // 全局 fail-closed
DomainConflict = ConcurrentHeads of streamId * heads                  // 永不升级
AppendError = StorageInvalid | AppendCasRejected | AppendRetryExhausted
PublishError = ... | IncompletePayloadClosure；ConvergeError = StorageInvalid | Transport | ...
```

### 3. Append / Publish（`EventStore.fs`）

```text
validateAppendSet：O(|new|) against tip（mergeByIdentity → vocabulary → checkIdentity → parents
                   → intra-batch DAG），不重读全历史
append：validate → materialize delta + structural merge → CompareAndSwapRef(canonical, expected, newOid)
        CAS false → AppendCasRejected / 重读 tip：eventsAlreadyCommitted → Ok current（幂等）
        否则基于新 tip 重建 → bounded retry（DefaultMaxRetries = 8）
publish：先写 payload blobs（OID 失配 → IncompletePayloadClosure），PayloadClosure.validatePresent，
         再走 append —— payload 先于引用它的 event 进入 ODB
```

### 4. Merge（`EventStoreMerge.fs`）

```text
EventStoreMergeSpec（契约 oracle）：mergeEvents = mergeByIdentity（set union + identity dedupe）
EventStoreMerge（生产）：structural tree merge——不同 EventId 路径直接 union；
        同 EventId 且 blob OID 相同 → 复用；OID 不同 → 读 bytes 校验 IdentityCollision；
        其它路径冲突 → NonCanonical。复杂度与 delta/tree-path 相关，非 O(N) 全量。
```

### 5. Fold（`EventStoreFold.fs`）

```text
AuthoritativeEventTypes：store 层合法 vocabulary（JournalEnvelope、Job*、JsTransaction*、
        InspectorCase*、Strength*……）；isResolution = EndsWith("ConflictResolved"|"Resolved")
StreamHeadState = Empty | Unique of head | Conflict of DomainConflict
topologicalOrder：Kahn + EventId 字典序 tie-break；缺 parent/成环 → StorageInvalid
applyStream：resolution event（parents 覆盖全部 prior heads）→ 收敛为单一 head
fold：validateVocabulary → validateParents → topologicalOrder → applyStream → { Streams; FoldOrder; Conflicts }
```

### 6. Git raw 物理层（`GitRawStore.fs` / `ProcessGitRawStore.fs` / `GitObjectDatabase.fs`）

```text
EventIdShard：PrefixLength=2、Extension=".jsonl"、布局 events/<2-hex>/<EventId>.jsonl
StoreTree：events/ + payloads/；canonicalOrder（目录按 name + "/" 排序）
PayloadClosure：§7.1 —— root payloads/ == ⋃ events.payload_refs；dangling → MissingPayload；
                 extras → NonCanonical
ProcessGitRawStore：memoized 对象/树缓存（content-addressed 使 memoization 精确；缺席不缓存）
CompareAndSwapRef：lockfile CAS（<ref>.lock via wx → 验证 current → rename），None 用 zeroOid
loadEventEnvelopes：一次 events/ 树遍历 + 每 blob 一次读取，O(|events|)；路径 EventId 与
                    envelope 不一致 → NonCanonical
```

### 7. Journal 适配（`Journal/EventStoreJournal{Codec,Writer}.fs`、`AgentJournal.fs`）

```text
EventStoreJournalCodec：JournalEnvelopeEventType = "JournalEnvelope"；encodeStreamId =
        journal/workspace | journal/session/<id> | journal/child/<id> | journal/process/<id>
EventStoreJournalWriter：filePath = ""（成功路径无 NDJSON）；serialized append；
        commitEnvelope 的 parents = [lastByStream[key]]；poisoned → 后续 Append 拒绝
AgentJournal.AppendEnvelope：commit → fold；fold 拒绝 → poison + FactRejected（line 保持 durable）；
        写失败 → JournalAppendFailure.WriteUnknown —— 结局未知，runtime 必须 reconcile
        （outcome-unknown 语义归 effect-accounting）
Journal/Fold.fs apply：PERSIST-004 —— 第一个不可能的行即停，不产生 writer 不可能产生的部分重放
```

## 恢复 fold 不变量（PERSIST-010）的实现落点

不变量权威定义在 `docs/what/persist.md` PERSIST-010（迁移后由本包 WHAT 015 承接）；
逐 fact 校验在 `Journal/Fold.fs` 恢复事实分支 + 各 domain fold（`CompanionFactFold`/
`ContextFactFold`/`BlogProjection`/`PrefixEpochProjection`/`XTraceProjection` 等）。
物理 event 形状见 WHAT 002/004；Journal 行经 codec 进入 EventStore。

## 已知边界与相关实现

- `EventStoreJournalWriter` 的 `CommitUnknown` 与 `AgentJournal.JournalAppendFailure.WriteUnknown`
  是「结局未知」的机械面；**重试/reconcile 政策**（先核物理证据、禁盲重试）归
  `effect-accounting`。
- merge 的并发/收敛面（k-way merge 代数、DomainConflict 表达、dumb remote）归
  `durable-convergence`；本文件只描述单一 store 内的 append/CAS/fold。
- 崩溃后从 durable facts 重入普通程序归 `crash-reconciliation`（`ResumeOrCreate` 是它消费的入口）。

## 历史与弃权

1. **旧 NDJSON journal / RuntimePath `blobs/` / Student QA 私有文件 —— GARBAGE（migration absence）**：
   `PERSIST-011`（Student QA）编号永久空缺；`unified-store-gate` 的 `student-qa-revival` /
   `no-migrator` / `dual-write` 扫描 + `leave-unread` 测试钉死「不读、不迁、不双写」。
   已删除生产 `Boot.fs`、NDJSON `JournalWriter`、目录 `BlobWriter`、`AgentJournal.createFromBoot`。
2. **`events/<hex-prefix>/<EventId>.jsonl` 分片路径 —— HOW**：物理布局，非领域事实。
3. **`CommitUnknown → 永久无法确定` —— 弃权**：storage.md §9 重审为「canonical root 即 durable
   witness」，被 WHAT 005/006 取代。
4. **migration 的 bytes 级确定性 —— HOW/迁移期**：`LegacyProjection == NewProjection` 且
   EventId/parents 排序/canonical bytes/closure/root OID 全确定才算迁移完成；这是
   one-shot 迁移工具的义务，不进入 runtime（`no-migrator`）。
5. **快照/进程模型（storage.md §10.1/10.2）—— HOW**：「Process 是 Replica、Snapshot 是冻结
   观察、不建 process registry」是并发模型；正向 replica 收敛律归 `durable-convergence`。
6. **`schemaVersion` 例外站点 —— HOW**：`NON_STORE_SCHEMA_VERSION_SITES`（HandleCompletionCodec
   等）是产品发布面字段，不是 durable store 版本；unified-store-gate 只拦 store 上下文。
