# durable-events

> durable truth 必须以**不可变事实、原子提交与确定性 fold** 形成单一可重放 substrate；
> 否则每个 feature 自造 journal/blob/ref/fold 后，restart 与跨 feature consistency 会分裂成多个世界。

## 这是什么包

`durable-events` 拥有「动态 durable state 的唯一解释权」。它规定：

```text
事实      = 不可变 event
修改      = append event（永不 rewrite）
删除      = append tombstone / retirement event
恢复      = deterministic fold
查询      = projection（O(1) 积分，不扫全历史）
大正文    = Git raw payload（content-addressed）
原子发布  = refs/wanxiang/store 的 CAS
```

EventStore 是**唯一 durable substrate**：没有第二个 journal、第二套 blob 目录、
feature-owned ref。旧 NDJSON journal / RuntimePath `blobs/` / Student QA 私有文件
**leave-unread**——不读、不迁、不双写。

```text
README.md   ← 你在这里
WHY.md      为什么必须只有一个解释权（被拒方案考古）
WHAT.md     唯一 normative 合同：16 条命题（DURABLE-EVENTS-001..016）
HOW.md      实现模型：Git raw store + CAS append + 确定性 fold；历史与弃权
PROOF.md    每条命题的测试落点
tests/      本包拥有的可执行 proof（8 个文件，72 断言）
```

## WHAT 概览（按命题组）

- **事实与身份**（001–003）：event=truth 且 append-only；无版本 envelope + additive
  vocabulary；canonical JSON 是 identity 协议。
- **提交原子性**（004–006）：CAS 是唯一提交原语、无部分写入；CAS 冲突先查 EventId
  再 bounded retry；提交结局的 durable witness = canonical root。
- **fail-closed**（007–008）：任一校验失败拒绝投影/启动、禁跳过坏 event；并发 fork
  是 DomainConflict（归 `durable-convergence`），绝不升级为全局 corruption。
- **单一 substrate**（009–011）：无 schema/migration generation、leave-unread；
  唯一 canonical ref、feature store 非法；Git raw 是唯一物理介质、无 commit 历史。
- **payload / projection / fold**（012–015）：payload closure；查询 O(1) 且投影非
  第二真相源；确定性 topological fold；恢复 fold 不变量 owner。
- **所有权红线**（016）：Git 物理概念只属于 Persist/Git infrastructure。

## HOW 概览

```text
ProcessGitRawStore(commonDir)               只做 plumbing：hash-object / mktree / cat-file / ls-tree / update-ref
  → EventStore.create / createWithRetries    Append/Publish/Merge/Converge + CAS retry
  → WorkspaceEventStore.acquire             process-local refcount
  → EventStoreJournalWriter                 Journal 成功路径只写 IEventStore；无 .ndjson / blobs/
  → AgentJournal.createFromEventStore       PERSIST-008：Snapshot 是积分态，不是重放
```

核心文件（精确到符号）：

| 概念 | 文件 |
|---|---|
| canonical JSON 协议 | `src/Wanxiangshu/Infrastructure/Persist/CanonicalEventCodec.fs`（`encode`/`checkIdentity`/`mergeByIdentity`/`tryDecode`） |
| Store 类型与错误 DU | `Infrastructure/Persist/StoreTypes.fs`（`StoreSnapshot`/`AppendCandidate`/`StorageInvalid`/`DomainConflict`/`StoreRef`） |
| Append/Publish/CAS retry | `Infrastructure/Persist/EventStore.fs`（`IEventStore`/`validateAppendSet`/`append`/`publish`） |
| 确定性 fold | `Infrastructure/Persist/EventStoreFold.fs`（`AuthoritativeEventTypes`/`topologicalOrder`/`applyStream`/`fold`） |
| Git raw 物理层 | `Infrastructure/Persist/GitRawStore.fs`（`EventIdShard`/`PayloadClosure`/`loadEventEnvelopes`）、`ProcessGitRawStore.fs` |
| Domain 侧 envelope | `Domain/EventStore.fs`（`EventEnvelope`/`PayloadRef`/`EventParents.canonicalize`） |
| Journal 适配 | `Journal/{EventStoreJournalCodec,EventStoreJournalWriter,JournalEventStoreBoot,AgentJournal}.fs` |
| 恢复 fold 入口 | `Journal/Fold.fs`（`apply`：第一个不可能的行即停，fail closed） |

## proof 概览

```bash
node --test requirements/durable-events/tests/event-store-append.test.mjs
node --test requirements/durable-events/tests/event-store-identity-collision.test.mjs
node --test requirements/durable-events/tests/event-store-fold.test.mjs
node --test requirements/durable-events/tests/event-store-journal-boot.test.mjs
node --test requirements/durable-events/tests/event-store-journal-codec.test.mjs
node --test requirements/durable-events/tests/event-store-journal-writer.test.mjs
node --test requirements/durable-events/tests/hook-dispatcher.test.mjs
node --test requirements/durable-events/tests/append-only-laws.test.mjs
# 全量：node tests/unit/run.mjs（自动包含 requirements/**/tests/*.test.mjs）
```

## DEPENDS ON

无。本包是 durable 层的根 substrate，所有 durable 包消费它，它不消费任何包。

## 边界（DOES NOT OWN）

- 多 replica 的 set-union / DomainConflict 收敛规则 → `durable-convergence`。
- 外部效果的 Requested/Accepted/outcome-unknown 语义 → `effect-accounting`。
- 各 domain event 的业务意义（Todo/Context/Review/Casebook/Strength…）→ 各 domain owner。
- Git raw ODB / refs 必须永久保持、旧 NDJSON/feature store compatibility → 迁移期
  已被 unified-store-gate + leave-unread 钉死（见 HOW「历史与弃权」）。
