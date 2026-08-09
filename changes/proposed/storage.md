> **状态**：Active — 本文件为变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。
> 原始 Proposal 已冻结于下方；后续事实仅追加于 Active work / Blockers / Final outcome。

# 万象术统一持久化底座 — 正式版方案与保姆级开发指南

> **文件定位**：`changes/proposed/unified-git-raw-event-store.md`
> **Active 路径**：`changes/active/storage.md`
> **优先级**：P0 / 横切型架构
> **兼容性策略**：ExplicitMigration + Clean Cutover
> **原则**：领域拥有事实，Persist 拥有持久化，Git 拥有字节；任何 feature 都不再拥有自己的 storage。

---

## 第一部分：正式版方案

---

### §0 执行决策

万象术当前持久化没有统一章法。现有系统及正在等待实施的 Proposal 中，已经同时出现：

| 现存机制 | 来源 |
|---|---|
| Journal NDJSON | 现行 Persist |
| RuntimePath blob | 现行 Persist |
| Student QA private file | Student & Teacher |
| domain-specific durable facts | 各 domain |
| Casebook Git tree ref | perm-inspector |
| custom refspec | perm-inspector |
| remote tracking ref | perm-inspector |
| revision / wall_clock merge | perm-inspector |
| feature-specific filesystem layout | 各 feature |

**本 Change 终止这种模式。**

以后万象术只有一个 durable persistence substrate：

```
Canonical NDJSON Events
    + Append-Only Event Sourcing
    + Git Raw Object Database
    + Single Canonical Store Ref
    + Generic Git Gateway
    + Dumb Remote
```

领域功能**只允许**定义：
- Event
- Projection
- Invariant
- Retention semantics
- Replication scope

领域功能**不得再**定义：
- database / journal format / filesystem store / blob directory
- storage ref / refspec / schema version / migration version
- remote merge protocol / sync hook / storage generation

> 这不是新增第五种存储。这是删除其它所有 durable storage 解释权。

---

### §1 最终目标

完成后，万象术所有动态 durable state 统一满足：

| 操作 | 统一语义 |
|---|---|
| 事实 | = event |
| 修改 | = append event |
| 删除 | = append tombstone / retirement event |
| 恢复 | = fold events |
| 查询 | = projection |
| 大正文 | = Git raw blob |
| 原子发布 | = Git ref CAS |
| 同步 | = Git objects + ref |
| 远端 | = dumb server |

**不存在**：
- UPDATE row / rewrite JSON / rewrite state file / overwrite blob
- mutable durable document
- schema v1 / v2 / store v1 / v2
- migration mode forever
- feature-owned ref / feature-owned remote protocol

**不属于本 Change 的**：静态、人工维护并随 repository source 正常提交的内容（`resources/`、`.agent/`、Rulebook authored Markdown、正式 docs、Change 文件）仍是普通 repository content。

---

### §2 第一原则：Event 是唯一 durable truth

任何业务状态只允许以 event 表达。示例 event vocabulary：

```
ManagerJobCreated / PromptRequested / PromptAccepted
BlogObservationCommitted
StudentQaQuestionAppended / StudentQaAnswerAppended
StrengthCandidatePrepared / StrengthCandidatePromoted
InspectorCaseCaptured / InspectorCaseRefreshed / InspectorCaseAccessed / InspectorCaseEvicted
```

Projection 可以产生：`CurrentManagerJobs`、`CurrentContext`、`CurrentCases`、`CurrentStudentQa`、`RecentObservations`、`CurrentStrengthCandidates`。

但 projection **不是第二真相源**。

**禁止**：先改 projection → 以后补 event。
**必须始终**：append durable event → commit 成功 → projection consume。

---

### §3 第二原则：Append Only

Committed event 永远不可：修改、覆盖、删除、原地升级、重新解释。

错误事实通过新事实纠正：
```
CaseCaptured → CaseRefreshed
CaseCaptured → CaseEvicted
PromptRequested → PromptAccepted
PromptRequested → PromptRejected
```

**禁止**：打开旧 JSON 把 status 从 pending 改成 accepted。
**禁止**：把旧 event rewrite 成新 schema。

> Event sourcing 的定义就是：历史只增长，不回写。

---

### §4 第三原则：统一 NDJSON + Authority 单事件单对象

所有 event 使用 canonical JSON object。逻辑 event stream 是：

```
event
event
event
...
```

物理编码统一为：**one canonical JSON object + LF**（即 NDJSON）。

Authority 层收紧为：**one event = one canonical JSON+LF blob**，路径按 EventId / 内容 identity 分片命名（例如按 EventId hex 前缀分片），**绝不按 sequence number / ordinal 命名**。这保证：
- 多进程同时 append 不会因"下一个序号是 42"路径撞车
- 不同 replica 即使批处理分组不同，只要 event 集合相同，canonical root 就相同（k-way merge = 集合并，不受 batch 影响）

Batch / pack 仅允许作为 Git 传输与压缩的后期优化，不得影响逻辑 tree 形态与 canonical root 计算。

Authority segment 一旦 published → immutable，不得打开已有对象继续写。append 的物理含义是：
```
旧 event blobs 保留 + 新增 immutable event blobs
```
而不是：
```
seek(existing.ndjson) → append bytes → fsync
```

这样不再存在"半条 journal line 已经进入 canonical history"的物理状态，也不存在"同集合不同分组得到不同 root"的歧义。

---

### §5 Event Envelope：无版本 + 因果顺序

统一 envelope **不含**：
- schemaVersion / storageVersion / journalVersion / formatVersion / generationVersion

**不允许**：
- `v1/` / `v2/` / `refs/wanxiang/store-v2` / `CaseEventV2` / `schema-2.json`

基本 envelope 表达事实身份 **与** 因果前驱：
```json
{
  "event_id": "...",
  "stream_id": "...",
  "event_type": "...",
  "parents": ["<predecessor EventId>", "..."],
  "payload": {}
}
```

- `parents`：本 event 直接依赖的 predecessor EventId 集合（可为空，表示 stream 根）。`parents` 构成因果 DAG 的边；缺省或空数组视为无前驱。
- 同一 `stream_id` 内，`Requested → Accepted`、`Opened → Question → Answer → Closed` 等 happens-before 必须通过 `parents` 显式表达，不得隐式依赖物理存储顺序或 EventId 字典序。

核心原则：**版本不是领域事实，因此不进入 event。**

Event evolution 采用 additive vocabulary：
- 已经写出的 `event_type + field meaning` 永远不改变语义
- 如果以后出现真正不同的新事实：定义新的 semantic event
- 不是同一个事实升级 schemaVersion

#### §5.0 Canonical JSON 协议（正式协议）

“canonical JSON”不是实现细节，是 identity 协议。同 EventId + 不同 canonical bytes 判 identity collision，`parents` 又是语义集合，若不冻结字节级 canonicalization，`[A,B]` vs `[B,A]`、key 顺序、数字格式、Unicode escaping 都会意外产生 collision。

正式协议（§4/§5 权威）：

- 编码：UTF-8，无 BOM，恰好一个 LF 结尾（`JSON + "\n"`），不得多余空白。
- 键序：object key 按 **Unicode codepoint 升序**（byte-level 亦等价于 UTF-8 升序，因 key 为 ASCII/UTF-8 安全子集）排序，递归适用于 envelope 与 payload 的任意嵌套 object。
- 数字：`System.Text.Json` / `F# JsonValue` 默认序列化产生的 decimal 归一化形式，不得出现 `+`、`NaN`/`Infinity`、指数大小写混用、前导零；同一数值在不同实现必须归一到同一字符串。
- 字符串：RFC 8259 转义；`"` / `\` / 控制字符 `U+0000..U+001F` 必须转义，`/` 可选不转义；同一抽象字符串的不同合法 escaping 必须归一到本规范选择的唯一形式。
- `parents`：**先去重，再按 EventId canonical 文本序（hex/lexicographic）升序排序后再编码**；禁止把集合写成无序数组。
- 稳定性：canonical bytes 是 commit 时写入 Git blob 的权威字节，后续实现不得以“更漂亮的 JSON 打印”改变已 committed event 的 bytes；校验失败 = StorageInvalid。

#### §5.0.1 Payload Shape 冻结（无灰区）

为使“无 schemaVersion”没有灰区：同一 `event_type` 的 **payload shape（字段名、字段含义、必填性）一经 committed 即冻结**。不得在同一 `event_type` 上“偷偷增加有语义的新字段 / 改变字段含义”并复用旧类型的解码路径。若需新的语义：新增 `event_type`（additive vocabulary）。新增可选字段仅当且仅当旧 decoder 按“未知字段忽略”的 canonical 规则仍能得到与旧语义完全一致的 projection 时才被允许；否则必须新类型。是否允许忽略未知字段、以及忽略/拒绝的精确规则，必须在 Persist 层统一定义，不得由各 domain 各自解释。

#### §5.1 因果模型（Causal Model）

- 全部 events 按 `parents` 构成全局因果 DAG（允许跨 stream 引用，但同 stream 内前驱必须显式声明）。
- Merge 仍只是 **集合并（set union）**，不解释因果。
- Projection 对 DAG 做 **deterministic topological fold**：任一 event 只有其全部 `parents` 已 fold 后才可 fold；`parents` 未知 / 缺失 / 成环 → 见 §5.3 StorageInvalid。
- 每个 domain stream 必须在设计时声明：其 fold 是 **order-independent**（可交换），或其顺序语义完全由 `parents` 决定。不得假设"EventId 排序 = 业务时序"。
- 物理 canonicalization 需要 deterministic 字节序时，可按 `EventId` 排序作为 **物理 tie-breaker**；此排序永不作为业务时序使用。
- 合法并发 fork 是物理层正常产物（A、B 离线同见 parent=P 各自 append A1/B1，union 必然得 fork），不得被定义为全局 corruption。领域互斥的并发 fork 属 §5.3 DomainConflict，由 projection 表达为 deterministic conflict state 并经后续 resolution event 收敛。

#### §5.2 历史词汇单调性（Monotonic Vocabulary）

- 已 committed event 的 `event_type + field meaning` 永不改变语义；unknown authoritative event 必须 **fail closed**（见 §5.3 StorageInvalid）。
- 推论：`Committed semantic event vocabulary is monotonic; old event decoders are permanent.`
- Clean Cutover 只能删除旧 Journal/Blob backend；一旦某个 `event_type` 被 committed，其 decoder / semantic support 永久进入兼容负担，不得以"已切新存储"为由删除旧 event 的解码能力。这正是"无 storage-version"严谨性的来源：消灭的是 storage-version compatibility，不是 historical-event compatibility。

#### §5.3 Fail-Closed 分类（StorageInvalid vs DomainConflict）

> 目标：使“append-only union 必然能产生物理合法 fork”与“全局不可恢复”是正交的。合法的并发永远不能把 Store 永久打成不可恢复。

**StorageInvalid — 全局 fail closed，不可恢复，必须告警并阻断 projection 前进：**

- 坏 JSON / 非 canonical JSON / 非 NDJSON / 缺 LF / 编码错误
- EventId collision：同 EventId + 不同 canonical bytes
- 缺 parent / parent 未知 / `parents` 成环 / payload missing / payload hash mismatch
- unknown authoritative `event_type`（含旧 client 遇到新 committed type）
- 任一 envelope 必填字段缺失或类型错误

效果：对应 StoreSnapshot 视为 corrupted/不可 fold；process 必须拒绝以它构建 projection，提供 `StorageInvalid` 诊断并引导人工修复/升级；不得“跳过坏 event 继续”。

**DomainConflict — 物理合法但业务互斥，并存于同一 history，可被 projection 表达为 deterministic conflict state，并允许后续 resolution event 收敛：**

- 同一 `stream_id` / 同一业务键的合法并发 fork（例：A、B 离线同见 P 各自 append A1/B1）
- 互斥状态并发断言（例：同一 QA 同时 Close/Reopen、同一 Job 同时 Accept/Reject）

效果：history 保留全部 competing events，projection 按 domain 规则收敛为 `Conflict { heads; reason }`（或等价的 domain-specific conflict 投影），并定义 resolution 协定：

- Resolution event（如 `FooConflictResolved` / `JobConflictResolved` / 领域具体 `*Resolved`）必须以 **所有 competing heads 为 `parents`**（至少包含需裁决的 heads 集合），从而在 DAG 上显式声明“已知并裁决了这些并发分支”；
- 仅当 resolution 及其全部 parents 已 fold，后续 projection 才离开 conflict state。

**严禁** 将“领域禁止的并发 fork”定义为 StorageInvalid；`§5.1 / §10.9 / §40` 中“非法 fork → fail closed”一律改按本条解释：Storage 层永不因自然 fork 进入不可恢复；“forbidden fork”指 DomainConflict 的业务不可接受态，由 projection 表达并经 resolution 收敛。

Unknown authoritative event：**属 StorageInvalid，fail closed**。不得"看起来差不多"→ 猜 schema → 跳过字段 → 继续启动。

---

### §6 Git Raw 是唯一物理 durable store

Git 只作为：
- content-addressed object store
- tree store
- atomic ref CAS
- object transport

**不使用**：commit history / branch / tag / merge commit / storage release commit。

动态 Store 不制造产品意义上的 Git history。

统一 canonical ref：
```
refs/wanxiang/store
```

直接指向 **root tree object**（而不是 commit）。

概念模型（EventId 分片，不使用 ordinal）：
```
refs/wanxiang/store
        │
        ▼
     root tree
        │
        ├── events/
        │     ├── ab/
        │     │     ├── ab12...cdef.jsonl   // one event = one blob, 按 EventId 分片
        │     │     └── ...
        │     ├── 3f/
        │     └── ...
        │
        └── payloads/
              ├── <git-object-id>
              └── ...
```
> 允许按 `events/<hex-prefix>/<EventId>.jsonl` 或等价 content-identity 分片；禁止 `streams/<stream>/<ordinal>.ndjson` 或任何 sequence-number 命名。`streams/` 视图如需可在 projection 侧派生，不得作为 authority 物理形态。

必须保持：一个 canonical ref、一个 object database、一套 append protocol。不得让领域拥有自己的 canonical ref。

---

### §7 Large Payload

当前各种 TextRef / snapshot / answer / Q/A body / frame bundle / diagnostics / large prompt material，如果不适合 inline event：

```
payload bytes → Git blob → GitObjectId → event 引用
```

Git object ID 就是物理 content identity。

**不再维护第二套**：RuntimePath/blob/ / SHA256 path convention / feature blob directory / blob database。

Store root 必须保持 committed payload object 可达。Event 引用 payload 后：payload 与 event 同属 committed history，不能因为 projection 不再需要它就删除历史 payload。

#### §7.1 Payload Subtree Canonicality（Closure 归一）

为使“相同 event 集合 → 相同 canonical root”（§4/§9）成立，需精确定义 root 中 `payloads/` 的集合语义，否则 A 曾错误 publication 一个未被任何 event 引用的 payload 而 B 没有，event set 相同而 root 仍不同，破坏 merge 的纯函数性。

规则：

- Envelope 在 Persist 侧拥有语义明确的 `payload_refs: Set<GitObjectId>`（序列化时纳入 canonical envelope；Domain 侧仅见 opaque `PayloadRef`，不得直接操作 Git OID，见 §45）。
- `payload_refs` 的 canonical 编码同样走 §5.0 规则：去重 + 按 OID 文本序排序。
- 当 event 内联小正文时 `payload_refs = ∅`；当引用大正文/快照/material 时 `payload_refs` 非空且一一对应实际引用的 blob OID。
- **Committed root 的 `payloads/` tree 恰好等于所有 committed events 的 `payload_refs` 的并集（closure）**：无遗漏（dangling ref → StorageInvalid）、无额外（unreferenced payload 不得被纳入 committed root）。GC 仅可清理 **从未被 committed root 引用的 orphan blobs**，已 committed payload 随 history 永久可达。
- 验证：`root payload set == ⋃ events.payload_refs` 可机械检查；违反则 fail closed。

---

### §8 Git tree 不是"版本"

每次 append 都可能产生新的 tree object。这不表示 Store Version 1 / 2 / 3。

Tree object 只是 Git immutable storage primitive。系统不维护：previous root / parent root / release root / version chain。

Canonical ref 永远只回答：**当前完整 append-only event set 是什么。**

历史来自 event 本身，不是来自 Git commit graph。

---

### §9 Atomic Append（EventId 分片，无 ordinal 争用）

**Canonical ref 的存在性同样走 CAS：不存在即 `Absent`。** 不设独立 `CreateRef` 路径。接口上只提供一种 CAS：

```
CAS(ref, expected = Absent | R0, new = R1)
```

- 空 repository 首次 publication：`CAS(refs/wanxiang/store, expected=Absent, new=R1)`。两进程同时首次 append → 一赢一退，输者观察到 ref 已存在后按“CAS 失败”路径走普通 k-way merge/retry。
- Remote 首次 push 亦为 lease creation：`expected remote = Absent` 的 CAS/lease-push。
- 普通 append：`CAS(refs/wanxiang/store, expected=R0, new=R1)`。

> 禁止 `CreateRef` 与普通 CAS 两套 bootstrap 并存；禁止“首次创建走特殊协议，之后走另一套”。

append 基本流程：
```
observe canonical root R0-or-Absent
→ canonicalize event bytes（one event = one JSON+LF blob, parents 已显式，§5.0 归一化）
→ write event/payload raw objects
→ construct root R1
     R1 = (R0-or-Absent) + new EventId-sharded event blobs + newly referenced payload objects
→ CAS: update refs/wanxiang/store from (R0-or-Absent) to R1
```
> 禁止按 ordinal / sequence number 命名路径；两进程同时 append 不应因序号分配产生路径冲突。不同 batch 分组必须得到同一 canonical root。

**成功**：Committed。
**CAS 失败**：重新读取 canonical root（含 Absent→present 转变）→ 验证 event_id 是否已经存在 → 不存在则基于新 root 重建 append → bounded retry。

进程在 CAS 附近崩溃时，不根据"函数有没有返回成功"猜。恢复只问：canonical store 中是否已经存在这个 EventId？
- 存在：Committed
- 不存在：NotCommitted

旧式 `CommitUnknown → 永久无法确定` 应被重新审视。Git canonical root 本身就是 commit outcome 的 durable witness。

---

### §10 核心并发模型：Per-Process + Snapshot + K-Way Merge

万象术不能假设一个 repository = 一个 Wanxiang process。实际运行允许：OpenCode process A / B / C、IDE / external Git、remote replica 同时存在。

因此统一 Persist **不建立**：global process lock / single daemon writer / single in-memory authority / repository-wide mutex。

统一语义保持：**per process + immutable snapshot + local append-only delta + k-way merge**，并将其推广到所有 dynamic durable storage。

#### 10.1 Process 是 Replica，不是 Authority

每个 OpenCode process 都是一个独立 replica。任一 process 可以：read / append local events / build projection / participate in merge / publish / sync remote。

没有某个特殊 process 拥有 master state / current truth in memory / merge coordinator lifetime。

Process crash 后：其内存全部可以消失。正确性不能依赖 process-local memory。

#### 10.2 Snapshot 是一次冻结观察

每次 process 开始一个需要一致 durable view 的操作时，取得 immutable `StoreSnapshot`。

Snapshot 一经取得：不随其它 process publication 自动变化。

A 不允许在一个逻辑操作中：前半段按 SA 判断、后半段偷偷读成 SB。需要看到新事实时必须显式 refresh snapshot 或在 publication conflict 时进入 merge + retry。

#### 10.3 Snapshot 不是 Storage Version

虽然 snapshot 有不同 root OID（S0 / S1 / S2），它们不是 schema v1 / v2 / database version / product version。只是某个 process 在某个时间点冻结观察到的 immutable durable state。

仍然保持：no schemaVersion / no storeVersion / no migration generation。

#### 10.4 每个 Process 只追加自己的 Delta

一个 process 在 snapshot `S` 上执行业务后形成 `Δprocess`。它只能 append new immutable events，不能修改 `S` 内已经存在的 event。

逻辑 mutation 是：`candidate = S ∪ Δprocess`，而不是 `load S → mutate existing records → save S'`。

#### 10.5 K-Way Merge 是统一 Primitive

统一 primitive 必须从第一天就是：`KWayMerge(snapshot[])`。

输入可能同时来自：current process snapshot / another local process publication / remote tracking snapshot / hook-observed snapshot / recovery snapshot。

要求 merge：
- **associative**：merge(A, merge(B, C)) = merge(merge(A, B), C)
- **commutative**：merge(A, B) = merge(B, A)
- **idempotent**：merge(A, A) = A
- **deterministic**：同一组输入，无论枚举顺序、无论由哪个 process 执行，必须产生相同 canonical result

#### 10.6 Event Store 的 K-Way Merge

基础 merge 不采用 mutable snapshot LWW。**Specification oracle**（纯函数语义）是：

```
allEvents = union(snapshot1.events, snapshot2.events, ..., snapshotK.events)
```

按 EventId 去重：
- same EventId + same canonical bytes → one event
- same EventId + different canonical bytes → **StorageInvalid / identity collision → fail closed**（§5.3）
- 两个不同 EventId → 永远都进入 merged history（即使 DomainConflict，亦保留全部 facts，由 projection 表达 conflict）

领域冲突交给 domain fold / invariant 处理。

**绝对禁止** Store 做 wall_clock newer wins 从而把其中一个 durable fact 消失。

Persist 负责不丢事实；Domain 负责解释事实是否相容。

**Production 实现形态（P1）：** 鉴于物理 authority 已按 EventId 分片为 Git tree，不得在每次 append/fetch 时读取并反序列化全量 events 做 O(N) set-union。实际实现必须优先 **structural tree merge**：不同 EventId 路径直接 union；仅当同 EventId 路径的 blob OID 不同时，才读取 canonical bytes 校验是否 identity collision。复杂度应与 delta/tree-path 相关，而非 `N = total events`。`union(allEvents)` 保留为 specification oracle / 契约测试对照，不得作为生产算法指导。

#### 10.7 不建立 Process Registry

禁止为了支持多开而新增：ActiveProcesses / ReplicaRegistry / ProcessLease / ProcessGeneration / WriterElection / PrimaryProcess / LeaderProcess。

所有并发知识只通过：immutable snapshot / Git root/ref / CAS conflict / remote tracking snapshot 显现。

#### 10.8 K-Way Merge 不能变成第二运行时

禁止维护：MergeStage / ReplicaSyncState / PendingPeer / MergeGeneration / NeedMerge / NeedsPush / MergeQueueStateMachine。

每次 merge 都从当前真实输入重新计算：snapshots + local candidate + remote observation → merge → validate → CAS。竞争时 bounded recursion / retry。

#### 10.9 所有 Domain 的 Merge Algebra

统一 Persist 必须区分两层 merge：

**第一层：Storage/Event Merge（永不丢事实，永不以 fork 判全局 corruption）**
- 原则：append-only set union / identity dedupe / never lose facts
- fork 本身属 DomainConflict（§5.3），不得在此层升级为 StorageInvalid

**第二层：Domain Projection Merge（fold merged facts）**
- 原则不是 merge mutable snapshots，而是 fold merged facts
- 合法并发 fork 在此层表达为 deterministic `Conflict`；经 `*Resolved`（以全部 heads 为 parents）收敛

真正的公式是：
```
Projection(KWayMerge(S1, S2, ... Sk))
= Fold(Union(Events(S1), Events(S2), ..., Events(Sk)))
```
而不是 `Merge(Projection(S1), Projection(S2))`。

#### 10.10 Casebook 的 LWW 重新定位

现有 Casebook 中 revision → wall_clock → deterministic OID tie 用于多个 replica 对同一个 mutable logical Case snapshot 选 canonical winner。

统一 event sourcing 后，不应该把这个机制提升成 Store merge 规则。应改成：CaseCaptured / CaseRefreshed / CaseAccessed / CaseEvicted 全部先进入统一 append-only event set。

如果 Casebook 产品语义仍要求同 session 当前只展示一个 Case，则 revision / wall_clock / deterministic tie 只属于 **CasebookProjection**，用来从完整 history 派生 `CurrentCase(session)`。它不允许删除 loser event，也不允许影响其它 domain。

> LWW = Casebook projection rule，不是 Persist replication rule。

---

### §11 Remote：永远双向收敛

Store remote 不存在：pull-only / push-only / download cache / upload backup。

唯一同步 primitive 是 `SyncStore(remote)`，其固定语义：

```
1. fetch remote store ref
2. read local canonical store ref
3. merge append-only event sets（K-Way Merge）
4. validate merged event history
5. CAS local canonical ref
6. CAS-push / lease-push merged root to remote
```

即永远是 remote → local + local → remote，一次完整的 convergence。

**禁止提供**：`PullStore()` / `PushStore()` / `DownloadStore()` / `UploadStore()` 这种可以被调用方选择成单向复制的 API。

底层可以拆 helper，但 Application 可见的同步语义只有：**Converge**。

---

### §12 Dumb Server

远端仍然是完全 dumb 的 Git remote。Server 只提供：Git objects / refs / fetch / push / lease / CAS / authentication。

**不知道**：Event / Projection / Prompt / Manager / Student / Casebook / Strength / Wanxiang。

同步智能全部在 client：fetch → merge → validate → CAS local → lease push。

**禁止**：server-side merge / pre-receive domain reducer / post-receive projection / Wanxiang-specific server API。

因此普通 GitHub / GitLab / Gitea / bare repository / SSH Git remote，只要支持所需 Git ref/object transport，即可作为 Store remote。

---

### §13 Repository Git Integration 是 Persist 的组成部分

统一 EventStore 不能只在万象术运行时同步。必须覆盖：
- Wanxiang 发起 Git 操作
- 用户在 shell / IDE / GUI 发起 Git 操作
- 其它工具发起 Git 操作

即 `$ git fetch` / `$ git pull` / `$ git push` 即使完全绕过万象术进程，也必须成为 Store synchronization opportunity。

这不是 optional convenience。这是统一 Persist 的 transport integration。

---

### §14 普通 fetch / pull 自动进入双向同步

为 Store remote 配置 custom fetch refspec，使普通 `git fetch` / `git fetch origin` / `git pull` 同时 transport remote canonical store ref → local remote-tracking store ref。

例如概念上：
```
refs/wanxiang/store → refs/wanxiang/remotes/origin/store
```

然后由 `reference-transaction` 观察：state = committed + store remote-tracking ref changed → 触发收敛。**实现细节（P1）：** `reference-transaction` 已在一次普通 fetch 完成“fetch 远端 store ref → 更新 remote-tracking”的事实，触发的收敛**不得再次无条件执行 `ConvergeStore` 内置的 fetch**（嵌套 fetch / 锁递归）。应复用已观察到的 `observedRemoteSnapshot`：外部 fetch 承担 fetch phase，hook 侧仅执行 `merge(observedRemoteSnapshot, local) → validate → CAS local → lease-push`。公开 API 仍唯一是 `ConvergeStore(remote)`，内部允许私有 `ConvergeStoreWithObservedRemote` 以消除重复 fetch；仍百分之百满足“永远双向”。

---

### §15 普通 push 同样自动进入双向同步

建立 Wanxiang-owned、可安全组合的 `pre-push shim`：

```
user: git push
       │
       ▼
Wanxiang pre-push shim
       │
       ├── fetch remote store
       ├── merge with local store
       ├── CAS local
       └── lease-push merged store
       │
       ▼
original user push continues
```

---

### §16 Local Event Append 同样主动同步（允许 coalescing，非 durable）

当 EventStore 本地产生新 committed event：append event → CAS local canonical root → 如果配置了 sync remote，应随后触发 `ConvergeStore(remote)`。

> 高频场景下，允许 **process-local、ephemeral、single-flight / coalescing** 的 convergence 合并：100 个 append 可合并为一次网络 convergence。该 coalescing 不得 durable，不得表现为 `SyncStateMachine` / `MergeQueueState`，且每次真正发生的 sync 仍必须满足"永远双向"的完整语义（fetch → merge → validate → CAS local → lease push）。

同步入口至少包括：
- Local event publication
- External git fetch / pull / push
- Wanxiang GitGateway fetch/pull/push
- Repository bootstrap
- Recovery / resume

它们最终全部进入同一个 `ConvergeStore`。不得各自实现同步协议。

---

### §17 没有 Pull Mode，也没有 Push Mode

这是永久 architecture invariant。

假设 Local = {A, B}，Remote = {A, C}。任何同步入口最终目标都只能是：
```
Local  = {A, B, C}
Remote = {A, B, C}
```

**禁止**：git fetch → Local = {A, B, C} → Remote 仍然 {A, C} 作为成功完成的 Store synchronization。
**禁止**：git push → Remote = {A, B} → Remote C 被覆盖。

正确算法始终是：fetch first → union / validate → CAS local → lease push。

---

### §18 "永远双向"的精确定义

"永远双向同步"不等于网络断开时假装 remote 已成功。它表示：任何 Store sync attempt 的协议方向永远是双向 convergence。

如果 offline / DNS failure / auth failure / remote unavailable / lease contention exhausted：
- 允许 local append 已 committed、remote 尚未 convergence
- 但该状态是 **replication pending**，而不是一种合法的 local-only synchronization mode

下一次任何 synchronization opportunity 必须重新执行完整：fetch → merge → local CAS → lease push。

不需要 durable `SyncPending = true`。是否收敛可以直接从 local canonical root / remote canonical root 重新观察。

---

### §19 Append-only Event Store 的 Merge 不使用 LWW

与 `perm-inspector` 必须明确分叉。Casebook 原设计使用 revision + wall_clock LWW 来解决同 Case replica 冲突。统一 EventStore 不允许这样做。

因为 Store 保存的是 immutable facts，不是 mutable Case snapshot。

正常 merge 是：`LocalEvents ∪ RemoteEvents`，按 EventId 去重。

两个不同 EventId 都保留。如果它们在领域上冲突：保留两个事实 → projection invariant 判断冲突 → fail closed / domain recovery。

**不得**：看 timestamp → 后写覆盖先写。

---

### §20 Git Hook 是 correctness integration，不是 feature accelerator

旧 Casebook 可以说 reference-transaction hook = sync accelerator。统一 Persist 不再这样定义。

对于用户完全绕过万象术执行普通 Git 仍要求触发 Store sync，因此 repository Git integration 属于正式 transport contract — 外部 Git 触发收敛 **不是 optional convenience，而是 acceptance contract 之一**。

但是 correctness 不能依赖"我们一定成功篡改了用户已有 hook"。安装规则必须同时满足：
- 不覆盖用户 hook
- 不删除用户 hook
- 不改写无法证明 ownership 的 hook
- 支持 safe chaining / dispatcher
- 无法安全集成时明确诊断

正式实现应建立 **Wanxiang Git hook dispatcher / shim**，统一承载 reference-transaction / pre-push。

**规范层与指南一致性**：当无法安全安装 / chain dispatcher 时，**禁止**标记为 `acceleration disabled` 并静默降级。必须明确标记为 **`Git integration incomplete`**，并视为 repository 未满足"external Git always triggers convergence"这一 acceptance contract。允许继续 local durable，但不得声称该 repository 已具备完整的双路径收敛能力。

---

### §21 Hook Recursion

所有 Store-triggered Git 操作都可能再次触发 hook。必须有统一 recursion guard，例如概念上的 `WANXIANG_GIT_SYNC_ACTIVE=1`。

进入 ConvergeStore 后设置。内部 fetch / update-ref / push 再次进入 Wanxiang hook 时：detect guard → no-op。

禁止靠"正常应该不会递归"证明正确性。

---

### §22 一个 Remote，一个同步算法

首版统一 Store 对 repository 只定义一个 sync remote。默认 `origin`。如果没有 origin，则 Store 可以继续 local durable 工作。

一旦 sync remote 存在：所有同步机会 → 双向 `ConvergeStore(syncRemote)`。

不允许 fetch 从 A / push 到 B / Casebook 用 C / Strength 用 D。也不在第一版实现 multi-remote CRDT。

---

### §23 新的"Git 必经之路"定义

最终"挂在 Git 必经之路上"严格定义为两条路径同时成立：

**路径 A：万象术内部**
```
Wanxiang Git operation → GitGateway → Store convergence integration → Git
```

**路径 B：万象术外部**
```
User / IDE / external tool → ordinary Git → repository-installed Wanxiang Git integration → Store convergence
```

```
                 Wanxiang
                    │
               GitGateway
                    │
                    ▼
User / IDE ─────── Git ─────── Remote
                    │
             Wanxiang hooks
                    │
                    ▼
              ConvergeStore
                    │
             ┌──────┴──────┐
             ▼             ▼
          Local Store   Remote Store
```

Git 才是真正的共同 choke point。不是 Wanxiang process。

---

### §24 Projection

Authoritative state 只来自 events。运行时：events → fold → in-memory projection。查询读取 projection，不扫描完整历史。

Projection 可以被随时丢弃并从 event history 重建。

第一阶段不建立另一个 durable projection database。如果未来启动时间真的需要 checkpoint：checkpoint 也只能作为 append-only derived event / raw payload，并且删掉 checkpoint → 从完整 event history 仍能得到同一 projection。否则它就是第二数据库。

---

### §25 删除语义

Append-only 不存在 physical delete event。业务上的删除（CaseEvicted / SessionRetired / QaClosed / JobAbandoned）都是新 event。

Projection：fold tombstone → 当前 active view 不再出现对象。但历史仍存在。

LRU eviction 只表示 active Casebook projection 不再暴露该 Case，不表示删除历史 Case event/blob。

Git GC 只能清理：从未 CAS 成功的 orphan objects / 临时构造失败 objects。不得清理 committed event closure。

---

### §26 Student QA 纳入统一 Store + 全 Store 保密边界二选一

当前 Student QA 不再保留独立 durable filesystem store。改成事件：
```
StudentQaOpened / StudentQaQuestionAppended / StudentQaAnswerAppended / StudentQaClosed
```

正文可以 Git raw payload blob。Event 只保存 identity / causal metadata / payload_ref（§7.1）。

**保密边界必须二选一并写死（禁止"统一 storage 但不扩大 reader"却无物理解释）：**

自首版起，**整个统一 Store v1 的 confidentiality boundary = repository Git ACL**（不仅 QA）。凡进入 `refs/wanxiang/store` 及其可达 `payloads/` 的任何 durable fact（QA、Casebook snapshot、prompt/session material 等），拥有 repository Git object 读取能力的人即可通过 Git plumbing 读取；feature / application capability 仅限制产品 API 读取路径，不对 repository reader 提供额外保密。

- 选项 A（首版默认）：接受上述边界，并在 `perm-inspector` / `storage` 明确告知：统一 Store 不扩大 *产品 API* 的读者，但不再对 *repository reader* 额外保密。需要比 repository reader 更细的保密，一律视为 future work。
- 选项 B（需另起 Proposal）：对需保密的 payload 引入 **client-side encryption / key boundary**，Git 仅承载密文；解密能力由 capability 另行分发。该 Proposal 负责定义加密范围、密钥分发与轮换。

首版若不引入加密，则必须选 A。Application capability 不能限制 Git plumbing 读取。

---

### §27 现有 Journal 迁移

当前 Journal 中所有仍具有产品意义的 durable facts：逐条 decode → 映射为统一 canonical event → append 到新 EventStore。

已有 EventId 尽可能保持 identity。无法直接保持的 legacy fact 使用 deterministic import identity。禁止 migration 生成随机 identity 导致重复导入。

现有 blob：read → verify → write Git raw blob → event 改为引用 payload_ref（Persist-owned GitObjectId，见 §7.1；Domain 侧仅见 opaque PayloadRef）。

迁移不是把 old.ndjson copy 到 Git blob，而是把旧 storage 的语义事实一次性导入新 event model。

**新增迁移责任 — Causal Reconstruction（引入 §5 parents 后必做）：**

原 Journal 的 append 顺序本身曾隐式携带 happens-before；迁移若仅填空 `parents`，projection equivalence 可能在 fixture 上碰巧通过，却丢掉真正的因果约束。每个 legacy domain 的 migrator 必须显式给出 `Legacy order/relationship → parents` 映射并 deterministic，例如：
- 严格线性 stream：每条 legacy event 的 `parents = [前一条同 stream event]`（首条为空）。
- 跨 stream 的 durable effect（如 Requested→Accepted、Opened→Question→Answer→Closed）：若旧规范/旧 fold 已有明确因果，必须在迁移时恢复为跨 stream `parents` 边；不得以“同批导入”丢弃因果。

Migration 必须证明 **bytes 级确定性**：同一 legacy input 重跑 migrator 得到完全相同的 `EventId + parents + canonical bytes + payload_refs closure + root OID`，否则视为迁移欠确定。

---

### §28 迁移策略：ExplicitMigration + Clean Cutover

本 Change 不是 Compatible dual-format，也不是丢弃全部存量。

实施顺序：
```
1. Freeze legacy durable writers
2. Enumerate legacy stores
3. Read legacy facts with frozen old readers
4. Import canonical events/payloads
5. Build new projections
6. Compare semantic state
7. Atomically activate new EventStore
8. Disable all legacy writers/readers
9. Restart exclusively from EventStore
```

切换以后不存在：`if v1 then ... else if v2 then ...` / `read new store, fallback old store` / dual write / shadow journal。

Legacy reader 只允许存在于 one-shot migration tool / migration test。不得进入正常 runtime。

---

### §29 Migration 必须验证语义，而不是比较文件

迁移成功不能只证明 event count 差不多 / bytes 都复制了。必须比较迁移前后的 domain projection：

active jobs / prompt authority / context frames / coverage / review witnesses / manager facts / student QA / enforcer observations / pending durable effects

必须：`LegacyProjection == NewProjection`。

**并必须同时验证 bytes 级确定性：** 同一 legacy input 重跑得到 **完全相同 `EventId + parents + parents排序 + canonical bytes + payload_refs closure + root OID`**（§5.0/§5.2/§7.1 归一化后）。仅 `LegacyProjection == NewProjection` 通过但 bytes/root 非确定 → 迁移未完成，因果约束或 canonical 规则仍欠定义。

对无法建立等价关系的数据：migration fail closed。不得静默丢弃。

---

### §30 修改现有 Persist 正式语义

实施本 Change 时需要系统性重写：
- `docs/what/persist.md`
- `docs/shape/persist.md`
- `docs/how/persist.md`
- `docs/why/persist.md`
- `docs/proof/persist.md`

重点包括：

| 项目 | 变更 |
|---|---|
| Envelope | 删除 schema version 概念 |
| Append | 从 filesystem append 改成 Git raw object + root CAS |
| CommitUnknown | 改成 EventId + canonical root reconcile |
| Tail corruption | 不再以 mutable NDJSON file tail 为核心故障模型 |
| Old Schema | 删除长期 schema-version runtime |
| Blob | 删除 RuntimePath blob store，改 Git raw payload |
| Physical location | 改 Git common object database + canonical ref |
| Student QA | 删除特殊 durable filesystem backend |
| Projection | 继续保持 authoritative events → derived state |
| Durable Effect | 保留 event-sourced Requested/Accepted 原则 |

现有产品 invariant 如果仍正确就保留。本 Change 只清算 storage mechanism，不借机改掉无关业务语义。

---

### §31 `perm-inspector.md` 必须重写 Storage 部分

Casebook Proposal 当前不得继续拥有自己的：
- `refs/wanxiang/inspector-casebook`
- `refs/wanxiang/remotes/origin/inspector-casebook`
- custom fetch refspec
- reference-transaction sync protocol
- Casebook-specific tree schema
- revision + wall_clock LWW storage merge
- feature-owned lease push
- feature-owned Git hook

Casebook 只定义业务 event（InspectorCaseCaptured / InspectorCaseRefreshed / InspectorCaseAccessed / InspectorCaseEvicted）以及 CasebookProjection / freshness replay semantics / LRU active-view semantics / Bookkeeper behavior。

物理 persistence / synchronization 全部引用统一 EventStore。

Casebook 不再是"一个自己使用 Git Raw 的 feature"，而是"统一 EventStore 上的一个 domain"。

---

### §32 `rulebook.md` 必须收口 Storage 部分

Rulebook 的 120 rule directories / 240 authored Markdown 仍然是 repository authored source，不迁入 EventStore。

但运行期产生的 Observation / delivery history / first-full / repeat-identity evidence / squash history / coverage 只能通过统一 event store 持久化。

Proposal 不再描述自己的 journal encoding / blob format / durable list storage。只描述 Observation event semantics / Projection semantics。

---

### §33 `strength.md` 必须收口 Storage 部分

Strength 可以继续定义 CandidatePrepared / CandidatePromoted / CandidateRejected 等 durable facts。

但 FrameBundleRef / PredictorSnapshotRef / candidate material 全部使用统一 Git raw payload。

Proposal 不得假定现有 Journal 文件 / 现有 RuntimePath Blob / 另一种 Strength store。

Strength 只拥有 event semantics / promotion semantics / projection。不拥有 persistence substrate。

---

### §34 其它 Proposal 与未来 Proposal 的统一规则

所有当前 Proposal 都必须审计。如果某 Proposal 出现 persist / durable / store / database / journal / JSON state / state file / blob directory / Git ref / refspec / remote sync / revision / schema / version / migration format，必须分类：

- 如果它描述的是 dynamic durable state → 改成统一 EventStore
- 如果它描述的是 authored repository content → 继续普通 Git source
- **没有第三类**

以后新 Proposal 允许写：
```
本 feature 新增 FooCreated / FooRetired events
FooProjection 从这些 events fold
Foo events 进入统一 EventStore（RepositoryShared）
```
> 统一 EventStore 不提供 `LocalOnly` durable scope。凡进入 durable EventStore 的事实均为 `RepositoryShared`；真正 machine-local 的状态只能是 ephemeral / runtime state，不得进入 canonical ref。若未来确需 LocalOnly，需另起 Proposal 引入 filtered ref / replication projection，但首版不提供。

禁止写：
```
本 feature 建一个 foo.db
本 feature 建 refs/wanxiang/foo
本 feature 建 foo-v2.json
本 feature 自己装 fetch hook
```

---

### §35 Static Architecture Gate

新增 repository gate。生产代码中只有 Persist / Git infrastructure 允许出现：
- `refs/wanxiang/store`
- Git raw store primitives
- update-ref
- store object materialization
- store replication

其它 domain/application feature 出现 `refs/wanxiang/` / feature database / feature journal / feature state file / feature raw Git sync → 直接 RED。

同时扫描 Change Proposal：新的 storage-bearing Proposal 如果定义自己的 database / storage ref / schemaVersion / versioned storage namespace / feature sync protocol → 必须人工 Reviewer 判 REVISE。

---

### §36 Event Schema Version Gate

永久增加反例，禁止重新引入：
```
schemaVersion / schema_version / storageVersion / journalVersion / formatVersion
StoreV2 / JournalV2 / /events/v2/ / refs/wanxiang/store-v2
```

这里禁止的是 durable event/store protocol versioning。不是禁止产品发布版本号。

---

### §37 Git Gateway Gate

所有万象术 production Git process / Git library primitive 必须集中。禁止 Domain / Application / Session / feature Infrastructure 随意 `Process.Start("git", ...)`。

允许的 production ownership 必须收敛到少数明确模块，例如 `Infrastructure/Git/*` / `Infrastructure/Persist/*`。

静态 gate 必须能够证明：以后任何 feature 都无法偷偷再造一条 Git storage path。

---

### §38 Dumb Server Acceptance

必须用一个完全不知道万象术业务类型的测试 server 验证。Server 只看到：object ids / object bytes / refs / expected old oid / new oid。

测试 server 不链接：Wanxiang Domain / event codecs / projection / Casebook / Strength / Prompt / Manager。

仍必须完成：object upload / object fetch / ref CAS / lease rejection / retry。

如果 server 必须理解 Case / Candidate / Prompt / Observation → 设计直接失败。

---

### §39 Crash Proof

必须覆盖：
- payload object written → crash before event object
- event object written → crash before root CAS
- new root constructed → crash before CAS
- CAS succeeds → process crashes before seeing return
- CAS race
- retry after uncertain return

永久不变量：
- canonical ref 未引用 → 不属于 committed history
- canonical ref 引用 EventId → committed exactly once
- orphan Git objects 允许存在 → 后续 GC
- 不得从 orphan object 的存在推断业务事实

---

### §40 Corruption Proof

Git object hash verification 成为物理完整性边界之一。必须证明：

**StorageInvalid → 全局 fail closed（§5.3）：**
- corrupt blob → Git read fails / digest mismatch → fail closed
- missing committed payload / payload hash mismatch → fail closed
- malformed NDJSON event / 非 canonical bytes / 缺 LF → fail closed
- unknown authoritative event_type → fail closed
- duplicate EventId with different bytes → fail closed

**DomainConflict → deterministic conflict state + resolution 收敛（§5.3）：**
- where domain forbids fork：保留全部 competing events → projection 进入 `Conflict` → 仅当以全部 heads 为 `parents` 的 resolution event 出现后才离开 conflict。

**禁止**：skip bad event and continue。**严禁** 将自然 fork 判为 StorageInvalid 使 history 永久不可恢复。

---

### §41 Storage Growth 是明确 trade-off

Append-only event sourcing 意味着 committed history monotonically grows。

本 Change 不允许通过 rewrite history / delete old events / squash Git commit / drop old payload 伪装解决空间问题。

逻辑 eviction / retirement / squash / compaction 只改变 projection，不改变 durable history。

若以后需要真正 archival / retention：必须单独 Proposal，且不得偷偷破坏 event sourcing。

---

### §42 最终产品语义：Local Multi-Process → Universal Replica

统一 Persist 的目标不是构造一个新的分布式数据库。它只是把万象术已经存在的单机多 OpenCode process 语义自然推广到多机。

**Multi-Machine 不是一种新的运行模式。它只是 Multi-Process 跨越了机器边界。**

#### 42.1 Best-Effort Distributed Extension

跨机器后不引入强一致性承诺。尤其不承诺：global transaction / linearizability / distributed lock / consensus / leader election / always-online remote。

网络分区时两边都可以产生新的 append-only facts。重新连通后 KWayMerge → eventually convergence。

#### 42.2 机器没有持久身份语义

统一 Persist 不应把 durable state 的意义绑定到 hostname / machine id / installation id / process id / absolute checkout path。

真正 durable 的 identity 来自：repository identity / domain identity / session identity / event identity / Git object identity。

换机器本身不产生 migration generation / new storage namespace / forked database。

#### 42.3 Universal Session Access

OpenCode session export/import 集成完成后，最终用户体验目标是：任何已经同步该 repository 的开发机都可以访问该 repository 已 durable publication 的 Session。

```
Machine A: OpenCode session S → export → Store publication → Git convergence
Machine B: git clone/fetch/pull → Store convergence → discover S → import S → continue session
```

对用户而言，不应需要理解 S 最早在哪台机器创建、S 的 raw payload 在哪个 Git object、哪个 OpenCode process publication、经过哪几个 replica merge。

#### 42.4 "透明"不表示迁移 Live Process

Universal Session Access 指 durable continuation，不是 live process migration。不承诺迁移正在运行的进程内存 / open file descriptor / PTY state / 正在执行的 shell command / 未 publication 的 transient state。

#### 42.5 最终抽象

```
Logical Repository
│
├── Source Git History
│
└── Wanxiang Durable Universe
     │
     ├── domain events
     ├── payloads
     ├── observations
     ├── QA
     ├── Casebook
     ├── Strength
     └── OpenCode Sessions
```

它们使用同一个 Git object transport，但保持不同语义：Source → Git commits / branches；Wanxiang durable universe → raw objects / refs / append-only events。

#### 42.6 长期愿景（明确 scope 边界）

> 本节为 **long-term product consequence，非当前已证明语义**。§42.1–42.5 描述当前 EventStore 选择的自然延伸；完整 Universal Session Access 依赖未来的 `Repository Identity + Session Portability` Proposal（定义：新 clone 如何发现 custom store ref、首次 bootstrap 的离线双初始化、fork 是否为新 logical repository、origin URL 变更是否改变 identity 等）。在该 Proposal 落地前，§42 不得被解读为当前 Store 已提供可证明的 repository identity 协议。

**Forward-compatibility 边界（多机多版本共存）：** 不同机器可运行不同 Wanxiang 版本；旧 client 遇到它不认识的新 authoritative `event_type` 时，按 §5.3 属 **StorageInvalid → 全局 fail closed**，必须升级后才能继续 projection。此为 §5.2 vocabulary monotonicity + §5.0.1 payload 冻结 + “无 schemaVersion”演进的必然推论，不是缺陷；需在多机 best-effort 语义中作为前向兼容边界明确告知用户。

1. 单机单 OpenCode 是最简单的 replica topology。
2. 单机多 OpenCode 不改变语义，只是多个 process replica。
3. 多机多 OpenCode 不改变语义，只是 replica 跨越机器边界。
4. Git remote 不是 authority，只是 dumb rendezvous replica。
5. 网络故障不会破坏 local durable correctness。
6. 网络恢复后通过 k-way merge best-effort convergence。
7. Wanxiang dynamic state 不属于机器。
8. OpenCode Session 在 export/import 集成后也不属于机器。
9. 换机器、换 checkout 路径、重新 clone，不改变 logical durable identity。
10. 最终达到 Universal Session Access：开发上下文跟随 repository，而不是跟随开发机。

**预留**：`Repository Identity + Session Portability` Proposal 负责定义 durable repository identity 的来源与发现协议；在其完成前，本 Change 不对 identity 的具体机制做可证明承诺。

---

### §43 Completion Criteria

只有同时满足以下条件才允许 Completed：

- [ ] production 只有一个 durable EventStore
- [ ] dynamic durable facts 全部是 append-only events
- [ ] canonical event format = NDJSON
- [ ] Git object database 是唯一 durable bytes backend
- [ ] 只有一个 canonical store ref
- [ ] 无 feature-owned storage refs
- [ ] 无 RuntimePath blob backend
- [ ] 无独立 Student QA durable file backend
- [ ] 无 schemaVersion / storageVersion runtime
- [ ] 无 v1/v2 dual reader
- [ ] 无 dual write
- [ ] legacy state 已 ExplicitMigration
- [ ] migration 前后 domain projections 等价
- [ ] all committed payloads reachable
- [ ] EventId 可解决 CAS-return crash ambiguity
- [ ] GitGateway 是 production Git 必经入口
- [ ] dumb server 无 Domain dependency
- [ ] Casebook 删除自有 Git persistence protocol
- [ ] Rulebook runtime history 使用统一 events
- [ ] Strength durable material 使用统一 events/raw payload
- [ ] 所有其它 Proposal storage sections 已审计
- [ ] static architecture gates green
- [ ] unit green
- [ ] integration green
- [ ] migration tests green
- [ ] affected e2e green
- [ ] npm run check green

---

### §44 一票否决项

Reviewer 看到以下任意一个直接 `REVISE`：

1. 新增 feature-specific database
2. 新增 feature-specific durable JSON/state file
3. 新增 feature-specific Git canonical ref
4. 新增 feature-specific refspec / remote merge protocol
5. 新增 schemaVersion / storageVersion
6. 为兼容旧存储保留长期 dual read
7. 新旧 store dual write
8. migration 只比较文件数量，不比较 domain projection
9. projection 被当成第二 durable truth
10. 修改旧 event bytes
11. 删除历史 event 代替 tombstone
12. LRU / squash / compaction 物理删除 committed history
13. Git commit/branch/tag 被拿来表达 EventStore history
14. dumb server 开始理解 domain event
15. feature 绕过 GitGateway 自己执行 store Git 操作
16. 因为某 feature "比较特殊"，重新给它开一套 storage exception
17. Proposed feature 自己定义 storage mechanism，然后声称"底层以后再统一"
18. 迁移后正常 runtime 仍能打开 legacy store

---

### §45 最终所有权

```
Domain     owns: Event meaning / Invariants / Projection rules (+ parents / causal 约束声明)
             暴露给 Persist/Application 的 payload 引用为 opaque PayloadRef
             不得出现 GitObjectId / RootOid / StoreSnapshot / AppendCandidate
Application owns: when to append which event (含 parents 选择)
Persist    owns: canonical NDJSON + §5.0 canonicalization / append-only event store / DAG fold substrate
                   Git raw payload + §7.1 payload_refs closure / CAS publication (Absent CAS 统一)
                   StoreSnapshot / AppendCandidate / RootOid / GitObjectId 等物理概念
                   （无 LocalOnly scope；vocabulary monotonic 永久负担）
GitGateway owns: all Wanxiang-initiated Git transport / store replication / subject Git transport
             + hooks are correctness integration; incomplete → Git integration incomplete
             + 公开 ConvergeStore(remote)；内部允许私有 helper ConvergeStoreWithObservedRemote(remote, observedRemoteSnapshot) 以避免 hook 触发的 nested fetch（§14/§15）
Dumb Server owns: objects / refs / CAS / auth
Repository Git integration owns: common convergence boundary (external Git → ConvergeStore)
Feature    does NOT own: storage format / database / ref / schema version / remote protocol
```

**所有权红线**：`src/Wanxiangshu/Domain/EventStore*.fs` 不得 `open Infrastructure` / 引用 `GitObjectId` / `StoreSnapshot` / `GitRawStore`；`StoreSnapshot` / `AppendCandidate` / `RootOid` / `GitObjectId` 定义归 `Infrastructure/Persist`。静态 gate（§35）必须覆盖此边界。

---

### §46 最终裁决

以后讨论任何新的 durable feature，只问：
- 它新增什么 event？
- 它如何 fold？
- 它的 invariant 是什么？
- 它的 parents / causal 约束是什么？

**不再问**：
- 它的数据文件放哪里？
- 用 JSON 还是 SQLite？
- 要不要新建一个 ref？
- 怎么设计 v2 schema？
- remote 怎么 merge？
- 要不要装一个 hook？

这些问题已经由统一底座一次性回答。

最终结构必须保持：

```
Events are the truth.
NDJSON is the wire.
Append is the only mutation.
Git raw is the storage.
Projection is derived.
GitGateway is the only Wanxiang-initiated Git path; repository Git integration is the common convergence boundary.
Server is dumb.
There are no storage versions.
Committed semantic vocabulary is monotonic.
```

---

### §47 Acceptance Matrix（永久证明）

| 场景 | 必须成立 |
|---|---|
| Local append | remote eventually receives merged events |
| ordinary git fetch | remote events enter local; local events are pushed back (no LocalOnly durable scope) |
| ordinary git pull | same bidirectional convergence |
| ordinary git push | remote is fetched first; remote-only events preserved; merged Store pushed |
| external IDE fetch/push | same behavior |
| Wanxiang internal fetch | same ConvergeStore primitive |
| lease rejection | refetch + remerge + bounded retry |
| offline | local event remains committed; next Git activity retries convergence |
| user existing hooks | never overwritten |
| Wanxiang hook recursion | bounded/no recursive sync loop |
| Local={A,B}, Remote={A,C} | after any successful sync: Local=Remote={A,B,C} |

最关键的 permanent assertion：**There is no successful one-way Store synchronization.**

---

### §48 并发 Completion Invariant

统一持久化完成后必须机械证明：

- [ ] OpenCode 多进程无需 leader
- [ ] 每个 process 使用 frozen StoreSnapshot
- [ ] snapshot 不随其它 process publication 偷变
- [ ] process mutation 只产生 append-only delta
- [ ] 不存在 repository-wide process mutex
- [ ] 不存在 PrimaryProcess / WriterElection
- [ ] local CAS conflict 进入 k-way merge
- [ ] merge primitive 原生支持 N inputs
- [ ] merge 对允许 history associative / commutative / idempotent / deterministic
- [ ] 同 EventId 不同 bytes fail closed
- [ ] 不同 EventId 永不因 replica conflict 丢失
- [ ] domain conflict 在 fold 后处理
- [ ] projection 是 snapshot-local derived state
- [ ] 相同 merged snapshot 必须得到相同 projection
- [ ] external Git sync 使用同一 k-way merge
- [ ] remote convergence 使用同一 k-way merge
- [ ] process crash 不留下 durable process-state requirement
- [ ] 所有动态 durable domain 使用同一并发模型

最终并发公式：
```
Per Process → Frozen Snapshot → Append Local Events → K-Way Merge
→ Domain Validation / Fold → Git CAS Publication → Bidirectional Git Convergence
```

---

## 第二部分：保姆级详细开发指南

---

### Phase 0 — Inventory（禁止跳过）

**目标**：列出所有当前 durable writer 和所有 Proposed storage design。

**具体步骤**：

1. 创建一个 inventory 文档（可以是 Active work 的一部分），逐条列出：

| 类别 | 具体项 | 当前位置 | 当前格式 |
|---|---|---|---|
| Journal | NDJSON append | `RuntimePath` 下 | NDJSON + envelope |
| BlobStore | 内容寻址 blob | `RuntimePath/blob/` | SHA256 路径 |
| Student QA | 独立文件 | `.git/` private dir 下 | UTF-8 自然语言 |
| Prompt durable effects | Claimed/Submitted/Accepted | Journal fold | NDJSON |
| Context | PrefixSnapshot / ActivePrefixEpoch | Journal fold | NDJSON |
| Enforcer | BlogEntryCommitted / EnforcementProjection | Journal fold | NDJSON |
| Review | ReviewVerdictRecorded / ConfirmedReviewWitness | Journal fold | NDJSON |
| Orchestrator | ManagerJobCreated / Published 等 | Journal fold | NDJSON |
| Casebook（proposed） | Git tree ref | `refs/wanxiang/inspector-casebook` | Git objects |
| Rulebook（proposed） | Observation events | 未定 | 未定 |
| Strength（proposed） | CandidatePrepared 等 | 未定 | 未定 |

2. 对每个 Proposed storage design（`perm-inspector.md`、`rulebook.md`、`strength.md`、`js-capability-projected-tools.md`），标注其中涉及 dynamic durable state 的部分。

3. **没有完整 inventory → 禁止开始迁移。**

**验收**：inventory 文档存在且覆盖上表全部行。

---

### Phase 1 — RED Architecture Gates（先红后绿）

**目标**：先增加静态门禁，确保当前仓库/Proposal 中已有例子能证明规则确实会 RED。

**具体步骤**：

#### 1.1 新增 `scripts/checks/unified-store-gate.mjs`

检查项：

```javascript
// 1. feature-owned durable store gate
// 扫描 src/ 中非 Infrastructure/Persist/ 和非 Infrastructure/Git/ 的文件
// 如果出现 refs/wanxiang/ → RED

// 2. feature-owned refs/wanxiang gate
// 同上，更精确：只有 Persist/Git infrastructure 可以出现 refs/wanxiang/store

// 3. schema-version storage gate
// 扫描 src/ 中出现 schemaVersion / storageVersion / journalVersion / formatVersion
// 在 event/store 上下文中 → RED

// 4. direct Git bypass gate
// 扫描非 Infrastructure/Git/ 文件中出现 Process.Start("git" 或等价 Fable 调用 → RED
```

#### 1.2 新增永久 fixture

在 `tests/unit/verify/fixtures/` 下创建：

- `unified-store-feature-ref.fs`：包含 `refs/wanxiang/foo` → 必须 RED
- `unified-store-schema-version.fs`：包含 `schemaVersion` 在 event 上下文 → 必须 RED
- `unified-store-git-bypass.fs`：在非 Git infrastructure 文件中调用 git → 必须 RED

#### 1.3 验证

```bash
# 先故意让 fixture 存在，运行 gate，确认 RED
node scripts/checks/unified-store-gate.mjs
# 期望：exit 1，报告 fixture 中的违规

# 然后删除 fixture（或移到 tests 专用目录），确认正式代码 GREEN
node scripts/checks/unified-store-gate.mjs
# 期望：exit 0
```

**验收**：gate 存在、fixture 证明能 RED、正式代码 GREEN。

---

### Phase 2 — Git Raw EventStore 核心实现

**目标**：实现 canonical event codec、raw object writer、payload writer、root tree builder、single canonical ref、CAS append、EventId reconcile、projection fold。

**具体步骤**：

#### 2.1 Domain 层（纯类型，无 I/O）

新建 `src/Wanxiangshu/Domain/EventStore.fs`（仅保留业务语义；不得出现 Git 物理概念）：

```fsharp
// Event Envelope（无版本，含因果前驱；payload 引用为 opaque）
type PayloadRef = PayloadRef of string  // opaque handle，Domain 不知 Git OID
type EventEnvelope =
    { EventId: EventId
      StreamId: StreamId
      EventType: string         // additive vocabulary
      Parents: EventId list     // causal predecessors，先去重再按 EventId 文本序排序（§5.0）
      Payload: JsonValue        // canonical JSON；大正文通过 PayloadRef 间接引用
      PayloadRefs: PayloadRef list }  // Persist-owned 语义（见 §7.1），Domain 仅透传
// 禁止在此文件出现：GitObjectId / RootOid / StoreSnapshot / AppendCandidate
```

`StoreSnapshot / AppendCandidate / MergeInput / GitObjectId / RootOid` 归属 `Infrastructure/Persist`，见 §2.3–§2.4 与 §45 所有权红线。Domain 仅定义 event 语义、因果约束与 projection 规则。

#### 2.2 Persist/Infrastructure 层：K-Way Merge（Specification Oracle + Structural 实现）

```fsharp
// Specification oracle（纯函数语义，供契约测试对照，禁止作为生产 merge 算法指导）
module EventStoreMergeSpec =
    /// 基础 merge：append-only set union + identity dedupe
    /// 注意：merge 仅做集合并；业务顺序由 §5.1 的 DAG topological fold 决定，
    /// EventId 排序仅用于物理 canonicalization，不作为因果时序。
    let merge (inputs: MergeInput) : Result<EventEnvelope list, MergeError> =
        // 1. union all events by EventId（§10.6 oracle）
        // 2. same EventId + same canonical bytes（§5.0）→ one event
        // 3. same EventId + different canonical bytes → Error IdentityCollision → StorageInvalid（§5.3）
        // 4. 不同 EventId → 全部保留（含 DomainConflict，交 projection）
        // 5. deterministic canonical bytes 排序仅作物理 tie-breaker（按 EventId 排序）
        ...

/// Production 实现：structural tree merge（优先按 EventId 分片路径 union，
//  仅同 EventId 且 blob OID 不同时读 bytes 校验 canonical bytes 是否冲突，见 §10.6）
module EventStoreMerge = // 归属 Infrastructure/Persist
    let merge (inputs: MergeInput) : Result<StoreSnapshot, MergeError> = ...
```
```

#### 2.3 Infrastructure 层：Git Raw Store + Persist-owned Store 类型

新建 `src/Wanxiangshu/Infrastructure/Persist/GitRawStore.fs` + `StoreTypes.fs`：

```fsharp
// Persist-owned 物理类型（Domain 不得引用）
type GitObjectId = GitObjectId of string
type RootOid = RootOid of GitObjectId
type StoreSnapshot =
    { RootOid: RootOid }  // frozen snapshot，以 RootOid 为权威；无 EventId 全量集合
type AppendCandidate =
    { BaseSnapshot: StoreSnapshot
      NewEvents: EventEnvelope list          // Domain EventEnvelope（含 opaque PayloadRef）
      NewPayloads: (GitObjectId * byte[]) list } // Persist 侧实际 blob 写入
type MergeInput = StoreSnapshot list

// 能力端口（不直接依赖 libgit2 或 process git）
type IGitRawStore =
    abstract WriteBlob : byte[] -> GitObjectId
    abstract WriteTree : TreeEntry list -> GitObjectId
    abstract ReadObject : GitObjectId -> byte[] option
    abstract ReadTree : GitObjectId -> TreeEntry list option
    abstract ReadRef : string -> GitObjectId option
    abstract CompareAndSwapRef : refName:string * expectedOld:GitObjectId option * newOid:GitObjectId -> bool
    // 无 CreateRef：首次创建 = CompareAndSwapRef(expectedOld=None → Absent CAS，§9)
```

#### 2.4 Infrastructure 层：EventStore 实现

新建 `src/Wanxiangshu/Infrastructure/Persist/EventStore.fs`：

```fsharp
type IEventStore =
    abstract OpenSnapshot : unit -> StoreSnapshot
    abstract Append : StoreSnapshot * EventEnvelope list -> Result<StoreSnapshot, AppendError>
    abstract Refresh : unit -> StoreSnapshot
    abstract Merge : StoreSnapshot list -> Result<StoreSnapshot, MergeError>
    abstract Publish : AppendCandidate -> Result<StoreSnapshot, PublishError>
    abstract Converge : remote:string -> Result<StoreSnapshot, ConvergeError>
```

Append 实现核心逻辑（含 §9 Absent 统一与 §5.0/§7.1 归一）：
```
1. observe canonical root R0-or-Absent（从 refs/wanxiang/store 读取；Absent = None）
2. canonicalize event bytes（§5.0：UTF-8/无BOM/单LF/key排序/parents排序/数字&字符串归一）
3. write event/payload raw objects（WriteBlob；payload_refs 去重+排序，root payload set = closure）
4. construct root R1 = (R0-or-Absent) + new EventId-sharded event blobs + closure payloads（WriteTree）
5. CAS: CompareAndSwapRef("refs/wanxiang/store", R0-or-Absent, R1)  // Absent 亦单 CAS
6. CAS 失败 → 重新读取（含 Absent→present）→ 验证 EventId 是否已存在（分片路径高效查询）→ 不存在则基于新 root 重建 → bounded retry
```

#### 2.5 Projection Fold

```fsharp
module EventStoreFold =
    /// 从完整 event history 构造 projection（DAG topological fold，§5.1/§5.3）
    let fold (events: EventEnvelope list) : Result<Projection, FoldError> =
        // 1. 按 parents 构建 DAG，deterministic topological order（§5.0 排序仅物理 tie-breaker）
        // 2. 每 event 仅当全部 parents 已 fold 才 fold；环/缺失前驱/缺 payload → StorageInvalid → fail closed（§5.3）
        // 3. 按 stream + event_type dispatch 到各 domain fold；unknown authoritative type → StorageInvalid → fail closed
        // 4. 领域互斥并发 fork → DomainConflict：保留全部 heads，projection 进入 deterministic Conflict state（§5.3），
        //    仅当以全部 competing heads 为 parents 的 *Resolved event 出现后才离开 conflict
        ...
```

#### 2.6 单元测试

在 `tests/unit/persist/` 下创建：

- `event-store-merge.test.mjs`：验证 associative / commutative / idempotent / deterministic
- `event-store-append.test.mjs`：验证 CAS 成功/失败/retry
- `event-store-fold.test.mjs`：验证 unknown event fail closed
- `event-store-identity-collision.test.mjs`：验证同 EventId 不同 bytes → fail closed

**验收**：所有 unit 通过；`npm run build` 通过。

---

### Phase 3 — Dumb Server / Git Gateway

**目标**：建立唯一 Git transport path。不得先给 Casebook 单独做同步。

**具体步骤**：

#### 3.1 GitGateway

新建 `src/Wanxiangshu/Infrastructure/Git/GitGateway.fs`：

```fsharp
// 所有万象术自己发起的 Git 操作都必须经过这里
type IGitGateway =
    abstract Fetch : remote:string -> Task<Result<unit, GitError>>
    abstract Pull : remote:string -> Task<Result<unit, GitError>>
    abstract Push : remote:string * refspec:string -> Task<Result<unit, GitError>>
    abstract ConvergeStore : remote:string -> Task<Result<StoreSnapshot, ConvergeError>>
```

内部实现：
```
Fetch/Pull/Push:
  → 执行普通 Git 操作
  → 同时 transport refs/wanxiang/store（通过 custom fetch refspec）
  → reference-transaction hook 触发 ConvergeStore

ConvergeStore:
  → fetch remote store ref
  → read local canonical store ref
  → KWayMerge(local, remote)  // §10.6 structural tree merge；StorageInvalid→fail closed，DomainConflict→保留全部 heads 交 fold
  → validate（含 §7.1 closure、§5.0 canonical、§5.3 分类）
  → CAS local（§9 Absent 统一）
  → lease push merged root to remote

内部允许私有 helper：
  ConvergeStoreWithObservedRemote(remote, observedRemoteSnapshot)
  // 供 reference-transaction 复用“已观察到的 remote-tracking snapshot”以避免嵌套 fetch（§14）；语义仍为完整双向收敛
```

#### 3.2 Hook Dispatcher

新建 `src/Wanxiangshu/Infrastructure/Git/HookDispatcher.fs`：

```fsharp
// reference-transaction hook 入口
// 只关心 refs/wanxiang/remotes/origin/store 的 committed 状态变化
// 已在普通 fetch 中完成远端 ref 更新时，调用 ConvergeStoreWithObservedRemote 以避免嵌套 fetch（§14）

// pre-push shim 入口
// 在用户 branch push 前执行 ConvergeStore（完整 fetch→merge→CAS→lease-push）

// recursion guard
// WANXIANG_GIT_SYNC_ACTIVE=1 时 no-op
```

#### 3.3 Hook 安装规则

```
hook absent → 可安装万象术 shim
hook 已是万象术拥有 → 可幂等维护
hook 是用户/其它系统拥有 → 不覆盖、不 rename、不 patch → 记录诊断 → Git integration incomplete（禁止标为 acceleration disabled 静默降级）
```

#### 3.4 Dumb Server 测试

新建 `tests/integration/persist/dumb-server.test.mjs`：

- 使用一个完全不知道万象术业务类型的 bare Git repository 作为 remote
- 验证：object upload / object fetch / ref CAS / lease rejection / retry
- Server 不链接任何 Wanxiang Domain 代码

**验收**：GitGateway 是唯一 production Git 入口；dumb server 测试通过。

---

### Phase 4 — Legacy Migration

**目标**：构建 one-shot migrator。迁移当前存量。做 projection equivalence proof。

**具体步骤**：

#### 4.1 Freeze legacy writers

在 migration 开始前，确保所有 legacy durable writers 已冻结（不再有新写入）。

#### 4.2 Enumerate legacy stores

按 Phase 0 inventory 逐项读取。

#### 4.3 Read legacy facts

使用 frozen old readers 逐条读取。

#### 4.4 Import canonical events/payloads（必须重建 parents）

对每条 legacy fact：
```
1. decode 旧格式
2. 映射为统一 EventEnvelope（含 §5.0 canonicalization 与 PayloadRef opaque 化）
3. 确定 EventId（尽可能保持原 identity；无法保持的用 deterministic import identity；禁止随机）
4. 显式给出 Legacy order/relationship → parents 映射并 deterministic：
   - 线性 stream：parents = [前一条同 stream event]
   - 跨 stream 因果（如 Requested→Accepted）：恢复旧规范的跨 stream parents 边
   - 去重并按 EventId 文本序排序后编码（§5.0）
5. 如果有 blob payload：read → verify → write Git raw blob → 记录 GitObjectId / PayloadRef（含 §7.1 closure）
6. append 到新 EventStore（§9 Absent CAS；同集合同 root 幂等）
```

#### 4.5 Build new projections

从新 EventStore fold 出所有 domain projection。

#### 4.6 Compare semantic state

```
LegacyProjection == NewProjection
```

逐项比较：active jobs / prompt authority / context frames / coverage / review witnesses / manager facts / student QA / enforcer observations / pending durable effects。

对无法建立等价关系的数据：migration fail closed。

#### 4.7 Migration 测试（含 Determinism Proof）

新建 `tests/integration/persist/migration.test.mjs`：

- 构造 legacy Journal + BlobStore + Student QA 的 fixture
- 运行 migrator → 验证 `LegacyProjection == NewProjection`
- 验证 **bytes 级确定性**：同一 legacy input 重跑 → `EventId + parents + canonical bytes + payload_refs closure + root OID` 完全相同
- 验证 causal reconstruction：线性 stream 的 `parents` 链与跨 stream 因果边均被正确恢复
- 验证 crash window（migration 中途崩溃 → 重入 → 幂等；随机 identity → 失败）

**验收**：migration 测试通过；projection equivalence 证明存在。

---

### Phase 5 — Cutover

**目标**：删除正常 runtime 中旧 writer/reader。

**具体步骤**：

1. 删除 Journal file writer（生产路径）
2. 删除 Blob directory writer（生产路径）
3. 删除 StudentQaStore filesystem backend（生产路径）
4. 删除 legacy readers（生产路径）
5. 只保留 migration reader（在 one-shot migration tool 中）

**验收**：
- 正常 runtime 中不存在任何 legacy store 的 read/write 路径
- `npm run build` 通过
- 所有 unit/integration 通过

---

### Phase 6 — Existing Domain Migration

**目标**：所有现有 domain 改用统一 store。

**具体步骤**：

逐个 domain 迁移：

| Domain | 旧存储 | 新 event |
|---|---|---|
| Journal facts | NDJSON file | 统一 EventStore |
| Prompt durable effects | Journal fold | 统一 EventStore |
| Manager jobs | Journal fold | 统一 EventStore |
| Reviewer witnesses | Journal fold | 统一 EventStore |
| Context | Journal fold | 统一 EventStore |
| Companion / Blogger observations | Journal fold | 统一 EventStore |
| Strength candidates | Journal fold | 统一 EventStore |
| Student QA | 独立文件 | 统一 EventStore |
| Casebook | Git tree ref（proposed） | 统一 EventStore |

每个 domain 迁移后运行该 domain 的全部测试。

**验收**：所有 domain 测试通过；无 domain 仍使用旧存储。

---

### Phase 7 — Rewrite Proposed Storage Sections

**目标**：审计并修改所有 Proposed 中的 storage 部分。

**具体步骤**：

至少审计并修改：

1. **`changes/proposed/perm-inspector.md`**：
   - 删除 `refs/wanxiang/inspector-casebook` / custom fetch refspec / reference-transaction sync protocol / revision + wall_clock LWW storage merge / feature-owned lease push / feature-owned Git hook
   - Casebook 只定义业务 event + CasebookProjection + freshness replay + LRU
   - 物理 persistence / synchronization 全部引用统一 EventStore

2. **`changes/proposed/rulebook.md`**：
   - 运行期 Observation / delivery history / coverage 改成统一 event
   - 删除自己的 journal encoding / blob format / durable list storage

3. **`changes/proposed/strength.md`**：
   - FrameBundleRef / PredictorSnapshotRef / candidate material 改成统一 Git raw payload
   - 删除对现有 Journal 文件 / RuntimePath Blob 的假定

4. **`changes/proposed/js-capability-projected-tools.md`**：
   - 审计其中涉及 durable state 的部分（如果有）

**验收**：所有 Proposed 的 storage sections 已审计并修改；无 feature-owned storage mechanism 残留。

---

### Phase 8 — Full Proof

**目标**：运行完整 unit / integration / e2e / migration / Git transport gate。

**具体步骤**：

```bash
# 1. 静态门禁
node scripts/checks/spec.mjs
node scripts/checks/architecture.mjs
node scripts/checks/dsl-ownership.mjs
node scripts/checks/unified-store-gate.mjs

# 2. 构建
npm run build

# 3. Unit
node tests/unit/run.mjs

# 4. Integration
node tests/integration/run.mjs

# 5. E2E
node tests/e2e/run.mjs

# 6. Migration tests
node tests/integration/persist/migration.test.mjs

# 7. Dumb server tests
node tests/integration/persist/dumb-server.test.mjs

# 8. 全量
npm run check
```

**验收**：全部 GREEN。

---

### 附录 A：文件级实施地图

| 新建/修改 | 路径 | 职责 |
|---|---|---|
| 新建 | `src/Wanxiangshu/Domain/EventStore.fs` | EventEnvelope（含 PayloadRef opaque）/ Domain causal约束，仅业务语义 |
| 新建 | `src/Wanxiangshu/Infrastructure/Persist/StoreTypes.fs` | StoreSnapshot / AppendCandidate / MergeInput / GitObjectId / RootOid（Persist-owned） |
| 新建 | `src/Wanxiangshu/Infrastructure/Persist/EventStoreMerge.fs` | K-Way Merge（Spec oracle + structural tree merge） |
| 新建 | `src/Wanxiangshu/Infrastructure/Persist/EventStoreFold.fs` | Projection DAG fold（StorageInvalid vs DomainConflict） |
| 新建 | `src/Wanxiangshu/Infrastructure/Persist/GitRawStore.fs` | IGitRawStore 端口 + 实现（含 §5.0 canonical + §7.1 closure） |
| 新建 | `src/Wanxiangshu/Infrastructure/Persist/EventStore.fs` | IEventStore 实现（含 §9 Absent CAS） |
| 新建 | `src/Wanxiangshu/Infrastructure/Git/GitGateway.fs` | 唯一 Git transport 入口 |
| 新建 | `src/Wanxiangshu/Infrastructure/Git/HookDispatcher.fs` | reference-transaction / pre-push shim |
| 新建 | `scripts/checks/unified-store-gate.mjs` | 静态门禁 |
| 修改 | `docs/what/persist.md` | 删除 schema version；改 Git raw |
| 修改 | `docs/shape/persist.md` | 改 ownership |
| 修改 | `docs/how/persist.md` | 改算法 |
| 修改 | `docs/why/persist.md` | 改理由 |
| 修改 | `docs/proof/persist.md` | 改证明 |
| 修改 | `changes/proposed/perm-inspector.md` | 收口 storage |
| 修改 | `changes/proposed/rulebook.md` | 收口 storage |
| 修改 | `changes/proposed/strength.md` | 收口 storage |
| 新建 | `tests/unit/persist/*.test.mjs` | merge / append / fold / identity |
| 新建 | `tests/integration/persist/*.test.mjs` | migration / dumb-server / convergence |

---

### 附录 B：严禁的假修复（Review 直接 Reject）

1. 给 EventEnvelope 加 `schemaVersion` 字段
2. 创建 `refs/wanxiang/store-v2`
3. 保留 legacy Journal reader 在正常 runtime 中
4. 为 Casebook 保留独立的 `refs/wanxiang/inspector-casebook`
5. 在 merge 中使用 wall_clock LWW
6. 创建 `PullStore()` / `PushStore()` 单向 API
7. 在 dumb server 上运行 domain reducer
8. 为某个 feature 开 storage exception（"它比较特殊"）
9. 在 migration 中只比较文件数量
10. 在 projection 中引入第二 durable truth
11. 使用 Git commit/branch/tag 表达 EventStore history
12. 让 feature 绕过 GitGateway 直接执行 Git 操作
13. 在 hook 中覆盖用户已有的 hook
14. 创建 ProcessRegistry / WriterElection / PrimaryProcess
15. 把 K-Way Merge 做成有状态的第二运行时
16. 引入 `LocalOnly` durable scope / filtered replication（首版禁止）
17. 用 ordinal / sequence number 命名 authority 段
18. 用 EventId 排序表达业务时序（仅允许物理 canonicalization）
19. 把 `acceleration disabled` 当作 hook 安装失败的合法状态
20. 删除或改写 `EventFrontier` 重新引入 / 让 snapshot 背全量 EventId
21. 在 Domain 层出现 `GitObjectId` / `RootOid` / `StoreSnapshot` / `AppendCandidate`
22. 在生产 merge 中全量反序列化做 O(N) set-union（应 structural tree merge，Spec 仅作 oracle）
23. 在 committed root 中写入未被 `payload_refs` closure 引用的 payload
24. 为首次创建单独提供 `CreateRef` / 第二套 bootstrap 协议
25. 将 DomainConflict（合法并发 fork）判为 StorageInvalid / 全局 fail closed

---

### 附录 C：Code Review 时只问这八个问题

1. **这个新 durable fact 是 event 还是 mutable state？** 如果是 mutable state → REVISE。
2. **这个 feature 是否定义了自己的 storage ref / database / journal？** 如果是 → REVISE。
3. **merge 算法是否只做 set union + identity dedupe？** 如果涉及 LWW / timestamp 比较 → REVISE。
4. **snapshot 是否 immutable？** 如果 snapshot 会被其它 process publication 改变 → REVISE。
5. **同步是否永远双向？** 如果存在单向 pull 或 push 路径 → REVISE。
6. **server 是否 dumb？** 如果 server 需要理解 domain event → REVISE。
7. **migration 是否比较了 domain projection？** 如果只比较文件 → REVISE。
8. **切换后是否还能打开 legacy store？** 如果能 → REVISE。

---

### 附录 D：最终设计摘要

```
┌─────────────────────────────────────────────────────────┐
│                    Domain Layer                          │
│  owns: Event meaning / Invariants / Projection rules    │
│  defines: FooCreated / FooRetired / FooProjection       │
│  does NOT define: storage / ref / schema / sync         │
└────────────────────────┬────────────────────────────────┘
                         │ append event
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   Application Layer                      │
│  owns: when to append which event                       │
│  uses: IEventStore.Append / IEventStore.Converge        │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                    Persist Layer                         │
│  owns: canonical NDJSON (§5.0) / append-only events     │
│        DAG fold (§5.1/§5.3) / payload closure (§7.1)    │
│        CAS publish (§9 Absent) / StoreSnapshot          │
│  ref: refs/wanxiang/store → root tree                   │
│  merge: structural tree merge / Spec oracle ( §10.6)    │
│  no: schemaVersion / storeVersion / migration generation│
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   GitGateway Layer                       │
│  owns: all Wanxiang Git transport                       │
│        store replication / subject Git transport        │
│  hooks: reference-transaction / pre-push shim           │
│  recursion guard: WANXIANG_GIT_SYNC_ACTIVE              │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                    Dumb Server                           │
│  owns: objects / refs / CAS / auth                      │
│  does NOT know: Event / Projection / Domain / Wanxiang  │
│  any Git remote works: GitHub/GitLab/Gitea/bare/SSH     │
└─────────────────────────────────────────────────────────┘
```

**一句话**：万象术只有一个持久化系统；Git Raw 只是这个系统唯一的物理介质。

---

## Active work

**Original proposal**：冻结于上方（`changes/proposed/storage.md` 原文，1813 行，已移入本文件）。

**范围**：按 §0–§48 执行 ExplicitMigration + Clean Cutover，完成 §43 Completion Criteria 与 §47/§48 永久证明。

**Remaining work**：
- [ ] Phase 0 Inventory（独立文档或本节清单）
- [ ] Phase 1 RED Architecture Gates（`unified-store-gate.mjs` + fixtures 先红后绿）
- [ ] Phase 2 Git Raw EventStore 核心（Domain/Persist/GitRawStore/EventStore/Fold/Merge + unit）
- [ ] Phase 3 GitGateway + HookDispatcher + Dumb Server proof
- [ ] Phase 4 Legacy Migration（freeze/enumerate/import/parents重建/bytes确定性）
- [ ] Phase 5 Cutover（删除正常 runtime 旧 Journal/Blob/StudentQa file backend）
- [ ] Phase 6 现有 Domain 改用统一 Store
- [ ] Phase 7 重写 Proposed Storage Sections（perm-inspector/rulebook/strength）
- [ ] Phase 8 Full Proof（`spec`/`architecture`/`dsl-ownership`/`unified-store-gate` + build + unit/integration/e2e + migration + dumb-server + `npm run check`）
- [ ] Formal docs 重写（`docs/{why,what,shape,how,proof}/persist.md`）

**Blockers**：无（待实施中发现则追加）。

**Completion criteria**：见 §43 + §48；另以 `npm run check` 全绿为准。
