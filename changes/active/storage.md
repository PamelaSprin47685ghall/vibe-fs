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

[…1188ln elided…]
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


## Approved Amendments

### Amendment G3.5-A — Clean-break cutover; Student QA retired（Playbook §10）

**Trigger:** Universal G3 Student/Teacher/QA/SKILL deletion is DONE（`changes/active/universal.md` G3 exit；`scripts/checks/student-teacher-absence.mjs` green）.

**Supersedes / narrows original Proposal text that listed Student QA as a migrate-into-EventStore domain and that required `LegacyProjection == NewProjection` / legacy fixture migrators for retired surfaces.**

Frozen cutover boundary for this Active Change:

```text
Student QA is a retired domain (G3). Do not migrate it into EventStore.
Do not invent a successor Student-QA event vocabulary.

旧 Student QA / 旧 Journal / 旧 Blob / 旧 feature-owned store:
- 不要求可读
- 不要求可迁
- 不进入新 active domain projection
- 不作为新 EventStore ongoing vocabulary
- 不要求 LegacyProjection == NewProjection
- 允许丢弃或原地留存但 runtime 永不打开
- 代码与测试中的旧路径按 CleanBreak 删除，不得 silent 保留兼容 shim

禁止：
- dual-write
- fallback old store
- 临时兼容一层读旧格式
- 为证明 LegacyProjection == NewProjection 而写旧档 reader / importer
```

**Remaining-work consequence:** Phase 4 “Legacy Migration” is **not** a format-preserving Student-QA/Journal importer. If retained as a checklist item, it means only: delete or leave-unread old paths + prove new EventStore is the sole runtime durability. No legacy reader, no dual-write bridge, no LegacyProjection≡NewProjection suite for retired domains.

**Not in this Amendment:** implementing EventStore itself（that is G4 Phase work）.

**Work origin**：Playbook G3 exit green → activate Storage for G3.5/G4（path was Proposed; body Active claim ignored until this `git mv`).


### Phase 0 Inventory — DONE（G4U0；Amendment G3.5-A）

Per §0 / Phase 0 table / **Amendment G3.5-A**: Student QA is **retired / do-not-migrate**; old Journal/Blob may be left unread; no legacy importer / dual-write / `LegacyProjection≡NewProjection`.

| Surface | Location / format (today) | Code / proposal status | Disposition |
|---|---|---|---|
| **Journal NDJSON substrate** | `RuntimePath` → `<git-common-dir>/wanxiangshu-next/runtimes/<RuntimeId>.ndjson` | Live writer/reader | **delete** format + on-disk **leave-unread-clean-break**; no migrator |
| **RuntimePath BlobStore** | runtime dir + `blobs/<sha256>` | Live | **delete** path convention; large bodies **survive→EventStore** as `payload_refs` |
| **Journal domain facts (live)** | NDJSON → fold projections（Prompt/Fallback/Review/Execution/Orchestrator/Companion/Context/Host/ManagerLifecycle/Runtime） | Live | **survive→EventStore** |
| **Student QA private file** | was `StudentQaStore` / `QA.md` | Absent in `src/`；absence ratchet green | **retired / do-not-migrate** |
| **Casebook (proposed)** | feature-owned Git refs in perm-inspector | Proposal only | feature store **delete**; Case events **survive→EventStore** when built |
| **Rulebook (proposed)** | authored `resources/enforcer/*`; runtime Observation | authored leave; Observation → EventStore | no private journal/blob |
| **Strength (proposed)** | assumed Journal/Blob refs | Proposal only | **survive→EventStore**; delete Journal/Blob substrate assumptions |
| **CausalWaitBridge** | `.wanxiangshu/diagnostics/causal-waits.json` | diagnostic | **not EventStore** |

`scripts/checks/unified-store-gate.mjs`：Phase 1 delivered；Phase 3 cleared `GIT_BYPASS_ALLOWLIST`.

**Remaining work**：
- [x] Phase 0 Inventory（独立文档或本节清单；**不含** Student QA 作为 migrate target）
- [x] Phase 1 RED Architecture Gates（`unified-store-gate.mjs` + fixtures 先红后绿；wired in `scripts/check.mjs`；allowlist cleared in Phase 3）
- [x] Phase 2 Git Raw EventStore 核心（Domain/Persist/GitRawStore/EventStore/Fold/Merge + unit；Converge wired in Phase 3）
- [x] Phase 3 GitGateway + HookDispatcher + Dumb Server proof（`GitGateway.fs` / `HookDispatcher.fs` / `GitSubject.fs` / `ProcessGitRawStore.fs`；`EventStore.Converge` → gateway；`unified-store-gate` `GIT_BYPASS_ALLOWLIST` cleared；`tests/integration/persist/dumb-server.test.mjs` 7/7：object upload/fetch、two-client merge、lease rejection+retry）
- [x] Phase 4 Clean-break policy / order / ownership map / doNotBuild（**docs/policy only**；supersedes frozen §Phase4 migrator / LegacyProjection via Amendment G3.5-A + Phase 4 Active notes below；**no** code cutover yet）
- [ ] Phase 5 Cutover（正常 runtime 仅 EventStore；删除 Journal/Blob writers；旧 StudentQa file backend 不打开）
- [ ] Phase 6 现有 Domain 改用统一 Store（仅仍存活 domain；Application/Session 改写到 `IEventStore`）
- [ ] Phase 7 重写 Proposed Storage Sections（perm-inspector/rulebook/strength）
- [ ] Phase 8 Full Proof（`spec`/`architecture`/`dsl-ownership`/`unified-store-gate` + build + unit/integration/e2e + dumb-server + `npm run check`；**无** legacy migrator suite for Student QA）
- [ ] Formal docs 重写（`docs/{why,what,shape,how,proof}/persist.md`）

### Phase 3 — DONE（G4U8–G4U12；2026-08-10）

验收：
- GitGateway 是 Wanxiang-initiated Git transport 入口（Fetch/Pull/Push/ConvergeStore）
- HookDispatcher：reference-transaction + pre-push + recursion guard + install ownership
- GitSubject 收口直调 `git`；`GitTree`/`GitAdapter`/`RuntimePath` 改走 Subject/Gateway
- dumb-server integration：bare remote、无 Domain 链接；CAS / lease reject+retry 证明
- `node scripts/checks/unified-store-gate.mjs` OK；persist/git unit + dumb-server GREEN

### Phase 4 — DONE（G4U14–G4U16；2026-08-10）

**Status：** clean-break docs/policy + gates + leave-unread proof **DONE**. Code cutover / domain rewrite remain Phase 6 then Phase 5（unchecked）.

验收：
- Active notes：clean-break meaning / chicken-egg order Phase4→Phase6→Phase5 / ownership map / doNotBuild（G4U14）
- `unified-store-gate` scanners += `student-qa-revival` / `no-migrator` / `dual-write`；unit 18/18；`GIT_BYPASS_ALLOWLIST=[]`（G4U15）
- `tests/integration/persist/leave-unread.test.mjs` 4/4：stale `wanxiangshu-next` NDJSON+blobs planted；EventStore open/append/converge 不读旧档（G4U16）

### Phase 4 — Active notes（P4U1+P4U4+P4U5；docs/policy DONE）

**Status：** docs/policy **DONE**. Code cutover / domain rewrite remain Phase 5 / Phase 6（unchecked）.

#### Clean-break meaning（supersedes frozen §Phase4 migrator text）

Frozen proposal body above `## Active work` retains §Phase4 “Legacy Migration” / `LegacyProjection == NewProjection` / migrator steps **byte-for-byte** as historical text. **Do not delete or rewrite that frozen body.**

**Authoritative Active meaning** (Amendment **G3.5-A** + this section):

```text
Phase 4 ≠ one-shot migrator
Phase 4 ≠ legacy reader / importer
Phase 4 ≠ LegacyProjection ≡ NewProjection suite
Phase 4 ≠ dual-write bridge

Phase 4 (docs/policy) = lock clean-break cutover semantics + chicken-egg order + ownership lanes + doNotBuild
Phase 5 (later) = delete Journal/Blob production writers after consumers are gone
Phase 6 (before Phase 5) = rewrite surviving Application/Session domains onto IEventStore
```

Old Journal / Blob / Student QA / feature-owned on-disk history: leave-unread or discard; runtime never opens them; no format-preserving translation into EventStore.

#### Locked recommended order（chicken-egg）

Frozen numbering stays 4 / 5 / 6, but **execution order is locked**:

```text
1. Phase 4 — ratchets / docs / policy（THIS section；DONE）
2. Phase 6 — domain rewrite onto IEventStore（Application/Session stop calling AgentJournal）
3. Phase 5 — cutover delete Journal/Blob writers（Writer / Boot / NDJSON / blobs production paths）
```

**Forbid:** deleting `JournalWriter` / `Boot` / Journal substrate while Application or Session still call `AgentJournal`.

**Forbid:** Phase 5 cutover before Phase 6 consumer rewrite is complete.

Rationale: Journal deletion is blocked until every live consumer is rewritten; otherwise clean-break removes the only durability path mid-flight.

#### Phase 6 ownership map（disjoint lanes）

| Lane | Owns | Does NOT own | Notes |
|---|---|---|---|
| **Journal substrate** | `src/Wanxiangshu/Journal/*`（`AgentJournal` / `Writer` / `Boot` / folds / RuntimePath NDJSON+Blob） | EventStore semantics; proposal docs | Live today; **deletion blocked** until App+Session consumers rewritten |
| **App + Session consumers** | `src/Wanxiangshu/Application/**` + `src/Wanxiangshu/Session/**` call sites that read/append via `AgentJournal` | Persist Git primitives; Journal on-disk format | Phase 6 rewrite target → `IEventStore` |
| **Persist owners** | `Infrastructure/Persist/*` + `Infrastructure/Git/*`（`IEventStore` / GitRawStore / GitGateway / HookDispatcher） | Domain event vocabulary; Application when-to-append policy | Sole durable substrate after cutover |
| **Proposal docs** | `changes/active/storage.md` Active work；`changes/proposed/{perm-inspector,rulebook,strength,entry}.md` storage sections | Production code | Phase 4/7 docs only; no migrator obligation |

**Journal deletion rule:** substrate lane may be removed only after App+Session consumer lane no longer references `AgentJournal` / Journal writers.

#### doNotBuild（explicit；Active work）

```text
doNotBuild:
  - migrator / one-shot importer for old Journal / Blob / Student QA / feature stores
  - legacy reader / frozen-old-format runtime reader
  - dual-write（Journal+EventStore or Blob+payload parallel write）
  - LegacyProjection ≡ NewProjection equivalence suite / fixtures
  - fallback-to-old-store shims
  - “temporary compatibility layer” that opens retired on-disk formats
```

Any PR introducing the above is out of scope for G4 Active and must be rejected under Amendment G3.5-A.

**Blockers**：无（待实施中发现则追加）。

**Completion criteria**：见 §43 + §48，**并受 Amendment G3.5-A 约束**；另以 `npm run check` 全绿为准。


[Showing lines 1-389 and 1578-1966 of 1966; 1,188 middle lines (51.7KB) elided. Read artifact://1095 for full output]