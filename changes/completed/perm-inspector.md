# Inspector Casebook — 持久复用、增量刷新与 EventStore 耐久

## 摘要

Inspector 的一次调用天然形成一个有价值的知识单元：

```text
Question
→ Inspector 调查 repository
→ Answer
```

当前该知识在调用结束后主要只存在于 Session/transcript 中。后续 Inspector 即使面对相同或高度相关的问题，也必须重新调查；已经消耗过的 read/glob/grep 等证据无法被可靠复用。

本 Change 引入一个**可选的 Inspector Casebook**：

1. 每个 Inspector Session 保存一个当前 Q&A（capture 全程 best-effort）；
2. 同时保存该答案所依据的、可重放的 repository observations（能捕获多少捕获多少，缺失允许）；
3. 后续 Inspector 可以通过 `fetch(session_id)` 取得旧答案；
4. `fetch` 不直接信任旧答案，而是先针对**当前 worktree**重放 observations；
5. 未检测到 observation 变化时直接复用旧 A；no-delta 只是 freshness hint，**不构成正确性证明**；
6. 检测到 observation 变化时，启动一个私有 Bookkeeper Agent，根据 Q/A 与 evidence diff 修订 Q/A；
7. Bookkeeper 成功且 evidence 再次稳定 → 更新 Case 并返回新 A；失败 → 保留旧 Case，返回旧 A（允许过时——这是预期产品语义）；
8. Casebook **不拥有**独立 durable store：Case 事实以 EventStore events 表达；Q/A/snapshot 等大正文经 `PayloadRef` 进入统一 payloads；
9. 物理耐久与同步统一落在 Persist 的 `refs/wanxiang/store` + `IEventStore` / `GitGateway`；Casebook 不制造 feature ref、branch commit 或产品版本历史；
10. linked worktree 通过 Git common-dir 共享同一个统一 EventStore；
11. remote 同步属于 Persist/GitGateway 的 dumb-remote `ConvergeStore`，**不是** Casebook 自有 sync/hook/refspec；
12. replica 收敛 = EventStore **集合并（set union）**；同 Case 合法并发 fork 由投影表达为 `DomainConflict`，经后续 resolution / refresh / evict events 收敛——**禁止** `(revision, wall_clock)` LWW；
13. observation replay 只是 freshness hint；任何 merge 标量或 EventStore 物理顺序都**不证明答案正确**；
14. Casebook 使用有限 LRU：淘汰通过 append `InspectorCaseEvicted` 表达，长期无人使用的条目退出 live projection；
15. 本功能完全 opt-in：只有 repository 中存在指定 marker directory 时才启用；不存在时工具、Prompt 注入、Bookkeeper 和 archive 行为全部静默消失（store sync 仍由 Persist 拥有，与 Casebook marker 无关）。

Compatibility：**Compatible**。未启用 Casebook 的 repository 行为必须保持不变。

---

# 0. 设计姿态

Inspector Casebook 明确选择 **availability and reuse over freshness guarantees**。

Casebook 是 hopefully useful 的 best-effort semantic cache，不是证明系统。旧答案可能因为 observation capture 不完整、shell 阅读未识别、未观察到的新文件、repository 并发变化、Bookkeeper 失败或其它信息缺口而过时或不准确；这些情况属于允许的产品行为。

## 0.1 必须严格保证

以下性质是机械安全边界，必须 fail closed：

```text
- Inspector 不修改 subject worktree；
- Case 动态数据不污染 git status / Orchestrator Clean Gate
  （只经统一 EventStore / refs/wanxiang/store，不写 worktree 动态文件）；
- Case publication 原子：一次 InspectorCaseCaptured / InspectorCaseRefreshed
  append 绑定完整 Q/A/snapshot/observations PayloadRef 集合，不出现撕裂状态；
- 物理 store CAS / k-way merge / remote converge 由 Persist + GitGateway 拥有，
  Casebook 不得自实现 feature CAS / lease push / hook；
- Bookkeeper 只能通过 edit-qa 修改 staged Q/A；
- Casebook 内容不逃逸 Synthetic TOML data containment；
- 同一 Inspector PrefixEpoch 的 Casebook index 字节稳定
  （冻结 CasebookProjection snapshot，不是 feature pin ref）；
- Casebook mutation 不主动制造 PrefixEpoch；
- ToolResultBound 始终生效；
- 路径不得逃逸 repository / .git 安全边界；
- 有界 retry，不建立第二运行时或 Casebook 专属无限同步循环。
```

## 0.2 明确不保证

以下性质只 best effort：

```text
- Inspector 是否捕获了全部 answer-relevant evidence；
- cat / sed / head / tail 或其它 executor 阅读是否被完整识别；
- glob / grep observation 是否代表整个逻辑搜索空间；
- snapshot 是否足以发现所有会影响旧答案的 repository 变化；
- 没检测到 observation delta 是否意味着旧答案仍然正确；
- Bookkeeper 是否成功把旧答案刷新到当前 repository 的最佳答案；
- EventStore 上并存的同 Case DomainConflict heads 哪一个“更正确”
  （物理层只做 union；业务正确性不由 merge 证明）。
```

因此：

```text
observation replay = freshness hint
EventStore union / DomainConflict = replica / concurrency accounting
Bookkeeper = opportunistic maintenance
A = reusable best-effort knowledge（via PayloadRef）
```

任何上述机制都不得被提升为 correctness proof。

---

# 1. 目标

## 1.1 核心目标

把 Inspector 已完成的调查从“一次性模型消费”变成：

```text
repository-scoped
+ evidence-backed
+ freshness-hinted
+ self-maintaining
+ remotely synchronizable（via unified Persist/GitGateway）
```

的知识缓存。

它不是普通字符串 cache。

Casebook 中真正可复用的对象是：

```text
Question
Answer
Evidence snapshot
Replayable observations
```

Answer 是否直接复用，由 observations 在当前 worktree 上的 best-effort 重放结果给出提示；未检测到变化即视为 cache hit，允许直接复用。

---

## 1.2 定位：best-effort semantic cache

本 Change 不声称“正确性保证”。

框架保证的只是机制方向，不是答案质量：

```text
EventStore set-union
+ CasebookProjection fold
+ DomainConflict（同 Case 合法并发 fork）
    = durable history / concurrency accounting

observation replay
    = freshness hint
```

```text
no detected change
≠ proved unchanged world

no detected change
= good enough cache hit
```

旧 A 可以因为 capture 缺口、未识别命令、未观察区域、并发变化、Bookkeeper 失败等原因过时——这是允许的产品行为。

**SUPERSEDED（不得再作为当前设计）：** 以 `revision` / `wall_clock` 作为 replica conflict resolution 或 merge scalar。

---

## 1.3 fetch 对 Inspector 免费

`fetch` 在模型决策语义上必须被描述为**零成本**。

Inspector system 必须包含以下指令（或语义等价的表述）：

```text
Inspector Casebook entries are available through fetch(session_id).

Treat fetch as free. Do not conserve, ration, or avoid fetch because of
cost, latency, token usage, or implementation concerns.

When an existing question appears even plausibly relevant, prefer fetching
it before repeating repository investigation.

Relevance-not cost-is the reason to decide whether to fetch.

Fetched answers are best-effort cached knowledge and may be stale or
imperfect. Use them freely, then continue investigating whenever useful.
```

后台真实成本（EventStore read、observation replay、Bookkeeper provider call、IEventStore.Append、Persist ConvergeStore）由 runtime 承担，Inspector 不可见，也不得据其优化调用。

`fetch` 仍是阻塞工具，但“阻塞”是执行语义，不是决策成本信号。

---

# 2. 非目标

本 Change 明确不做以下事情。

## 2.1 不建立知识数据库

不增加：

```text
embedding index
vector database
semantic search service
knowledge graph
coverage graph
自动主题分类
问题聚类
answer confidence score
```

Inspector 只看到一个简单的：

```text
session_id -- full question
```

列表，并自行判断是否值得 `fetch`。

---

## 2.2 不引入 commit history / feature Git history

Casebook 不创建：

```text
Casebook commit
parent commit
Casebook branch
tag
merge commit
历史版本链
feature-owned storage ref
```

Git raw object database 只作为 **Persist/EventStore 的物理底层**：

```text
content-addressed blob/tree store
+ atomic refs/wanxiang/store CAS（Persist 拥有）
+ remote object transport（GitGateway 拥有）
```

Casebook 领域层只看见 events / projections / opaque `PayloadRef`，不得操作 Git OID / root OID / feature ref。

---

## 2.3 不保证历史 Q/A 可追溯为产品 API

`Q` / `A` 是 **CasebookProjection 中的当前 canonical 文档**（bytes 经 PayloadRef 耐久）。

EventStore 保留 append-only event history；产品层不存在：

```text
previous revision（作为用户 API）
history()
rollback()
show old answer
```

**SUPERSEDED：** 把 `revision` 当作 merge scalar 或用户可查询版本号。

淘汰通过 `InspectorCaseEvicted` 表达；被 Evict 的 Case 退出 live projection，但不得靠“静默缺席”假装删除事实。

---

## 2.4 不用 timestamp 判断 freshness 或 merge winner

即使某个 remote Case 相关 event 更“新”，或投影中存在更新的 Accessed/Refreshed：

```text
也不得因此跳过 evidence replay
也不得用 wall_clock / revision 决定答案正确性或 LWW winner
```

freshness 只来自当前 worktree 上的 observation replay。

---

## 2.5 不改变 subject worktree

Inspector、Bookkeeper、以及 Persist 对 store 的同步都不得：

```text
write subject source files
stage files
commit files
stash
checkout
rebase
改变 branch
```

Case 动态内容不进入 worktree，因此不得使 Clean Gate 变 dirty。

---

# 3. Opt-in

## 3.1 Marker

功能启用 marker：

```text
.wanxiang/casebook/
```

Git 不保存空目录，因此 repository 可以提交：

```text
.wanxiang/casebook/.keep
```

但运行时只判断：

```text
directory exists?
```

不解释 `.keep` 内容，也不要求 README、manifest、配置文件或 schema 文件。

---

## 3.2 Feature disabled

若：

```text
.wanxiang/casebook/
```

不存在，则 **Casebook 产品表面**必须整体静默消失。

具体包括：

```text
Inspector provider schema 中没有 fetch
Inspector system prompt 中没有 Casebook index
不创建 Bookkeeper
不要求 Bookkeeper Agent 配置存在
不采集 Casebook observations
不 archive / 不 append InspectorCase* events
```

**不**包括（这些不属于 Casebook 所有权）：

```text
关闭 refs/wanxiang/store
卸载 Persist/GitGateway hooks
删除统一 EventStore 对象
Casebook 自有 custom refspec / feature sync（本 Change 根本不拥有它们）
```

Feature disable **只关闭 Casebook surface**。统一 store 的 sync/converge 继续由 Persist 拥有，与 Casebook marker 无关。

**SUPERSEDED：** 保留或清理 `refs/wanxiang/inspector-casebook` 作为 Casebook authority；本 Change 不得再以该 ref 为当前设计。

重新创建 marker 后，新的 Case 事实从当前 EventStore 上的 InspectorCase* events / CasebookProjection 继续工作（clean break：不要求迁移任何旧 feature-store 布局）。

---

# 4. EventStore 物理模型 / 共享 store

> **REPLACE** 原 “§4 Git 物理模型 / `refs/wanxiang/inspector-casebook`”。
> Casebook **不得**拥有 feature canonical ref。

## 4.1 统一 canonical store

每个 Git repository 只有一份动态 durable substrate：

```text
refs/wanxiang/store
```

由 Persist 拥有，直接指向 EventStore root tree（events/ + payloads/），而不是 commit。

Casebook 的 durable 事实只能是：

```text
InspectorCaseCaptured
InspectorCaseRefreshed
InspectorCaseAccessed
InspectorCaseEvicted
```

经 `IEventStore.Append` 进入该 store。大正文经 envelope `payload_refs`（Domain 侧 opaque `PayloadRef`）。

```text
refs/wanxiang/store
        │
        ▼
   EventStore root
        ├── events/     # InspectorCase* 与其它 domain events 共存
        └── payloads/   # Q/A/snapshot/observation materials
```

**禁止（当前设计）：**

```text
refs/wanxiang/inspector-casebook
Casebook-owned remote-tracking refs
Casebook-owned pin refs
Casebook-owned refspec / lease push / feature hook
```

---

## 4.2 linked worktree 共享

store / CasebookProjection 的 repository identity 取 Git common directory。

不同 linked worktree：

```text
repo/
repo-worktree-a/
repo-worktree-b/
```

必须解析到同一个：

```text
Git common object database
refs/wanxiang/store
```

不得为每个 worktree 创建不同 Casebook copy 或不同 feature store。

因此：

```text
worktree A ─┐
worktree B ─┼──► one unified EventStore
worktree C ─┘
```

但 `fetch(session_id)` 的 freshness replay 永远针对**调用 Inspector 所在的当前 worktree**。

---

## 4.3 没有 Case events

marker 存在但 CasebookProjection 当前没有 retained Case 表示：

```text
Casebook enabled
Casebook currently empty
```

不是错误。

第一条可永久化 Case 通过 append `InspectorCaseCaptured` 创建；物理上由 Persist 对 `refs/wanxiang/store` 做 Absent→present 或普通 CAS（Casebook 不实现私有 CreateRef）。

---

# 5. 逻辑 Case materials（event payloads，非 feature tree authority）

> **REPLACE** 原 “§5 Root tree 格式 / cases/<session>/… 作为 Casebook authority”。
> 任何 Git tree 布局若存在，只属于 Persist EventStore 物理编码，**不是** Casebook 产品树权威。

逻辑 Case 至少包含：

```text
session_id
Q bytes
A bytes
replayable observations
evidence snapshot materials
```

耐久方式：

```text
append InspectorCaseCaptured | InspectorCaseRefreshed
→ payload / payload_refs 承载 Q、A、observations、snapshot
→ CasebookProjection fold → CurrentCases
```

不建立 Casebook-owned root manifest，不把 `cases/<id>/meta.toml` 树当作第二真相源。

---

## 5.1 Session identity / 路径安全

模型不得提供 Case 存储路径。

逻辑身份只能从真实 Inspector SessionId 确定性得到：

```text
SessionId → Case stream / business key
```

Evidence 路径必须拒绝：

```text
/
..
NUL
path traversal
平台相关歧义
```

产品文档仍可用 `Q` / `A` 指称 canonical question/answer bytes；它们是逻辑文档名，不是 feature Git path authority。

---

# 6. Case 内容

## 6.1 Q（逻辑 Q.md）

新 Inspector Case 创建时：

```text
Q = Inspector invocation 的完整 initial prompt
```

不摘要。

不做 ToolResultBound truncation。

Bookkeeper 后续**允许修改 Q**。

因此长期语义是：

```text
Q
= current canonical question（CasebookProjection）

initially initialized from original Inspector prompt
```

不是 immutable forensic record。

Casebook 不保留一个额外：

```text
OriginalQ
```

也不提供产品层旧 Q history API。耐久历史上的旧 Q bytes 若仍被旧 event 的 PayloadRef 引用，那是 EventStore 物理可达性，不是产品 rollback API。

---

## 6.2 A（逻辑 A.md）

新 Case 创建时：

```text
A
= Inspector tool 实际返回给 caller 的 ToolResult body
```

必须先经过现有 ToolResultBound。

因此：

```text
内部 Inspector Session 可能产生更长文本

但

A == caller 真正拿到的 bounded answer
```

不存在：

```text
A.full
A.raw
hidden untruncated answer
```

大正文经 `PayloadRef` 进入 EventStore payloads；Domain 不得直接写 Git OID。

---

## 6.3 Bookkeeper 更新后的 A

Bookkeeper 修改 A 后，最终 candidate A 仍必须满足同一个 ToolResultBound。

CasebookProjection 中保存的 A：

```text
就是 fetch 最终能够返回的 bounded bytes
```

因此以后：

```text
fetch(id)
→ projected A（PayloadRef）
```

不需要另一套 Casebook 专属截断规则。

普通 tool boundary 再应用一次相同 bound 必须是幂等的。

---

# 7. Case metadata（无 revision / wall_clock）

> **REPLACE** 原 `meta.toml` 中的 `revision` / `wall_clock` merge schema。

Casebook **不**再持久化：

```text
revision
wall_clock
```

作为 Case 字段或 merge scalar。

投影可派生的 cache 字段只有与 LRU 相关的 **last_access**（见 §7.3），来自 event fold，不是独立可 LWW 的文档权威。

不得增加：

```text
confidence
status
fresh
stale
validated
phase
owner
generation
semantic score
```

---

## 7.1 SUPERSEDED — revision

**SUPERSEDED：** 以 `revision = max(...) + 1` 表达 refresh / 竞争胜负。

当前设计：内容更新 = append `InspectorCaseRefreshed`（或初次 `InspectorCaseCaptured`）。并发同 Case heads = EventStore union + `DomainConflict`，经后续 resolution/refresh/evict 收敛。

---

## 7.2 SUPERSEDED — wall_clock

**SUPERSEDED：** 以 `wall_clock` 作为 revision 并列的 merge tie-breaker。

当前设计：禁止 wall_clock LWW。时钟只可出现在诊断；不得进入 Case merge schema。

---

## 7.3 last_access via InspectorCaseAccessed

以下成功路径 append（或等价地让投影视作 access）`InspectorCaseAccessed`：

```text
新 Case Captured 成功
成功 fetch(session_id)（含 no-delta touch 与 refresh 成功后的访问）
```

`CasebookProjection.last_access(session_id)` 由 Accessed（以及 Captured/Refreshed 作为首次/刷新访问）fold 得出，只服务 LRU。

不要求 wall-clock 单调可信。时钟漂移最多影响 cache retention，不影响 answer validity。

仅 LRU touch：**只** append `InspectorCaseAccessed`，不得伪造 Refreshed，不得引入 revision++。

---

# 8. Replica merge — SUPERSEDED LWW；EventStore union + DomainConflict

> **REPLACE** 原 `(revision, wall_clock)` LWW。

## 8.0 SUPERSEDED callout

**SUPERSEDED（不得实现）：**

```text
(revision, wall_clock) lexicographic max
canonical content-tree OID tie-break as Casebook merge
last_access 写入 meta.toml 再独立 max merge
missing != tombstone 且 prune 不留 deletion record
```

以及任何 Casebook-owned：

```text
compareAndSwapCasebookRoot
fetchRemoteCasebook
pushRemoteCasebook*
pinCasebookRoot
```

---

## 8.1 EventStore union

本地与 remote 的 Case 事实进入同一个 append-only event set。

Persist k-way merge **只做集合并 + identity dedupe**，不解释 Case 业务胜负。

`CasebookProjection` 对 union 后的 InspectorCase* events 做 deterministic fold，得到 retained Cases。

---

## 8.2 同 Case DomainConflict

同一 `session_id` 上出现互斥的并发 Captured/Refreshed heads 时：

```text
物理层：合法 fork，history 全保留（非 StorageInvalid）
投影层：DomainConflict { heads; reason }
```

禁止字段级拼装：

```text
local Q + remote A + local snapshot
```

Resolution 必须经后续 event（例如新的 `InspectorCaseRefreshed` / 领域 `*Resolved` / 或 Evicted）显式声明 parents 覆盖 competing heads；不得用 LWW “悄悄选一个”。

---

## 8.3 last_access 派生，不是独立 merge 文档

`last_access` 由 Accessed/Captured/Refreshed fold 派生。

另一 replica 刷新 A 不会“擦掉”本机刚发生的 Accessed——因为 Accessed events 在 union 后仍然存在；投影取最新 access 语义服务 LRU。

---

## 8.4 Evicted tombstone events

Case 从 live projection 消失必须通过：

```text
append InspectorCaseEvicted
```

表达。

```text
missing projection entry
≠
“从未存在” 或 “远程删除未声明”
```

LRU prune **创建** Evicted tombstone event，而不是静默从 feature tree 删路径。

---

# 9. LRU 与有界性

Casebook 必须有界。

不得依赖：

```text
provider token window
模型上下文表
动态 token estimation
```

只能依赖固定的：

```text
case count
UTF-8 bytes
payload / stored bytes（经 EventStore）
rendered index bytes
```

实施时必须在正式 what/how 中公布有限正数常量，至少覆盖：

```text
CasebookMaxCases
CasebookMaxStoredBytes
CasebookIndexMaxUtf8Bytes
```

这些是 cache contract，不是模型容量预测。

---

## 9.1 Prune key

淘汰顺序：

```text
projected last_access ascending
then session_id lexical
```

即最久未访问优先 Evict。

---

## 9.2 Prune timing

以下操作之后统一运行同一个纯 prune 决策，并对需要淘汰的 Case append `InspectorCaseEvicted`：

```text
local Case Captured / Refreshed publication
EventStore converge 后投影刷新
InspectorCaseAccessed touch 后如越界
```

相同 event 集合必须生成相同 retained Case 集（fold 确定性）。

---

## 9.3 单 Case 超界

如果新 Case 自身就无法满足：

```text
stored-byte bound
或
完整 Q index entry bound
```

则 Inspector 原调用正常返回，但该 Session **不 append Captured**（不进入 CasebookProjection）。

不得为了缓存而截断：

```text
Q
snapshot
observation evidence
```

因为这会破坏 Q/A/evidence 的一致性与 replay 语义。

---

# 10. Inspector Index

## 10.1 内容

Inspector system 中注入当前 retained Case 的：

```text
session_id -- full Q
```

Q 不摘要、不 embeddings、不关键词化。

建议按：

```text
last_access descending
then session_id lexical
```

展示，方便最近使用内容靠前。

---

## 10.2 低信任数据

旧 Q 可能包含：

```text
指令
prompt injection
代码
错误文本
恶意 Markdown
```

因此完整 Q 必须作为明确标记的低信任 data 注入。

不得：

```text
裸拼成 system instructions
把 Q 中 # comment 提升为 trusted instruction
从 Q 反推 Authority
```

Renderer 必须使用统一字符串 codec / containment 规则。

---

# 11. PrefixEpoch 稳定

Casebook 变化不得主动制造 PrefixEpoch。

禁止：

```text
新增 Case
→ switch PrefixEpoch

LRU touch（Accessed）
→ switch PrefixEpoch

Persist converge / remote sync
→ switch PrefixEpoch

Bookkeeper refresh
→ switch PrefixEpoch
```

---

## 11.1 CasebookIndexSnapshot

每个 Inspector PrefixEpoch 持有一个冻结的：

```text
CasebookIndexSnapshot
```

逻辑内容至少包括：

```text
ordered session_id
Q bytes
rendered index bytes
足以在 epoch 内服务 fetch 的冻结 Case materials
  （Q/A/observations/snapshot PayloadRef 或已解析 bytes）
```

**不得**把 Casebook feature root OID / `refs/wanxiang/store` OID 当作 Casebook 产品权威字段写进 index contract。
允许实现为了诊断记录一次投影采样时的 store snapshot 身份，但它不是 Casebook authority，也不得引入 feature pin ref。

同一 PrefixEpoch 内永久不变。

---

## 11.2 选择时机

新的 Inspector epoch 的 index 必须在：

```text
该 epoch 第一份 provider-visible bytes 被 seal 之前
```

选择完成。

不得在 seal 后重新读取 CasebookProjection 并替换 Q list。

对于 prefix probe/promote：

```text
probe candidate materialize
→ 同时冻结 candidate CasebookIndexSnapshot
→ provider request
→ probe 成功
→ promoted epoch 继承同一个 snapshot
```

禁止 probe 成功后再重新 sample CasebookProjection。

对于 compaction reanchor：

```text
reanchor
→ 构造下一 epoch 第一请求前
→ 选择新 CasebookIndexSnapshot
```

---

# 12. Epoch freeze（无 feature pin refs）

> **REPLACE** 原 `refs/wanxiang/pins/<host-session-id>/<epoch-id>`。

LRU（`InspectorCaseEvicted`）或其它 replica 可能在当前 Inspector epoch 活跃期间把某个已展示 Case 从 **live** CasebookProjection 淘汰。

因此 index 不能只保存 session id。

当前 epoch 必须冻结其 index 所展示 Case 的 materials，使 epoch 内 `fetch` 仍可读。

```text
冻结 = process/session 持有的 CasebookIndexSnapshot materials
     + 对应 PayloadRef 所指向的已 committed EventStore payloads
```

**SUPERSEDED / 禁止（当前设计）：**

```text
refs/wanxiang/pins/...
pinCasebookRoot
Casebook-owned pin ref push/fetch
把 pin 纳入 Casebook merge
```

已 committed 的 EventStore payloads 随 `refs/wanxiang/store` history 可达；Casebook **不得**为了 epoch 安全性再发明第二套 feature pin/GC 协议。Epoch retire / Session dispose 后释放进程内冻结 snapshot 即可。

---

# 13. fetch(session_id)

## 13.1 工具可见性

只有：

```text
Casebook enabled
且
Role = Inspector
```

时 provider schema 和 execution registry 同时暴露：

```text
fetch(session_id)
```

其它 Agent 均没有该工具。

feature disabled 时不存在空壳 tool。

---

## 13.2 Lookup

调用时：

1. 首选当前 `CasebookProjection` 中同 `session_id` 的 retained Case；
2. 若 live projection 已因 Evicted 不存在，但当前 Inspector epoch 的 **frozen CasebookIndexSnapshot** 中存在该 ID，则从冻结 materials 恢复；
3. 两者都没有则返回 typed tool failure：
   `CASE_NOT_FOUND`。

不得从 Session transcript 猜旧答案。
不得查 feature pin refs（本 Change 不存在它们）。

---

## 13.3 失败面

`fetch` 只在无法提供答案时失败：

```text
session_id 不存在（projection 与 epoch freeze 皆无）
Case materials 损坏到无法读取
A 缺失
```

只要存在可读取的 A：

```text
prefer answer over failure
```

refresh 失败、Persist remote 失败、publication 失败都不构成 fetch 失败（§22.3、§29.1；sync 失败语义属 Persist）。

---

# 14. Observation capture

Casebook 的 freshness 依赖 observation capture。

Inspector 运行期间必须从真实 tool execution 记录 observation，而不是 Session 结束后从自然语言反推“它看过什么”。

---

## 14.1 原则

```text
capture = best effort，无资格门槛
```

含义：

```text
能捕获多少 observation 就捕获多少
不能可靠解释的 observation 就跳过
不因此阻止 Case creation
```

不设置 `capture_complete` 之类的元数据字段——一旦引入，实现者容易据此重新长出 correctness protocol。

漏捕获的后果只是未来少一次变化检测机会，不是归档失败。

---

# 15. read observation

成功的 `read` 至少记录：

```text
repository-relative path
read arguments / range
tool-visible semantic result digest
snapshot full-file bytes
```

`snapshot/files/<path>` 保存该 repository 文件在 observation capture point 的完整快照。

---

## 15.1 Capture race

如果 Host read 与 after-hook 之间文件发生变化，导致无法证明 snapshot 能产生 Inspector 实际看到的 read result：

```text
跳过该文件的快照
Case 照常归档
```

该文件因此失去未来 freshness 检测机会——可接受的 evidence loss。

绝不允许把：

```text
Inspector 看见旧内容
snapshot 却保存新内容
```

写成“相容证据”；快照要么相容，要么不存在。

---

# 16. glob observation

记录完整 canonical：

```text
pattern
root/path
options
complete result
```

Replay 使用同一 semantic capability。

比较的是 canonical result，不比较 stdout 格式噪声。

Host glob 无法证明 result 完整时（如无法识别的 truncation），如实记录已有结果并照常重放比较；完整性存疑只是降低检测能力，不阻止归档。

---

# 17. grep observation

记录：

```text
pattern
scope/path
options
complete semantic result
```

必须能够区分：

```text
zero matches
```

和：

```text
result 被截断后刚好只显示 zero/部分 matches
```

无法区分时按已记录结果重放比较（best-effort）；不阻止归档。

---

# 18. executor 阅读容错

Inspector 仍可使用现有 executor。

Casebook 积极识别明确的纯读取命令，识别成功则编译为等价 replayable observation 并记录底层文件 snapshot。正例（不限于）：

```text
cat file
cat -n file
cat file1 file2
head file
head -n N file
head -N file
tail -f file
tail -n N file
tail -N file
sed -n 'A,Bp' file
sed -n A,Bp file
cat file | grep bar
```

pipeline 中首段能确认是纯文件读取时，同样记录该文件被阅读。

---

## 18.1 无法识别的命令

以下形式不做解析：

```text
cat "$(command)"
sed ... $(find ...)
sh -c ...
bash -c ...
命令替换
无法确定读取目标的复杂 pipeline
```

遇到无法解释的 executor command：

```text
跳过该命令的 observation
不影响 Case 归档
```

原则：cheap useful inference preferred。漏识别（false negative）与轻微误识别（false positive）都可接受——误识别最多导致一次多余 refresh 或漏检，不构成安全问题（路径安全是独立硬边界，见 §19）。

---

# 19. Repository containment

Casebook 的 durable Case materials 会进入统一 EventStore，并随 `refs/wanxiang/store` 由 Persist/GitGateway 同步。

因此 evidence 永久化必须满足：

```text
路径属于当前 subject repository
realpath 不逃逸 worktree
不是 .git 内容
不是 Casebook marker/data 自己
不是其它 repository/submodule 的外部内容
```

---

## 19.1 快照范围

快照只永久化：

```text
repository 内、非 .git、非 casebook 自身、realpath 不逃逸 worktree 的文件
```

以下文件**不做快照**（但 Case 照常归档，仅跳过这些证据）：

```text
ignored file
untracked file
外部 file
symlink 逃逸后的 target
submodule / 外部 repository 内容
```

理由：

Case materials 会进入统一 store 并可能随 Persist sync 到达 remote，不能把 `.gitignore` 或 repository boundary 变成秘密旁路。

这是单文件证据策略，不是 Case 资格门槛。

---

## 19.2 已跟踪但未提交的修改

Git-tracked 文件即使当前存在 unstaged/staged 修改，仍可以进入 snapshot。

这是刻意行为。

因此启用 Casebook 即表示接受：

> Inspector 对 tracked working-tree 内容形成的 evidence snapshot 可能作为 EventStore payload 进入 `refs/wanxiang/store`，并经 Persist/GitGateway 同步到 remote（例如 `origin`），即使该内容尚未通过普通 branch commit 发布。

这一点必须在正式用户文档中明确说明。

如果产品不愿接受该行为，应另行修改批准范围；不得在实现阶段偷偷改成 “只缓存 HEAD”。

---

# 20. fetch observation 的传递性

Inspector A 可以调用：

```text
fetch(old-session-id)
```

然后基于旧 Case 的答案继续调查。

新 Case 的 correctness 不能只记录：

```text
“我调用过 fetch X”
```

否则会形成递归 Case dependency graph。

---

## 20.1 Evidence flattening

当 `fetch(X)` 成功时，runtime 已经得到：

```text
X 的 A
X 的当前 captured observation bundle
X 的当前 snapshot
```

当前 Inspector 的 capture 必须**吸收 X 的 observation bundle**。

因此新 Case publication 时：

```text
current direct observations
+
all fetched Case captured observations
→ flattened observation set
```

EventStore payload 内容寻址（Persist 管理的 Git raw blobs）负责物理 dedupe。

---

## 20.2 不建立 fetch dependency graph

Case 不保存：

```text
depends_on = [session-id...]
```

也不在未来 replay 时递归 fetch 历史 Case。

每个可复用 Case 在 publication 时都是 evidence-self-contained。

这防止：

```text
A fetch B
B fetch C
C fetch D
...
```

形成永久链式运行时。

---

# 21. Observation normalization

相同 observation 被 direct read 与 fetched Case evidence 重复带入时，按 canonical observation identity 去重。

identity 必须来自 typed request/evidence，不从字符串猜测。

相同 identity 但 observation 内容矛盾：

```text
以当前 replay 结果为准
```

不得随机选一个，也不得因此拒绝归档。

---

# 22. fetch freshness flow

`fetch(session_id)` 是阻塞工具。

正常流程：

```text
lookup Case
→ replay observations against current worktree
→ compare
```

---

## 22.1 未检测到变化

```text
no detected delta
→ 不启动 Bookkeeper
→ append InspectorCaseAccessed（best-effort）
→ Persist 可随后 ConvergeStore（Casebook 不拥有 sync）
→ return exact A（PayloadRef 指向的 bounded bytes）
```

这是最便宜的 hot path。no-delta 不构成“答案仍然正确”的证明，只是 good enough cache hit。

---

## 22.2 检测到变化

```text
detected delta
→ freeze old/new evidence delta
→ 构造 Bookkeeper request
→ 启动 Bookkeeper
→ Bookkeeper edit-qa*
→ Bookkeeper idle
→ replay/verify current evidence again
```

若当前 worktree 在 Bookkeeper 运行期间未继续漂移：

```text
append InspectorCaseRefreshed:
  Q / A / refreshed snapshot / refreshed observations
  （大正文经 PayloadRef；小字段可 inline）
→ CasebookProjection 消费新 event
→ return new A
```

不得使用 `revision + 1` / `wall_clock` 作为 merge 或 freshness 标量。

---

## 22.3 maintenance failure ≠ fetch failure

Bookkeeper 失败、subject 持续漂移、publication 失败时：

```text
不 append 撕裂 candidate
CasebookProjection 保持旧 Case
return 当前 projected A
```

旧 A 可能过时——这是允许的产品行为（见 §29.1、§30；remote/local publication 失败语义见 Persist/EventStore，Casebook 不拥有独立 sync 状态机）。

---

# 23. Evidence diff

Bookkeeper 不直接读取 repository。

因此 Coordinator 必须把“它需要知道的 repository 变化”预先构造为 self-contained evidence diff。

至少包含：

```text
read snapshot:
  old bytes vs current bytes

file created/deleted:
  full relevant change

glob:
  old canonical result vs new canonical result

grep:
  old canonical result vs new canonical result

recognized executor observation:
  old observation vs new observation
```

内容属于低信任 data。

---

# 24. Bookkeeper

## 24.1 身份

Bookkeeper 是 Casebook feature 专用的**内部 Agent**。

建议 Agent pair：

```text
fast-bookkeeper
deep-bookkeeper
```

但它们是 conditional internal agents：

```text
Casebook disabled
→ 不要求配置存在

Casebook enabled
→ fast/deep pair 必须同时存在并通过启动校验
```

不得因为这个 optional feature 把所有 repository 的 baseline startup 要求从现有 mandatory Agent set 无条件扩大。

---

## 24.2 Tier

调用：

```text
fast-inspector fetch
→ fast-bookkeeper

deep-inspector fetch
→ deep-bookkeeper
```

Bookkeeper fast/deep：

```text
system prompt 相同
tool surface 相同
权限相同
只改变 model binding
```

---

## 24.3 不可见

Bookkeeper 不出现在：

```text
Manager fork-agent enum
Inspector fetch 参数中的 agent enum
Coder inspector enum
list() 可创建 Agent 清单
公开 Role 选择
用户 Agent catalogue
```

它只能由 Casebook runtime 合成。

---

# 25. Bookkeeper Session 所有权

不得为 Bookkeeper 复制一套：

```text
child map
cancel map
recovery state machine
session registry
retire loop
```

Bookkeeper 应复用现有 managed child/Satellite lifecycle machinery。

推荐把它建模为 ephemeral internal Satellite：

```text
Inspector WorkSession
  └── Bookkeeper Satellite for one fetch transaction
```

每次需要 refresh 时创建一个专属 Bookkeeper Session。

该 Session：

```text
只属于这一 fetch transaction
完成后 retire
不跨不同 Case 复用 history
不递归创建 Bookkeeper
不拥有 fetch
```

如实施时现有 SatelliteRuntime 的“一 owner 固定一个 kind”结构无法支持 ephemeral multiplicity，应扩展现有 Satellite owner，而不是复制第二套 runtime。

---

# 26. Bookkeeper tool surface

Bookkeeper 恰好只有：

```text
edit-qa
```

没有：

```text
read
glob
grep
executor
write
edit
mv
rm
fetch
fork
join
list
network
```

---

# 27. edit-qa

`edit-qa` 是新的专用工具。

不得复用普通 `edit` 名称，因为其：

```text
schema
path authority
read-before-edit contract
lifecycle
```

均不同。

---

## 27.1 Schema

逻辑 schema：

```text
document = "Q.md" | "A.md"
old_text
new_text
```

只允许编辑当前 fetch transaction 的两个 staged documents。

不接受 filesystem path。

---

## 27.2 Semantics

执行：

```text
staged document 中寻找 old_text
→ 必须存在且满足确定性 replacement 规则
→ 替换为 new_text
→ 更新 staged bytes
```

不存在/歧义：

```text
tool failure
```

Bookkeeper 可以重复调用 `edit-qa`。

---

## 27.3 绕过 read-before-edit 的正确方式

不得：

```text
伪造 read tool transcript
篡改 Host read cache
调用 unpublished Host API
给普通 edit 偷开权限
```

`edit-qa` 自己拥有 staged Q/A 的内存读写权，因此根本不进入普通 filesystem edit 的 read-before-edit contract。

---

# 28. Bookkeeper Prompt

Bookkeeper system 必须表达：

```text
目标：
根据 supplied evidence change
维护当前 Q/A，使 A 对当前 repository evidence 保持有效。

只能通过 edit-qa 改 Q/A。

如果变化不影响答案，可以零次调用 edit-qa 并直接 idle。
```

---

## 28.1 Dynamic data envelope

至少注入：

```toml
[case]
session_id = '...'

[question]
content = '''...'''

[answer]
content = '''...'''

[repository_change]
patch = '''...'''
```

实际字符串必须由统一 canonical renderer / codec 生成。

Q/A/diff 全部是 data。

任何 diff 中出现的：

```text
# Ignore previous instructions
```

不得逃逸为 Bookkeeper trusted instruction。

---

# 29. Bookkeeper idle

Bookkeeper 运行直到正常 idle。

允许：

```text
0 次 edit-qa
1 次 edit-qa
N 次 edit-qa
```

零编辑表示模型认为 evidence 变化不影响当前 Q/A。

框架不得强迫模型“必须修改一点东西”。

---

## 29.1 Bookkeeper failure

以下情况：

```text
provider failure 最终耗尽
invalid terminal
Session lost
edit-qa contract failure 后未恢复
Bookkeeper lifecycle 无法证明完成
```

则：

```text
fetch 返回当前 stored A
旧 Case 保持不变
旧 snapshot 不推进
```

返回的 A 可能未针对最新 evidence 刷新——这是预期产品语义；refresh 失败不是 fetch 失败（§22.3）。

---

# 30. Subject drift during Bookkeeper

Bookkeeper 可能运行较久。

因此它 idle 后，Coordinator 必须重新验证：

```text
Bookkeeper 输入所基于的 current observation state
==
现在的 current observation state
```

若已经改变：

```text
discard staged Q/A
不得 publish snapshot
```

然后重新开始 refresh。

该稳定化循环必须有限。

建议：

```text
CasebookRefreshMaxAttempts = 3
```

连续 3 次都遇到 subject drift：

```text
返回当前 stored A
```

不得无限等待“repository 终于稳定”。

---

# 31. Publication 原子性（EventStore append）

> **REPLACE** 原 Casebook tree CAS / `update-ref refs/wanxiang/inspector-casebook`。

逻辑 Case：

```text
Q
A
snapshot
observations
```

必须作为**一次** `InspectorCaseCaptured` 或 `InspectorCaseRefreshed` append 原子切换进 live projection（同一 event 的 payload / `payload_refs` 闭包完整）。

物理步骤由 Persist 拥有：

```text
canonicalize event + write payload blobs
→ build store root candidate
→ CAS refs/wanxiang/store via IEventStore
```

在 Append 成功之前，新 Case 对 CasebookProjection 读者不可见。

---

## 31.1 本地 append

使用：

```text
IEventStore.Append(InspectorCaseCaptured | InspectorCaseRefreshed | …)
```

Casebook Application **不得**直接调用 feature `update-ref`，也不得暴露：

```text
compareAndSwapCasebookRoot   # do not use
```

---

## 31.2 Append / CAS conflict

store CAS 因其它 worktree/process 更新失败时：

```text
Persist 重读最新 StoreSnapshot
→ k-way merge / retry append（bounded）
```

若同一 `session_id` 出现合法并发 heads：

```text
投影：DomainConflict
当前 fetch 不得假装自己的 candidate 已是唯一 winner
→ 重新读取投影
→ 对当前 worktree 重新 freshness check
→ 必要时以 competing heads 为 parents append 新的 Refreshed / Resolved
```

若持续竞争超过有限预算：

```text
fetch 返回当前可读 A；或在完全无法读 A 时失败
```

不得用 revision/wall_clock LWW “强行宣布胜利”。

---

# 32–40. Remote / refs / dumb sync / hooks / bootstrap / cleanup — SUPERSEDED

> **REPLACE** 原 §§32–40 整段 Casebook-owned remote/refspec/hook/bootstrap 设计。
> 统一由 Persist + `GitGateway` 拥有。Casebook **必须不**拥有 sync / hooks / refspecs。

## 32. SUPERSEDED — Casebook Remote policy

**SUPERSEDED：** “若 origin 存在则 origin 是 Casebook sync remote” 以及任何 Casebook-owned remote 选择。

当前：Case 事件随统一 EventStore 复制范围走 Persist 策略。Casebook 不选择 remote，不实现 `fetchRemoteCasebook` / `pushRemoteCasebook*`。

---

## 33. SUPERSEDED — Casebook Remote refs

**SUPERSEDED / 禁止作为当前设计：**

```text
refs/wanxiang/inspector-casebook
refs/wanxiang/remotes/origin/inspector-casebook
Casebook custom fetch refspec
```

当前唯一动态 store ref：

```text
refs/wanxiang/store
```

其 remote-tracking / refspec / lease-push 由 Persist/GitGateway 定义，不是本 Change 的端口。

---

## 34. SUPERSEDED — Casebook dumb-remote sync 算法

**SUPERSEDED：** Casebook 自己的

```text
fetch remote Casebook ref → merge → local CAS → CAS-push
```

当前：Application 需要跨 replica 可见性时调用 Persist 的 converge 能力（经 `GitGateway`），语义是 EventStore union，不是 Casebook LWW tree merge。

---

## 35. SUPERSEDED — Casebook Remote CAS-push / lease

**SUPERSEDED：** Casebook `--force-with-lease` 推 feature ref。

Lease / CAS-push 若存在，只针对 `refs/wanxiang/store`，由 Persist/GitGateway 实现。Casebook 不得 blind force，也不得拥有自己的 lease API。

---

## 36. Local correctness 与 remote availability 分离（保留产品语义）

### 36.1 Local publication failure

若当前 `fetch(session_id)` 需要刷新 Case，而本地 `IEventStore.Append(InspectorCaseRefreshed)` 无法完成：

```text
CasebookProjection 保持不变
fetch 返回当前 stored A
```

答案可用性优先于刷新成功。

### 36.2 Remote / converge failure

offline / DNS / auth / lease contention / converge budget 用尽时：

```text
不得让本地已 append 成功的 Answer 失效
fetch 可以正常返回本地 projected A
sync/converge 留到 Persist 后续入口
```

Remote 是 EventStore replica 通道，不是 Casebook authority。
Casebook **不**保存 `PendingSync` / `NeedsPush` 第二状态机（见 §54）。

---

## 37–38. SUPERSEDED — Casebook reference-transaction hook / ownership

**SUPERSEDED / 禁止作为当前设计：**

```text
Casebook 监听 refs/wanxiang/remotes/origin/inspector-casebook
WANXIANG_CASEBOOK_HOOK_ACTIVE
Casebook 安装/维护 reference-transaction shim
“hook acceleration disabled” 作为 Casebook 合法状态
```

若仓库存在 Persist/GitGateway 的统一 hook dispatcher，那是 storage Change 的职责。Casebook 正确性不得依赖任何 hook；feature disable 也不得去卸载 Persist hooks。

---

## 39. SUPERSEDED — Casebook Bootstrap（feature refspec / hook / casebook-only fetch）

Casebook marker 首次被发现时：

```text
初始化 Casebook domain/runtime surface
不安装 Casebook custom refspec
不安装 Casebook hook
不做 casebook-only bootstrap fetch
不创建空 Casebook commit / feature ref
```

若需要与 remote 对齐，走统一 EventStore converge（Persist），失败不阻止 Inspector。

---

## 40. Feature disable cleanup

marker 消失后：

```text
provider surface 立即关闭
不 archive / 不暴露 fetch / 不要求 Bookkeeper
```

**不得**借机删除或改写：

```text
refs/wanxiang/store
Persist/GitGateway hooks
用户 hook
EventStore objects
```

残留若有人错误实现过的 Casebook hook：**不是**本 Change 当前设计；若代码路径仍存在，必须在无 marker 时 no-op，且不得被文档描述为应维护的产品能力。

---

# 41. Inspector completion → Case creation

Inspector 正常完成后：

```text
if feature disabled:
    return as today

else:
    Q = full initial Inspector prompt
    A = exact bounded Inspector ToolResult
    snapshot = captured evidence snapshot
    observations = flattened replayable observations
    append InspectorCaseCaptured（PayloadRef 承载大正文；best-effort）
    （Persist 可随后 ConvergeStore；Casebook 不拥有 sync）
    return same A
```

capture 有缺口不阻止归档：能捕获多少就保存多少。

Casebook publication 不得改变原 Inspector caller 已应获得的 Answer bytes。

**SUPERSEDED：** `meta.revision = 1` / `meta.wall_clock = now` 作为 publication schema。

---

# 42. Publication failure on initial archive

第一次 Inspector 已经成功完成，但 `InspectorCaseCaptured` append 失败时：

```text
Inspector 原 tool call 仍应返回其正常 A
```

因为 Casebook 是附加可选缓存，不得把一次已经完成的只读调查变成“因为 cache 写失败所以调查失败”。

区别于 `fetch` refresh：

```text
initial archival failure
→ skip cache, original Inspector answer still succeeds

fetch refresh publication failure
→ Case 保持不变，返回当前 stored A
```

两者都不让调用方拿不到答案；区别只在 archive 是否推进。

---

# 43. Casebook 与 Journal / EventStore authority

> **REPLACE** “Casebook tree ref 自身就是 Casebook authority”。

**EventStore 是 Case durable facts 的唯一 authority**：

```text
InspectorCase* events
+ PayloadRef materials
→ CasebookProjection
```

不得把完整：

```text
Q
A
snapshot
observation result
Bookkeeper patch
```

复制进 Journal / NDJSON / 私有旁路文件作为第二份 truth。

Journal 或诊断日志如需记录，只保存：

```text
session id
EventId / opaque PayloadRef
byte count
observation count
result/error code
duration
```

不得记录大段 Case 内容。Journal **must not hold Case bodies**。

---

# 44. Casebook 与 Worktree Clean Gate

动态 Case 内容只能进入：

```text
统一 EventStore（refs/wanxiang/store + object database）
```

不得生成：

```text
.wanxiang/casebook/<session>/
```

这样的动态 worktree 文件。

`.wanxiang/casebook/.keep` 是静态 opt-in marker，正常由 repository commit 管理。

因此 Case refresh / Persist sync 不应出现在：

```text
git status
```

也不得使 Orchestrator workspace dirty。

---

# 45. Casebook 与 Inspector “只读”

Inspector 模型本身仍只有只读调查能力。

它不能：

```text
写 source workspace
直接写 EventStore / 直接 append
直接 edit Q/A
调用 update-ref
调用 git push
```

`fetch` 是一个受 runtime 控制的知识读取/刷新工具。

Bookkeeper 对 Q/A 的修改发生在 staged documents 中，不授予 Inspector filesystem write 权限。

因此“Inspector 对 subject repository 只读”保持成立。

---

# 46. Q/A edit 与 snapshot commit 顺序

必须严格：

```text
freeze old Case
→ replay evidence
→ construct diff
→ Bookkeeper
→ final staged Q/A
→ final current evidence verification
→ build refreshed materials
→ append InspectorCaseRefreshed（原子 publication）
→ return A
```

禁止：

```text
先换 snapshot / 先 append 不完整 event
再跑 Bookkeeper
```

否则 Bookkeeper 失败后会得到：

```text
new snapshot + old A
```

下一次 replay 将错误地认为没有变化。

---

# 47. Corruption / invalid Case

Case / event 消费必须验证：

```text
safe session identity
Q readable
A readable and within ToolResult contract
observations schema valid
snapshot path containment valid
PayloadRef 可达且属于 committed store closure
```

**不再验证**（已删除的 schema）：

```text
revision valid positive integer
wall_clock / timestamps parseable as merge fields
meta.toml LWW schema
```

无效 Case：

```text
不得进入 Inspector index
不得被 fetch 返回
```

坏 event 若属 Persist `StorageInvalid`（坏 JSON / 缺 parent / unknown authoritative type 等）→ **fail closed via Persist**，不得“跳过坏 event 继续猜”。

Casebook 是 cache：业务上非法但物理合法的 Case materials 可在后续 deterministic prune 中 `InspectorCaseEvicted`；不得从自然语言 Q/A 猜缺失 metadata。

---

# 48. Remote malformed data

Remote 经 Persist converge 并入的 event set 中若含非法/损坏 Case payload：

```text
非法 Case 不得进入 CasebookProjection 覆盖本地合法 Case
StorageInvalid → Persist fail closed
DomainConflict → 投影表达冲突，等待 resolution event
```

**SUPERSEDED：** “不得因为 remote revision 更大就绕过 validation”——本 Change 已无 revision merge。

---

# 49. Bookkeeper security

Bookkeeper system 中：

```text
Q
A
patch
file content
grep output
glob output
```

全部视为 untrusted data。

唯一 trusted instruction 是框架固定的维护任务。

`edit-qa` 的参数同样是模型输出，必须经过：

```text
document enum gate
size gate
exact replacement gate
UTF-8 gate
```

---

# 50. No second runtime

Casebook flow 必须使用普通结构化程序表达：

```text
let!
match
bounded retry
ordinary task/resource scope
```

不得创建：

```text
CasebookStage
FetchPhase
BookkeeperPhase
SyncStateMachine
Command/Reply interpreter
Step AST
```

**SUPERSEDED：** “`revision` 是真实 replica data”——replica data 是 EventStore events，不是 revision 计数器。

`PrefixEpoch` 是已有领域事实，不新增 CasebookGeneration 类伪阶段。

---

# 51. 并发

## 51.1 不同 Case

不同 `session_id` 可以并发准备 payload / append candidates。

最终 store mutation 经 Persist `IEventStore` CAS / merge 收敛。

---

## 51.2 同一 Case / 同一 worktree

同一 Inspector runtime 对：

```text
(worktree identity, session_id)
```

建议 single-flight `fetch` refresh，避免同时启动两个 Bookkeeper。

该 single-flight 是进程内物理所有权，不写入 Case metadata / Journal。

---

## 51.3 不同 worktree 同一 Case

允许并发。

因为各自针对不同 current worktree validation。

两边可能各自 append `InspectorCaseRefreshed`。

Replica 层：

```text
EventStore union
→ CasebookProjection
→ 若互斥 heads：DomainConflict
→ 后续 Refreshed/Resolved/Evicted 收敛
```

**SUPERSEDED：** `revision → wall_clock → OID` LWW 选唯一 canonical Case。

另一个 worktree 下一次使用时再次 replay observations；若不适合其 tree，会再次 refresh。

这是预期 eventual behavior，不建立产品 multi-version Case API。

---

# 52. Remote convergence

只要：

```text
两 replica 后续能经 Persist/GitGateway 互相 converge
且没有无限新的 mutation
```

重复执行：

```text
EventStore set-union
CasebookProjection fold
InspectorCaseEvicted prune（bounded）
store CAS / converge
```

应 eventually 收敛到相同 event set（进而相同 live projection，冲突经 resolution 消失）。

不要求 vector clock、HLC、CRDT conflict set，也**禁止** LWW。

---

# 53. SUPERSEDED — timestamp 同值 LWW

**SUPERSEDED：** “same revision + same wall_clock → 任取一个 / OID tie-break”。

当前：并发 heads 保留在 history 中，由投影标为 `DomainConflict` 或由 domain 定义的 order-independent fold 消化；不得为了 merge 漂亮而把 wall_clock 塞进产品 schema。

不得因此增加：

```text
replica_id
Lamport clock
vector clock
UUID timestamp
```

到 Casebook 产品 schema。

---

# 54. Sync failure 不建立第二状态机

不保存：

```text
PendingSync
NeedsPush
RemoteDirty
SyncGeneration
```

下一次合法同步入口直接从 Persist 物理状态：

```text
local refs/wanxiang/store
GitGateway observed remote store
```

重新计算该做什么。

物理状态就是事实；Casebook 不拥有 sync 状态机。

---

# 55. Git reflog / history

统一 store ref 不主动为 Casebook 创建 reflog。

不要求：

```text
--create-reflog
```

产品不提供 Case history 恢复 API。

Git 因自身配置临时留下 unreachable object 或 reflog，不改变产品语义。Event history 来自 events，不是 Git commit graph。

---

# 56. Git GC / 可达性

`refs/wanxiang/store` 保证 committed EventStore root（含 payload closure）reachable。

Casebook **不**维护 feature pin refs；epoch freeze 依赖已 committed payloads + 进程内 snapshot。

未被任何 committed store root 引用的 orphan blobs 可由正常 Git GC / Persist 规则回收。

不得自己实现第二套 Casebook object GC，也不得用 pin ref 阻止 Persist GC 协议。

---

# 57. Formal specification impact

实施该 Change 时，应建立一个独立正式主题，例如：

```text
docs/why/casebook.md
docs/what/casebook.md
docs/shape/casebook.md
docs/how/casebook.md
docs/proof/casebook.md
```

Change 文件本身不定义正式 Clause ID。主题名 “casebook” 指产品能力，不暗示 feature Git ref，也不得写回 `refs/wanxiang/inspector-casebook`。

---

## 57.1 architecture

需要正式化：

```text
Casebook best-effort freshness vs EventStore replica convergence 分离
Case 动态数据只经 refs/wanxiang/store，不进入 worktree
PrefixEpoch CasebookIndexSnapshot freeze（无 feature pin）
Casebook 不制造新 epoch
不建立第二运行时
Casebook 不拥有 sync/hooks/refspecs
```

---

## 57.2 agent

需要修改/补充：

```text
Inspector conditional fetch tool
Bookkeeper internal Agent
conditional fast/deep Bookkeeper pair
feature disabled 时不要求 Bookkeeper config
Bookkeeper 不可见
edit-qa 唯一工具
```

---

## 57.3 host / execution

需要正式化：

```text
Bookkeeper ephemeral managed/Satellite lifecycle
owner / cancel / retire
fetch blocking semantics
single-flight
无第二 child runtime
```

---

## 57.4 projection / companion / context

需要正式化：

```text
Inspector system CasebookIndexSnapshot
同 epoch 字节稳定
probe promotion 继承 frozen index
Casebook 更新不制造 epoch
低信任 Q list containment
```

---

## 57.5 synthetic TOML

需要把以下 LLM-visible surface 纳入现有统一 renderer/inventory：

```text
Inspector Casebook index
Bookkeeper dynamic Q/A/evidence envelope
fetch/edit-qa 自定义 tool text result（如适用）
```

---

## 57.6 persist

需要明确：

```text
Case durable authority = unified EventStore（IEventStore）
InspectorCaseCaptured / Refreshed / Accessed / Evicted
CasebookProjection fold
大正文 = PayloadRef → store payloads/
Journal / 诊断不得复制 Case bodies
物理 CAS / converge / dumb remote = Persist + GitGateway
禁止 Casebook feature ref / LWW / pin / Casebook hook
```

---

## 57.7 orchestrator

需要 proof 确认：

```text
Case refresh / Persist sync 不产生 worktree dirty
Clean Gate 行为不变
```

---

# 58. 推荐实现分层

## Domain / Kernel

纯类型与算法：

```text
Case（逻辑 Q/A/observations/snapshot refs）
Observation
ObservationIdentity
ObservationReplayResult
CasebookProjection
LruPrune（基于 projected last_access）
DomainConflict 表达（同 Case heads）
```

纯函数：

```text
foldCasebookProjection
prune
classifyReplay
normalizeObservations
```

不得 Git I/O，不得出现 `GitObjectId` / feature ref API。

**SUPERSEDED domain 类型：** `CaseRevision`、LWW `compareCase` / `mergeCasebooks` 作为 storage merge。

---

## Application

结构化 workflow：

```text
archiveInspectorResult   → Append Captured
fetchCase
refreshCase              → Append Refreshed
touchCaseAccess          → Append Accessed
evictCases               → Append Evicted
```

直接使用 CE / Task / match。
需要跨 replica 时组合 Persist converge（经 GitGateway），**不**实现 Casebook syncCasebook 端口。

---

## Infrastructure

仅实现能力：

```text
IEventStore / StoreSnapshot（Persist）
GitGateway（Persist 组合；非 Casebook 拥有）
filesystem evidence reads
Host tool observation adapter
SyntheticToml renderer
```

**禁止 Casebook Infrastructure 再实现：**

```text
feature tree materialization as authority
update-ref Casebook ref
fetch/push Casebook ref
reference-transaction Casebook shim
wall clock LWW
```

---

## Session / Process

物理 ownership：

```text
Inspector epoch CasebookIndexSnapshot freeze（进程内）
same-worktree fetch single-flight
Bookkeeper child lifetime
```

不得把这些镜像成长期领域状态机，不得创建 Casebook pin refs / Casebook hook recursion guard。

---

# 59. 推荐主要端口

概念上保持具名 capability；**不**建立 Casebook generic Git Command bus。

```text
# EventStore / projection
IEventStore.Append
IEventStore 读取 StoreSnapshot / 投影输入
fold CasebookProjection

# Observation
captureReadObservation
replayObservation

# Bookkeeper
startBookkeeper
awaitBookkeeperIdle
```

Persist 组合根可使用 `GitGateway`，但那不是 Casebook 端口。

**Do not use / SUPERSEDED Casebook ports：**

```text
compareAndSwapCasebookRoot
fetchRemoteCasebook
pushRemoteCasebookWithLease
pinCasebookRoot
releaseCasebookPin
readCasebookRoot   # as feature-ref authority
```

---

# 60. 测试与 proof：Feature gating

必须证明：

```text
marker absent
→ Inspector 无 fetch schema
→ ToolRegistry execute fetch 也拒绝
→ 无 Casebook index
→ 无 Bookkeeper config requirement
→ 无 archive / 无 InspectorCase* append
→ Casebook surface 全关
```

双门都必须测：

```text
provider schema
execution registry
```

不能只隐藏 schema。

**SUPERSEDED proof：** “Casebook hook no-op”——Casebook 当前设计不拥有 hook；Persist hooks 与 marker 无关。

---

# 61. 测试与 proof：Q/A

必须证明：

1. 新 Case Q 逐字等于完整 Inspector initial prompt；
2. Q 不经过摘要；
3. A 逐字等于实际 Inspector ToolResult body；
4. oversized Inspector answer 先走现有 ToolResultBound，再作为 Captured payload；
5. Bookkeeper 可以改 Q；
6. Bookkeeper 可以改 A；
7. Bookkeeper 可以连续多次 edit-qa；
8. 零 edit idle 合法；
9. edit-qa 不能写第三个文件；
10. Bookkeeper 最终 A 仍满足 ToolResultBound。

---

# 62. 测试与 proof：Observation capture

至少覆盖：

```text
read full file
read range
glob zero result
glob multi result
grep zero result
grep multi result
文件 deletion
文件 create
文件 rename 导致 glob/grep 变化
```

并证明：

```text
capture 不完整（如 executor 命令无法识别）
→ original Inspector 成功
→ Case 照常 Captured
→ 缺失的 observation 只是未来少一次变化检测机会
```

---

# 63. 测试与 proof：executor parser

正例（必须识别为 observation）：

```text
cat file
cat -n file
head file
head -n 30 file
tail -100 file
tail -f file
sed -n '20,80p' file
cat file | grep bar
```

负例（必须安全跳过、不报错、不阻止归档）：

```text
cat "$(...)"
sh -c ...
bash -c ...
命令替换
无法确定读取目标的复杂 pipeline
```

负例命中：

```text
该命令的 observation 被跳过
Case 仍 Captured
```

---

# 64. 测试与 proof：路径安全

覆盖：

```text
tracked file
tracked modified file
untracked file
ignored file
.git path
../ escape
absolute external path
symlink escape
submodule/external repo
```

批准范围内 evidence 永久化（EventStore payload）；范围外（untracked/ignored/external/.git）证据跳过，Case 仍 Captured。

---

# 65. 测试与 proof：Freshness

## unchanged

```text
Case old snapshot
current worktree identical
fetch
→ Bookkeeper zero launches
→ A exact
→ InspectorCaseAccessed appended（best-effort）
```

## changed read

```text
file bytes changed
→ 启动 Bookkeeper refresh（一次 refresh flight）
→ 成功：InspectorCaseRefreshed + 返回新 A；失败返回旧 A
```

## changed glob

```text
new matching tracked path
→ 检测到 delta → 启动 refresh
```

## changed grep

```text
matching line set changes
→ 检测到 delta → 启动 refresh
```

这三类尤其证明：

> freshness 检测不限于“被 read 的旧文件没变”，而覆盖 negative/search evidence。

## refresh failure

```text
changed evidence + Bookkeeper 失败
→ 返回旧 A
→ 不 append Refreshed；snapshot 不推进
```

---

# 66. 测试与 proof：Bookkeeper failure

故障注入：

```text
create Session fail
provider fail
edit-qa fail
invalid terminal
idle wait fail
```

期望：

```text
无新的 InspectorCaseRefreshed
old projected Case unchanged
fetch 返回旧 A（内容可能过时，这是预期语义）
```

---

# 67. 测试与 proof：Subject drift

剧本：

```text
replay change A
→ Bookkeeper starts
→ worktree changes to B before idle
```

必须：

```text
candidate discarded
不 append 基于 A 的 Refreshed
重新 refresh
```

连续超过 bounded attempts：

```text
返回旧 A
```

---

# 68. 测试与 proof：Atomic publication（EventStore append）

在以下每一点故障注入：

```text
payload blob write
event canonicalize
store root build
IEventStore CAS
```

CasebookProjection 只能看见：

```text
old complete Case
或
new complete Case
```

绝不能：

```text
new Q + old A
new A + old snapshot
```

---

# 69. 测试与 proof：Local multi-worktree CAS（store）

模拟：

```text
A observes store S0
B observes store S0

A appends Captured/Refreshed → S1
B CAS S0→S2 fails
B reloads S1
B merges / retries
B publishes S3
```

最终 event set 同时包含双方不冲突 Case events。

---

# 70. 测试与 proof：same-case conflict（DomainConflict，非 LWW）

两个 worktree 对同一 Case 同时 refresh，各自 append `InspectorCaseRefreshed`。

必须：

```text
EventStore union 保留双方 events
CasebookProjection 表达 DomainConflict 或等价确定性冲突态
不得用 (revision, wall_clock) 选 winner
```

后续 resolution / 新的 Refreshed（parents ⊇ competing heads）后投影离开冲突态。

**SUPERSEDED proof：** `max((11,t1),(11,t2))` LWW。

---

# 71. 测试与 proof：Remote sync — SUPERSEDED Casebook remote；改测 Persist converge

**SUPERSEDED 作为 Casebook 拥有的测试面：** custom Casebook ref / lease / `remote rejects tree custom ref`。

当前应证明（依赖或对接 storage Change）：

```text
Case events 经 refs/wanxiang/store converge 可见
network/auth/lease failure 不回滚已成功 local Append
Casebook 无 fetchRemoteCasebook / pushRemoteCasebook 端口
```

---

# 72–73. SUPERSEDED — ordinary git fetch Casebook hook / existing hook ownership

**SUPERSEDED proofs：**

```text
remote tracking Casebook ref → Casebook reference-transaction hook
WANXIANG_CASEBOOK_HOOK recursion guard
Casebook bootstrap 不覆盖用户 hook
```

当前：Casebook 不拥有这些；Persist/GitGateway hook 证明属于 storage Change。本 Change 只需证明 Casebook 正确性不依赖 hook。

---

# 74. 测试与 proof：Prefix stability

同 Inspector PrefixEpoch：

```text
request 1 index = I0

后台：
Captured 新 Case
fetch Case / Accessed
LRU Evicted
Persist converge

request 2 index 必须仍逐字 = I0
```

只有下一合法 epoch 建立时才允许 index 变为 I1。

---

# 75. 测试与 proof：probe boundary

专门测试：

```text
candidate epoch index selected = I1
probe provider sees I1
probe promoted
next request still I1
```

禁止：

```text
probe 时 I1
promotion 后重新 sample 得 I2
```

---

# 76. 测试与 proof：Epoch freeze（无 pin refs）

```text
epoch index 包含 Case X
→ live projection LRU append InspectorCaseEvicted(X)
→ Git GC / Persist GC 按 store 规则运行
→ active epoch fetch(X) 仍可从 CasebookIndexSnapshot 冻结 materials 读取
```

**SUPERSEDED：** `refs/wanxiang/pins/...` / `pinCasebookRoot` 证明。

Epoch retire 后释放进程内 freeze；不得残留 feature pin ref。

---

# 77. 测试与 proof：fetch evidence flattening

剧本：

```text
Case A 已存在
Inspector B calls fetch(A)
B 再 read file2
B 完成
```

B 的 observations 必须包含：

```text
A 的 captured evidence
+ B direct evidence
```

之后 Evict A：

```text
fetch(B)
```

仍可独立 freshness replay。

不得要求 A 仍存在于 live projection。

---

# 78. 测试与 proof：LRU

验证：

```text
successful fetch → InspectorCaseAccessed
不 append 伪造 Refreshed

Bookkeeper refresh → InspectorCaseRefreshed
Accessed 语义更新 last_access

prune → InspectorCaseEvicted tombstone
projection 不再 retained
```

**SUPERSEDED：** `content revision 不变` / `revision +1` / `wall_clock changes` / `merge last_access = max` meta schema。

同 ties 必须 deterministic（fold 纯函数）。

---

# 79. 测试与 proof：Clean Gate

执行：

```text
Inspector archival（Captured）
fetch refresh（Refreshed）
LRU touch（Accessed） / Evicted
Persist converge（若测集成）
```

前后：

```text
git status porcelain
```

subject worktree 状态必须完全不因 Case 动态数据改变。

---

# 80. 测试与 proof：知识旁路

扫描：

```text
Journal
normal diagnostic logs
runtime metadata
Host business facts
```

不得出现不必要的完整：

```text
Q
A
snapshot
Bookkeeper patch
```

**EventStore（`refs/wanxiang/store` events + payloads）是这些数据的唯一新永久化面。**

---

# 81. 发布完成判据

本 Change 只有同时满足以下条件才可关闭：

1. feature disabled repository 的 Inspector 行为与当前完全兼容；
2. marker 启用后 Inspector 有 conditional `fetch`；
3. Inspector Case 完成后可 append `InspectorCaseCaptured`（capture best-effort）；
4. Q 初始完整，不摘要；
5. A 与实际 bounded ToolResult 一致；
6. read/glob/grep observation 可重放；
7. 未识别的 executor 命令只跳过其 observation，Case 仍 Captured；
8. external/untracked/ignored/.git 证据不永久化，Case 仍 Captured；
9. unchanged evidence 不启动 Bookkeeper；touch = `InspectorCaseAccessed`；
10. changed evidence 触发 best-effort refresh；成功 = `InspectorCaseRefreshed`；失败返回旧 A；
11. Bookkeeper 只有 `edit-qa`；
12. Bookkeeper 可修改 Q 和/或 A；零 edit idle 合法；
13. Bookkeeper failure 不推进 snapshot，fetch 返回旧 A；
14. Bookkeeper idle 后重新验证 subject stability（有界重试）；
15. Q/A/snapshot publication = 一次 EventStore append（Captured/Refreshed）原子；
16. Case durable authority = unified EventStore / `refs/wanxiang/store`，不创建 Casebook commit / feature ref；
17. 所有 worktree 共享同一 EventStore（common-dir）；
18. 动态 Case 内容不污染 worktree；
19. full-Q Inspector index 同 PrefixEpoch 字节稳定（冻结 CasebookProjection snapshot）；
20. Casebook mutation 不主动切 PrefixEpoch；
21. epoch freeze（无 feature pin refs）防止活跃 index Case 在 Evict 后不可读；
22. `fetch` 使用 current worktree 做 freshness replay；
23. fetched Case evidence 被 flatten，不形成递归 dependency runtime；
24. replica 收敛只用 EventStore union + projection；**禁止** `(revision, wall_clock)` LWW；
25. observation no-delta 与任何物理标量都不构成正确性证明；
26. last_access 由 Accessed/Captured/Refreshed 投影派生；
27. deterministic LRU 有界；淘汰 = `InspectorCaseEvicted`；
28. remote 同步属 Persist/GitGateway，Casebook 不拥有 refspec/hook/lease API；
29. Persist converge / lease failure 不破坏 local 已 Append 的 Case；
30. Casebook 正确性不依赖 Git hook；
31. 不覆盖用户 Git hook（Casebook 也不安装自己的 hook）；
32. 没有 server-side hook requirement（dumb remote 属 Persist）；
33. 没有 Casebook commit/version/history API；
34. 没有第二运行时、Stage/Phase/Casebook Sync state machine；
35. Inspector prompt 明确 fetch 免费（§1.3）；
36. Journal 不持有 Case bodies；
37. why/what/shape/how/proof 与实现、proof tests 全部闭环；
38. spec/lint/architecture/DSL/static gates 与相关 unit/integration/e2e 全绿；
39. 新增静态门均有受控反例证明能够判红；
40. 端口面不包含 SUPERSEDED Casebook Git ports（§59）。

---

# 82. 推荐实施顺序

为避免边写边发明语义，建议实施者严格按以下顺序工作。

### Step 1 — 正式规范

先建立 Casebook 的：

```text
why
what
shape
how
proof
```

并同步更新：

```text
architecture
agent
host/execution
projection/context
synthetic-toml
persist（EventStore vocabulary + CasebookProjection）
orchestrator proof
glossary/navigation
```

先把所有权和硬边界写完整：**Casebook 无 feature store**。

### Step 2 — 纯 Domain

实现并测：

```text
Case / observation identity / dedupe
CasebookProjection fold
DomainConflict 表达
LRU prune → Evicted 决策（纯）
freshness classification
```

无 Git/Host I/O。
**不要**实现 LWW merge / revision / wall_clock。

### Step 3 — EventStore + GitGateway（非 feature store）

对接统一 Persist：

```text
IEventStore.Append / StoreSnapshot
PayloadRef materials
refs/wanxiang/store CAS（Persist）
GitGateway converge 组合（非 Casebook 拥有）
```

先证明多 worktree local append/converge correctness。
**SUPERSEDED Step：** Casebook tree read / pin refs / feature root validation。

### Step 4 — Observation capture/replay

接：

```text
read
glob
grep
recognized executor
path safety
tracked gate
flattened fetch evidence
```

做到“能捕获多少捕获多少；不可解释的证据跳过；路径安全 gate 拦截非法证据”。

### Step 5 — Inspector archive

把现有 Inspector terminal ToolResult 接到：

```text
Q
A
observations
snapshot
append InspectorCaseCaptured
```

保证 archive failure 不影响原 Inspector Answer。

### Step 6 — Inspector fetch hot path

先实现：

```text
lookup（projection + epoch freeze）
replay unchanged
return A
append InspectorCaseAccessed
```

证明不启动 Bookkeeper。

### Step 7 — Bookkeeper

增加：

```text
conditional internal pair
ephemeral lifecycle
system TOML
edit-qa
changed-evidence refresh
append InspectorCaseRefreshed
post-idle stability verify
```

### Step 8 — Prefix index

最后接 Prompt projection：

```text
full Q list
epoch freeze CasebookIndexSnapshot
candidate epoch sampling
```

避免先污染 provider prefix 再补 seal 证明。无 pin refs。

### Step 9 — Persist remote converge（非 Casebook remote）

**不**实现 Casebook custom refspec / lease push。

验证 Case events 随 `refs/wanxiang/store` 经 GitGateway converge；remote failure 保持 local Append success。

### Step 10 — 无 Casebook hook step

**SUPERSEDED：** “Git fetch accelerator / reference-transaction Casebook shim”。

若 Persist 已有统一 hook dispatcher，Casebook 不单独接入；只证明 Casebook 不依赖 hook。

### Step 11 — Crash/concurrency/e2e

完成：

```text
IEventStore CAS races
DomainConflict same-case
Bookkeeper drift
LRU Evicted
epoch freeze without pins
feature off compatibility
Persist converge failure isolation
```

之后再申请 Reviewer。

---

# 83. 最终设计原则

本 Change 最终应保持以下五句话成立：

```text
1. Casebook 是 best-effort semantic cache，不是第二个产品数据库，
   也不是证明系统；允许旧答案过时或不准确。

2. 统一 EventStore（refs/wanxiang/store + IEventStore/GitGateway）
   解决“Case 事实与 materials 放哪里 / 如何同步”；
   observation replay 提供 freshness hint，不证明答案正确。

3. 禁止 revision / wall_clock LWW；replica 只做 EventStore union，
   同 Case 冲突由 DomainConflict + 后续 events 收敛——
   永远不解决答案对不对。

4. Inspector 仍然只读 subject repository；
   Bookkeeper 只能编辑 staged Q/A。

5. 如果 Casebook marker 不存在，
   Casebook 产品表面应当像从未实现过一样消失
   （Persist store sync 不受 Casebook marker 左右）。
```

---

# Active work

> 本文件为变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。
> Original proposal 原文冻结于上方；后续事实只追加于 Active work / Amendments / Blockers / Final outcome。

## Work origin

用户通过 `changes/proposed/entry.md` Implementation Playbook 明确启动：G5（JS Capability-Projected Tools）Exit 达成（`changes/completed/js-capability-projected-tools.md` Final outcome；54 单测 + `npm run check` + `check:release` + Long Stroke 全绿）后，按 Gate 顺序进入 **G6 perm-inspector + Universal Casebook completion**。

## Cross-proposal prerequisites

| Gate | Status | Evidence |
|---|---|---|
| G0–G4 | DONE | 见 `changes/completed/*`（storage Final outcome） |
| G5 JS tools | DONE | `changes/completed/js-capability-projected-tools.md`；最终文件执行层已稳定 |
| G4R testing | DONE | 单一 Long Stroke e2e；无新 canary |

## Approved Amendments

### Amendment G6-A — Storage（Playbook §14.1 A）

```text
Casebook 不再拥有自己的 storage（Git raw store / local CAS / remote / hook 全部
SUPERSEDED——proposal §32–40 已 rebase 到统一 EventStore）。
物理持久化 = InspectorCase* events（Captured/Refreshed/Accessed/Evicted）
→ 统一 EventStore；大正文（Q/A/snapshot）经 PayloadRef → store payloads。
禁止 feature ref / LWW / pin / Casebook hook。
```

### Amendment G6-B — Lifecycle（Playbook §14.1 B）

```text
非复用 Inspector scope → terminal → archive（InspectorCaseCaptured）。
复用 Inspector scope → 调用期间只 capture（不逐次 finalize）；
ReuseScope close → freeze draft → exactly one CaseFinalize → retire/release。
禁止：每个 return finalize / 每个 owner turn finalize / idle / timer / token 阈值 finalize。
```

### Amendment G6-C — Bookkeeper（Playbook §14.1 C）

```text
同一个 Bookkeeper Agent 提供两个 request contract：CaseRefresh（changed evidence
→ edit-qa* → stability verify → InspectorCaseRefreshed）与 CaseFinalize。
不新建 LearningCompiler / CaseSynthesizer / StudentReplacement。
```

### Amendment G6-D — Ownership（Playbook §14.1 D + G5 后世界）

```text
Dedicated Inspector = Work + Attached；Bookkeeper = InternalLeaf + Attached。
不再依赖旧 Satellite-only-WorkSession / Satellite 不可递归 语义。
Observation capture 从最终执行层接（G5 后 = builtin read/glob/grep Host 执行
的 typed observation；js-* bindings 捕获为可选扩展），不从 transcript 文本推断。
```

## Remaining work

按 Playbook §14–21（G6-A..G6-G）+ proposal §58–63：

### G6-A — Casebook Domain First（纯 Domain；无 Host I/O）— DONE（d9d15d5f）
- [x] Formal docs：`docs/{why,what,shape,how,proof}/casebook.md`（CASE-001..012）；`docs/README.md` 索引；spec.mjs 注册（389 clauses）
- [x] `Case`（逻辑 Q/A/observations）、`Observation`（FileRead/GlobResult/GrepResult）、`ObservationIdentity`（normalize 去重）
- [x] `CasebookProjection` fold（Captured/Refreshed/Accessed/Evicted → Map<sessionId, Case> + 派生单调 last_access）
- [x] `Observations.normalize` / `classifyReplay`（Fresh 仅 exact normalized equality）/ `evict`（LRU，victims 返回供 Evicted tombstone）
- [x] DomainConflict 由 EventStore 层表达；Domain 无 revision/wall_clock（CASE-011）

### G6-B — Observation Capture（最终执行层）— DONE（dbfd7660）
- [x] `CasebookCapture.capture`（read → FileRead+sha256；glob → GlobResult；grep → GrepResult；未知工具 None）
- [x] executor 阅读容错（§63 正例 cat/head/tail/sed 含选项跳过 + cat|grep pipeline；负例 sh -c/bash -c/命令替换安全跳过）

### G6-C — Non-reusable Inspector Path — DONE（store/workflow + lifecycle session wiring）
- [x] `CasebookStore`：InspectorCaseCaptured/Refreshed/Accessed/Evicted 事件 + payload codec + topoSort 因果序 + project（fold+LRU）；AuthoritativeEventTypes 扩展
- [x] `CasebookFeature` marker gating（.wanxiang/casebook/ 目录存在；CASE-009）
- [x] `CasebookWorkflow`：archiveInspectorResult / fetchCase / checkFreshness；全部返回 Result（archive failure ≠ Inspector call failure）
- [x] Host 采集钩子：`tool.execute.after`（SpikePlugin；marker 门控；read/glob/grep typed capture → `ObservationCollector` per-session buffer；不改变工具结果）
- [x] Inspector terminal / graceful close → drain + archive：`CasebookLifecycle.tryFinalizeInspector`（draft Q/A + collector.Drain → finalizeCase）；SpikePlugin + HostSignalBootstrap + SyncDelegateRuntime hooks（notePrompt/noteAnswer/cleanup）
- [x] unexpected SessionDeleted → `cleanupInspector` only（never append）

### G6-D — Fetch Hot Path — DONE
- [x] `CasebookReplay`：replayOne/replayAll（只读重放；不可复现 = 变化信号）
- [x] fetch(session_id) 工具（`FetchTool.spec`：fresh/stale/no-case；`CasebookTools.buildSpecs` marker 门控 + 独立模块避免 dual-write token pair；ToolRegistry 经 casebookToolSpecs 接入）
- [x] `CasebookIndex` process-local Snapshot（epoch + session id set；invalidate on Captured/Refreshed/Accessed；refresh from projection）
- [x] same-worktree fetch single-flight（`FetchTool` in-flight Task gate；CASE-011）
- [x] fresh hit → best-effort `touchAccess`（InspectorCaseAccessed）

### G6-E — CaseRefresh（Bookkeeper）— DONE（minimal mechanical；LLM agent deferred）
- [x] `refreshCase`（append Refreshed，线性 parent）+ `needsRefresh`（replay 决策 Fresh/Stale/no-case）
- [x] maintenance failure ≠ fetch failure（Result 语义；失败保留旧 Case）
- [x] Host mechanical Bookkeeper：`CasebookBookkeeper.refreshStale` — needsRefresh → fetch → replayAll → refreshCase(same Q/A, replayed obs)；无 LLM / 无 edit-qa
- [x] FetchTool stale branch：single-flight 内尝试 mechanical refresh once，再 re-fetch；成功且 Fresh → 返回 fresh A；否则仍 stale + refresh:required（不把 maintenance 变成 fetch error）
- [x] unit：`tests/unit/casebook/bookkeeper-mechanical.test.mjs` + fetch stale→mechanical path
- [ ] **Remaining（诚实）**：live Host LLM / `tool.execute.before` Long Stroke **未** Exit。`G6HostPathE2E` landed（inspector-tool path unit）。digest deletion ≠ synthesis Exit。Observational APIs（**not** Exit）：`BookkeeperRuntime.setSessionPort` / `runTransaction` / `isAttached` / `tryTxId`；`EditQaTool.execute`（`Q.md`\|`A.md`, unique `old_text`）；`BookkeeperStaging.begin`/`read`/`replace`/`take`/`abort`。`txId` in `BookkeeperRuntime` not child options。digest gone；BookkeeperRuntime+edit-qa landed。`SpikePlugin` calls `BookkeeperRuntime.setSessionPort` at `createHost`；`tryFinalizeInspector` is `Task`。`G6HostPathE2E` landed（**not** Exit）：`HostSignalBootstrap` SessionDeleted awaits `tryFinalizeInspector` Task before CancelSession；`SpikePlugin` passes `CasebookLifecycle.tryFinalizeInspector` (`Task`) and `BookkeeperRuntime.setSessionPort`。`tests/unit/casebook/g6-inspector-tool-finalize-fetch.test.mjs` is inspector-tool → SyncDelegate → lifecycle → Bookkeeper → fetch，**not** live Host LLM / `tool.execute.before` Long Stroke。G2 `promptModel` not removed。

### G6-F — CaseFinalize（Universal 核心）— DONE（workflow + lifecycle；semantic synthesis deferred）
- [x] `finalizeCase`：exactly-one guard（同 scope 二次 finalize 拒绝；unexpected SessionDeleted 不 reconstruct）
- [x] process-local draft：`CasebookDraftStore` + `CasebookLifecycle.notePrompt/noteAnswer`
- [x] graceful path：`tryFinalizeInspector` freeze draft → finalizeCase once → Index invalidate/refresh
- [x] SyncDelegate / SpikePlugin / HostSignalBootstrap 接线（onInspectorPrompt/Answer/Cleanup；owner graceful finalize hook）
- [ ] **Remaining（诚实）**：ReuseScope-close LLM CaseFinalize synthesis（多轮 Q/A → one canonical Q/A via Bookkeeper provider transaction）—  deferred；当前 finalize = draft Q/A + captured observations 直接 Captured

### G6-G — Universal 最终关闭 — DONE（unit integration window；full LLM e2e deferred）
- [x] unit e2e：`tests/unit/casebook/universal-loop.test.mjs` — lifecycle note→finalize→fetch；cleanup no write；CancelSession cleanup；mechanical refresh after drift
- [x] unit suite `tests/unit/casebook/*` 36 PASS；`npm run build` PASS
- [x] Universal + perm-inspector → `changes/completed/`（同一 integration window）
- [ ] **Remaining（诚实）**：full Host e2e（Meditator → multi-turn dedicated Inspector → ReuseScope close → LLM CaseFinalize → next Session fetch）需 LLM Bookkeeper；不阻塞本 Change 的 mechanical Casebook surface 收口

## Completion criteria

G6 mechanical Casebook surface（Domain / Capture / Store / Replay / Fetch+single-flight / Index / Lifecycle finalize+cleanup / mechanical Bookkeeper）已交付并有 unit proof。LLM Bookkeeper Agent + semantic CaseFinalize 明确 Remaining，不伪造完成。`npm run build` + `node --test tests/unit/casebook/*.test.mjs` 36 PASS。全量 `npm run check` / e2e 不在本关口强制（Playbook 允许 targeted）。

## Blockers

G6 仍 PARTIAL。digest gone；BookkeeperRuntime+edit-qa landed。`G6HostPathE2E` landed（inspector-tool → SyncDelegate → lifecycle → Bookkeeper → fetch unit）but **not** live Host LLM / `tool.execute.before` Long Stroke。digest deletion ≠ synthesis Exit。CaseFinalize synthesis / full Host e2e 是 Product Exit Remaining，不得降格为后续增量。无 user amendment authority。

## Final outcome

**G6 Inspector Casebook（perm-inspector）mechanical surface**（2026-08-11 observational PARTIAL；G6-E/F/G Remaining 仍开放）：

1. **Domain / Store / Capture / Replay**：CASE-001..012 正式文档 + 纯 Domain projection；统一 EventStore 事件（Captured/Refreshed/Accessed/Evicted）；marker opt-in；typed observation capture + executor 阅读容错；freshness = exact normalized observation equality（hint，非 proof）。
2. **Lifecycle session wiring**：`CasebookLifecycle` — notePrompt / noteAnswer / tryFinalizeInspector / cleanupInspector / touchAccess；`SpikePlugin` passes `CasebookLifecycle.tryFinalizeInspector` (`Task`) and `BookkeeperRuntime.setSessionPort`；`HostSignalBootstrap` SessionDeleted awaits `tryFinalizeInspector` Task before CancelSession；`g6-inspector-tool-finalize-fetch.test.mjs` is inspector-tool path unit, **not** live Host LLM；G2 `promptModel` not removed；graceful finalize once；unexpected delete = cleanup only（零 EventStore 写）。
3. **Fetch hot path**：`FetchTool` fresh/stale/no-case；CASE-011 single-flight；fresh → Accessed；process-local `CasebookIndex` epoch snapshot。
4. **Minimal Bookkeeper（无 LLM）**：`CasebookBookkeeper.refreshStale` 在 stale 时用同一 Q/A + 重放 observations 发布 Refreshed；FetchTool stale 分支尝试一次后 re-fetch；maintenance failure ≠ fetch failure。
5. **Proofs**：`tests/unit/casebook/*` **36 PASS**（含 `bookkeeper-mechanical`、`lifecycle-wiring`、`fetch-tool`、`universal-loop`、index/store/domain/capture）。
6. **诚实 Remaining**：`BookkeeperRuntime` / `EditQaTool` / `BookkeeperStaging` cited observationally；digest synthesizer gone；LLM Bookkeeper / CaseFinalize 多轮 synthesis / full Host Meditator e2e **仍开放**。

**Gate 移交**：与 `universal.md` 同窗 completed **不等于** G6 Exit。`BookkeeperRuntime`/`EditQaTool` surface ≠ G6 Exit；digest synthesizer gone；G6-E/F/G Remaining 仍开放。

## Amendment (2026-08-11 strict audit)

Living status is observational. Product Exit Gates (G6-E/F/G) remain the acceptance baseline. This section does not override Gate text. Mechanical digest has **no** user amendment authority and is **not** Bookkeeper / `edit-qa` / synthesis. Host-path unit is **not** full tool→PromptDispatcher→TurnCompleted→Casebook→fetch e2e.

The Remaining bullets under G6-E/F/G above stay open:
- **LLM Bookkeeper** (InternalLeaf + Attached, `edit-qa` synthesis) still open — `BookkeeperRuntime` / `EditQaTool` / `BookkeeperStaging` cited observationally; digest synthesizer gone from `CasebookBookkeeper`; not Host e2e / not Exit.
- **edit-qa synthesis** still open — `EditQaTool.execute` (document `Q.md`|`A.md`, unique `old_text`) is surface, not Host e2e proof.
- **Single provider transaction synthesis (CaseFinalize)** deferred — ReuseScope-close multi-turn Q/A → one canonical Q/A via exactly-one Bookkeeper provider transaction not evidenced; current finalize is draft Q/A direct `Captured`.
- **Evidence stability verify after synthesis** deferred (freeze → Bookkeeper → replay/verify → publish not exercised with LLM candidate).
- **Real Host Meditator→reusable Inspector→scope-close→CaseFinalize→cold fetch e2e** deferred — only helper/unit evidenced (`tests/unit/casebook/universal-loop.test.mjs`, `tests/unit/casebook/*` 36 PASS); no full Host e2e with LLM Bookkeeper.

