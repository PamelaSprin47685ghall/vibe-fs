# HOW —— durable-events（实现模型与约束）

> 本文件**非 normative**。行为合同在 `WHAT.md`；本文件回答「代码在哪里、怎么工作」，
> 并收纳历史与弃权裁决。

## 生产装配链

```text
ProcessEventLog(commonDir, WriterId)         // Infrastructure/Persist/ProcessEventLog.fs
  → .git/wanxiang/events/<WriterId>.ndjson   // one process = one file; never sliced
  → CanonicalIntegrator CE                   // the only history consumer
  → EventStore.createLocal                   // append + payload closure + Current
  → WorkspaceEventStore.acquire（process-local refcount）
  → IJournalEventStoreBoot.ResumeOrCreate / EventStoreJournalWriter
  → AgentJournal.createFromEventStore | createFromProjection

Wanxiangshu/OpenCode startup
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

运行时 append/replay 不调用 Git object API。`HookDispatcher` 只在 startup ensure hook/refspec；
`HookSync` 才是独立 Git hook 子进程的同步入口，并以 `WANXIANG_GIT_SYNC_ACTIVE` 防递归。

## 分层与所有权（shape/persist.md PERSIST-006 / storage.md §45）

| 层 | 拥有 | 不得拥有 |
|---|---|---|
| Domain | `EventEnvelope` / `PayloadRef` / 因果与业务语义 | `GitObjectId` / `RootOid` / `StoreSnapshot` / Git 操作 |
| Persist | canonical JSON、本地 process NDJSON、payload closure、k-way input、唯一 `CanonicalIntegrator` CE、sync materialization | 领域业务判断、feature-owned history reader、按 domain 拆 backend |
| HookDispatcher / HookSync / GitGateway | startup hook/refspec ensure；独立 Git-hook 进程 transport、writer-file blobification、remote snapshot replace | 产品进程主动 fetch/pull/push、Domain reducer、后台同步状态机、第二套业务积分器 |
| AgentJournal / Strength / Casebook / JsTransaction | EventEnvelope producer + 向 Integrator 注册单-event rule + 读取 Current | 历史枚举、独立 replay/fold/project loop、physical journal/backend |

红线：`src/Wanxiangshu/Domain/EventStore.fs` 不引用 `Infrastructure`；Git object identity 只存在于
sync membrane；业务模块不能取得 `IEventHistoryReader`/本地 writer 文件路径。

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

### 2. Store / local-log 类型（`StoreTypes.fs` / `ProcessEventLog.fs`）

```text
WriterId：process-start fresh globally-unique physical writer identity
LocalFrontier：每个 WriterId 当前完整 byte length / last EventId（仅 Integrator/Persist 可见）
StoreSnapshot：仅 remote-sync membrane 的 Git root snapshot；不是本地 Current/frontier witness
StorageInvalid = IdentityCollision | NonCanonical | MalformedEnvelope | MissingParent
              | CyclicParents | MissingPayload | UnknownEventType
DomainConflict = ConcurrentHeads of streamId * heads
GitObjectId / RootOid / StoreRef：只在 remote sync materialization/transport 使用
```

### 3. Local Append / Publish（`EventStore.fs` / `ProcessEventLog.fs`）

```text
createLocal：生成 fresh WriterId；打开 .git/wanxiang/events/<WriterId>.ndjson（append-only）
boot：CanonicalIntegrator 独占读取 .git/wanxiang/events/*.ndjson → k-way merge → Current
validateAppendSet：只向 Integrator 查询 Current/indexed identity/parents；不自行扫描历史
append：acquire cross-process `.git/wanxiang` physical gate
        → canonical encode 新 events → append complete JSON+LF lines → durability barrier
        → 同一批 events 交给 CanonicalIntegrator.integrateLive → 更新 Current/frontier → release gate
publish：先在同一 physical gate 下把新增 payload bytes 写 .git/wanxiang/payloads/<PayloadRef>；
        event append 再独立取得 gate、验证 closure 后提交
```

同一 gate 也由 standalone Git-hook sync 在整个 local snapshot/import + remote publication 窗口持有，
所以运行中的 writer 与外部 `git fetch/pull/push` 不会互相观察半截文件；它只是跨进程物理资源锁，
不表达业务 stage/Current，也不构成状态机。运行时等待该物理资源不使用业务 10s watchdog。

没有 `SegmentMaxBytes`、rotation、tail rewrite、EventId→blob index、Git tree/CAS retry。
`WriterId.ndjson` 从进程开始一直增长到进程退出；新进程只创建新 WriterId。

### 4. K-way merge / Sync（`EventKWayMerge.fs` / `CanonicalIntegrator.fs` / 独立 hook）

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

### 5. 唯一 Canonical Integrator CE（`CanonicalIntegrator.fs`）

```text
integrator {
    register JournalIntegration.rule
    register StrengthIntegration.rule
    register CasebookIntegration.rule
    register JsTransactionIntegration.rule
    register ...future business rules
}

boot:  history streams → k-way merge → Integrator CE → Current
live:  newly committed EventEnvelope     → same Integrator CE → Current'
```

Integrator 拥有：history reader、k-way frontier、identity dedupe、parent/vocabulary structural validation、
Current。注册 rule 只接受“当前槽位 + 单个 EventEnvelope”，返回更新后的槽位/拒绝；rule 不得读取文件、
不得枚举历史、不得自己建立 replay loop。`Composition/Durable/Fold.fs` 的各 domain fold 变成 Journal rule 内部的
单-event integration primitives，而不是另一个 history owner。

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

### 7. Journal / feature 适配

```text
EventStoreJournalWriter：只负责把 Journal Envelope 编成 universal EventEnvelope 并 append；
                         boot projection 直接读取 CanonicalIntegrator.Current.Journal
Strength/Casebook/JsTransaction：只生产 EventEnvelope、注册 Integration.rule、读取自己的 Current 槽位
AgentJournal.AppendEnvelope：local commit → Integrator live integration；不额外 fold history
```

Journal 的 `payload_refs` 不再是空数组：`JournalPayloadClosure.ofFact`（EventStoreJournalWriter.fs）
是唯一派生点，把 fact 中所有真实 sha256 content-address（`BlobRef`/`BlobDigest`）映射为 `PayloadRef`；
`MagicTodoFactCodec.payloadRefs` 对 `Fact.MagicTodo` 的 typed fact 做同样映射。非 content-address 的
占位值（如测试里的 `blob-1`/`digest:base`）不是 payload reference。append 前 closure 校验（
`Store.fs validatePayloadClosure`）因此对 Journal 真正生效：引用缺失的真实 payload → StorageInvalid。

任何 `loadEvents(raw,snapshot)` / `EventStoreMergeSpec.merge(...history...)` 出现在业务模块都属于结构性 RED。

## 恢复 fold 不变量（PERSIST-010）的实现落点

不变量权威定义在历史 what/persist PERSIST-010（迁移后由本包 WHAT 015 承接）；
逐 fact 校验在 `Composition/Durable/Fold.fs` 恢复事实分支 + 各 domain fold（`CompanionFactFold`/
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
