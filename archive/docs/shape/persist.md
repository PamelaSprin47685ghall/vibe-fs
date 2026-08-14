# Persist — 边界

## 单一 durable 边界

动态 durable state 的唯一物理介质是仓库 Git raw object database。  
Canonical ref：

```text
refs/wanxiang/store  →  root tree（不是 commit）
```

概念形态（EventId 分片；禁止 ordinal / sequence 命名）：

```text
root tree
  events/<hex-prefix>/<EventId>.jsonl   // one event = one blob
  payloads/<git-object-id>              // payload closure
```

**不使用** commit history / branch / tag / merge commit 表达 EventStore 历史。  
**不得**出现 `refs/wanxiang/store-v2` 或任何 feature-owned `refs/wanxiang/<feature-…>`。

静态、人工维护并随 repository source 提交的内容（`resources/`、正式 docs、Change 文件等）仍是普通 repository content，不属于 EventStore。

## PERSIST-006：所有权与路径

| 层 | 拥有 | 不得拥有 |
|----|------|----------|
| Domain | `EventEnvelope` / `PayloadRef` / 因果与业务语义 | `GitObjectId` / `RootOid` / `StoreSnapshot` / `AppendCandidate` / Git 操作 |
| Persist | canonical JSON、CAS publish、`StoreSnapshot`、merge/fold、payload closure | 领域 event vocabulary、feature ref |
| GitGateway / HookDispatcher | Wanxiang-initiated Git transport、store converge、hook shim | Domain reducer、第二套 merge 运行时 |
| AgentJournal | 应用侧 journal 适配表面（append fact / fold projection） | 平行 NDJSON/Blob 后端、独立 canonical ref |

生产 `IGitRawStore` 实现是 `ProcessGitRawStore`：只做 plumbing（`hash-object` / `mktree` / `cat-file` / `ls-tree` / `update-ref`），不把 HEAD/index/branch 当 store history。

旧 RuntimePath journal（`wanxiangshu-next` / `*.ndjson`）与目录 `blobs/`：**leave-unread**；不得再作为生产写入口。  
Host 通过 `WorkspaceEventStore` 按 git common-dir 取得 process-local raw+store；Journal boot 走 `IJournalEventStoreBoot` / `EventStoreJournalWriter`，不得旁路另开 NDJSON writer。

## 写入口纪律

领域事实的 durable append 最终进入 EventStore；Strategy A 下 Application/Session 可继续调用 `AgentJournal`，但其成功路径只写入 `IEventStore` / `IGitRawStore`。Strength 的 Prepared/Promoted/Traced 也只走该边界；frame/predictor 大 material 只能成为 envelope `payload_refs`，不得建立 Strength NDJSON、RuntimePath blob 或 feature ref（STRENGTH-006/017）。  
各领域外部效果的 Requested/Accepted 成对出现（PERSIST-009），不得旁路「先改内存再补盘」。

上下文恢复事实（PERSIST-010）的单一观察写入口是相应 reconcile 路径（例如 compaction → `ContextReanchored`），禁止多处随手写 fold 特例。

## StudentQaStore — G3 已删除（retired）

`StudentQaStore` / 私有 `QA.md` filesystem backend **不存在于生产**（G3 clean-break；PERSIST-011 空缺）。无 dual-write、无 legacy reader、无编译期 QA 权限面。
