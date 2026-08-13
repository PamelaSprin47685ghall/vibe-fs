# Persist — 目标实现

## Implements

行为合同见 `what/persist.md`；本文件只描述 Git raw append、payload、merge/converge、AgentJournal adapter 与 durable effect 算法。

## Ownership

Store 边界与所有权见 `shape/persist.md`。

---

## 核心端口

```text
IEventStore
  OpenSnapshot / Refresh → StoreSnapshot
  Append(base, events) → StoreSnapshot
  Publish(AppendCandidate) → StoreSnapshot
  Merge(snapshots) → StoreSnapshot
  Converge(remote) → StoreSnapshot   // 经 GitGateway 绑定
```

`StoreSnapshot` 只冻结 `RootOid`（`refs/wanxiang/store` 指向的 root tree），不背全量 EventId 集合。  
`AppendCandidate` = base snapshot + new events + Persist 侧 payload blobs（GitObjectId × bytes）。

生产装配：

```text
ProcessGitRawStore(commonDir)
  → EventStore.create / createWithRetries(+Converge)
  → WorkspaceEventStore.acquire（process-local refcount）
  → IJournalEventStoreBoot / EventStoreJournalWriter
  → AgentJournal.createFromEventStore | createFromProjection
```

Git 传输唯一入口：`GitGateway`（Fetch/Pull/Push/ConvergeStore）。  
`HookDispatcher`：`reference-transaction` / `pre-push` shim + recursion guard（`WANXIANG_GIT_SYNC_ACTIVE`）；不得覆盖用户已有 hook；converge 协议注入，hook 自身不 fetch/merge。

Dumb remote 只懂 objects / refs / CAS / auth；不得跑 Domain reducer。

---

## PERSIST-007：PayloadRef / Git raw blob

大正文不进 envelope inline（超过阈值或不适合 inline 时）：

```text
payload bytes → IGitRawStore.WriteBlob → GitObjectId
             → Domain PayloadRef（opaque）
             → envelope.payload_refs
```

Committed root 的 `payloads/` **恰好等于**全部 committed events 的 `payload_refs` 并集（closure）：dangling ref → `StorageInvalid`；未引用 payload 不得进入 committed root。

顺序：先写 payload objects 并校验 closure，再 `Publish`/`Append` 引用它们的 events。Payload 写失败 → 无可用 receipt；CAS 未见证 EventId → 不得假装已提交（PERSIST-003）。

### AgentJournal 适配映射

`EventStoreBlobWriter` 对既有 `BlobRef` 调用方保持：

```fsharp
// BlobRef 路径形态仍为 blobs/<handle>
// <handle> = Git blob OID hex（与 PayloadRef / Persist oid 同文）
// BlobDigest = SHA-256(UTF-8 content)，用于完整性，不是 Git OID
```

成功路径**不得**创建磁盘 `blobs/` 目录；body 只在 Git ODB。  
`BlobRef` / `BlobDigest` 定义仍在 `Identity.fs`；Persist 映射 OID，Domain 不直接操作 `GitObjectId`。

---

## PERSIST-009：Durable Effect

```text
Requested / Claimed
→ 按确定性效果身份执行或核对
→ Accepted / Created / Published
```

| 效果 | Request | Accepted | Reconcile |
|------|---------|----------|-----------|
| Worktree | `WorktreeCreateRequested` | `WorktreeCreated` | `git worktree list` / Sweep |
| Publish | `PublishClaimed` | `Published` | ref/head（ORCH-007） |
| Prompt | （PROMPT-011） | PhysicalAccepted | PROMPT-011 at-most-one |
| Blogger | `BloggerRequestMaterialized` | Entry/SquashCommitted | ProviderRun receipt |

崩溃后：Requested 未 Accepted → **结局未知**。先执行表中 Reconcile；仅当物理证据证明效果不存在且该效果的合同允许幂等重试时才重试。Prompt 例外地保持 Pending，按 PROMPT-011 检索 `PromptKey`，不得自动重发。Accepted → 该领域合同已确认物理完成；重复 Accepted 幂等；不得把 Accepted 折回 Requested。

### Session 创建例外

Host 在 `session.create` 返回前不分配 child SessionId → 不引入 `SessionCreateRequested`。  
accepted 证据 = 链接事实：`HandleLinked` / `CompanionBloggerLinked`。

---

## Append / Merge / Converge 算法要点

1. **Canonicalize**：`EventEnvelope.normalize`（parents / payload_refs 去重排序）→ canonical JSON+LF bytes。  
2. **Materialize**：EventId 分片写入 `events/`；payload 写入 `payloads/`；构造候选 root。  
3. **CAS**：`CompareAndSwapRef(refs/wanxiang/store, expected, new)`；Absent 用 zero-oid expected。  
4. **K-way Merge**：`Merge` = set union + identity dedupe（structural tree merge；Spec oracle 仅测试）。禁止 wall_clock LWW。  
5. **Converge(remote)**：经 GitGateway 双向同步 store objects+ref → merge → 校验 fold + payload closure → 发布；禁止单向 `PullStore`/`PushStore` API。  
6. **Fold**：按 `parents` 做 deterministic topological fold；StorageInvalid vs DomainConflict 分类见 PERSIST-003。

---

## AgentJournal 适配表面（Strategy A）

`AgentJournal` **保留**为应用侧 API；耐久底层是 EventStore：

| 组件 | 职责 |
|------|------|
| `EventStoreJournalCodec` | Journal envelope ↔ `EventEnvelope`（固定 journal event_type） |
| `EventStoreJournalWriter` | `IJournalWriter`；成功路径无 `.ndjson` / 无目录 `blobs/` |
| `WorkspaceEventStore` | common-dir → `ProcessGitRawStore` + `IEventStore` |
| `IJournalEventStoreBoot` | `ResumeOrCreate` 不点名 `IEventStore` token，避免 dual-write 同文件桥 |

W1-boot `EventStoreJournalWriter.loadJournalEnvelopes` 走 `GitRawStore.loadEventEnvelopes`：一次 `events/` 树遍历 + 每 blob 一次读取，O(|events|)。禁止经 `EventStoreMergeSpec`（set-union oracle，仅合同测试）。非 JournalEnvelope 事件解码后跳过，不参与 AgentJournal fold。

同一生产模块不得同时写 EventStore **与** Journal NDJSON 路径（`unified-store-gate` `dual-write`）。  
已删除：生产 `Boot.fs`、NDJSON `JournalWriter`、目录 `BlobWriter`、`AgentJournal.createFromBoot` / directory `create`。

---

## 上下文恢复 fold 实现落点（不变量见 what/persist.md PERSIST-010）

拒绝条件（不变量）权威定义见 `what/persist.md` PERSIST-010——不满足任一条拒绝 envelope，fail closed。  
本处只留实现落点：恢复 fold 逐 fact 校验在 `Journal/Fold.fs` 的恢复事实分支；物理 event 形状见 PERSIST-001/002；Journal 行经 codec 进入 EventStore。
