# Wanxiangshu Cross-Proposal Implementation Playbook

## 多 Proposal 收敛实施顺序与集成手册

**性质：Implementation Plan / Integration Playbook**
**不是产品 Proposal，不取代任何现有 Proposal，不定义正式 Clause，不修改任何已批准产品语义。**

本文件只规定：

```text
先做什么
后做什么
哪些可以并行
哪些绝对不能提前
什么时候允许删除旧路径
每一阶段的退出条件
Proposal 相互覆盖时如何落地
```

并明确一条贯穿全程的持久化立场：

```text
不必保留磁盘格式兼容性
不必保留与现有 on-disk format 的兼容性
甚至不需要读旧档
```

Unified Storage / Session / Casebook 等 cutover 是 **clean break**：旧 Journal / Blob / feature-owned store 上的历史数据可以丢弃或留在原地不再读取；新世界只认最终 EventStore 语义。禁止为“迁旧数据”“双向兼容”“旧档可读性”投入工期。

**进度快照最后同步：** 2026-08-10（`ac41ef8f` HEAD；详见 §0）

当前 Change 分布：

```text
Active:
  changes/active/universal.md      — G2/G3 DONE；G6 Casebook 待做
  changes/active/storage.md        — G4 Phase 0–7 DONE；Phase 8 收口

Completed（本 Playbook 相关）:
  changes/completed/causal-ce-observability.md
  changes/completed/orchestrator-e2e-timeout.md

Proposed（尚未激活主线 Gate）:
  js-capability-projected-tools
  perm-inspector
  rulebook
  strength
  magic-todo                    — 不在本 Playbook Gate 序列内；可独立 Lane
```

已解决的历史 anomaly：`storage` 已从 `changes/proposed/` 迁至 `changes/active/storage.md`（G3.5 激活）。

这些 Change 不能按照“每个 Proposal 自己从 Phase 0 一路做到完成”的方式独立实施。

因为它们已经形成明确的横向依赖：

```text
Causal CE
    ↓
Session / Sync Delegate architecture
    ↓
Student / Teacher deletion
    ↓
Unified Storage
    ↓
Capability-projected file tools
    ↓
Inspector Casebook
    ↓
Rulebook
    ↓
Strength
```

其中部分工作允许提前并行准备，但**最终 integration 顺序必须遵循本计划**。

---

# 0. 实施进度快照

> 本节是 **living status**，随 `git log` 与 `changes/active/*.md` Active work 更新；不修改任何 Proposal 产品语义。

## 0.1 Gate 总览

| Gate | 状态 | 证据 / 备注 |
|---|---|---|
| **G0** Governance + Baseline | **DONE** | Storage path 唯一化；baseline 已建 |
| **G1** Causal CE + Orchestrator canaries | **DONE** | `changes/completed/causal-ce-observability.md` + `orchestrator-e2e-timeout.md`；release **0.6.0** |
| **G2** Universal Runtime Foundation | **DONE** | `ReuseScope` / `SessionOwnership` / `SyncDelegate` + CausalAwait dual-await；`ca9fd08a` |
| **G3** Universal Clean Break | **DONE** | Student/Teacher/QA/SKILL 删除；Meditator → Inspector only；ratchet green |
| **G3.5** Storage cutover scope 修订 | **DONE** | Amendment G3.5-A；Student QA retired；no migrator / dual-write |
| **G4** Unified Storage | **DONE** | `changes/completed/storage.md` Final outcome；G4R（`changes/completed/test.md`）后验证：`npm run check` GREEN + Long Stroke GREEN（48 steps / 5.0s / ceilings 367/367）；`check:release` GREEN |
| **G5** JS Capability-Projected Tools | **DONE** | `changes/completed/js-capability-projected-tools.md` Final outcome；54 js-tools 单测 + `npm run check` + `check:release` + Long Stroke 全绿；C-3 按用户裁决（共存满足 §107，钩子不接入） |
| **G6** perm-inspector + Casebook | **READY（待 activate）** | G5 Exit 达成；Universal 仍 Active（CaseFinalize / CaseRefresh 未做） |
| **G7** Rulebook | **NOT STARTED** | — |
| **G8** Strength | **NOT STARTED** | — |
| **G9** Global Convergence | **NOT STARTED** | — |

**当前主线位置：** G5 已 completed → **activate G6**（perm-inspector + Universal Casebook completion）。

## 0.2 自 Playbook 落地以来关键 commit（`31d456ec` 之后）

```text
80009351  Causal Waits + Diagnostic Bridge
e0de430e  release 0.6.0
8319771f  capability-projected tools（prep only；G5 未激活）
ca9fd08a  SyncDelegate tools + store feature checks
f5c0f7e7  EventStore JournalEnvelope + codec
dc6c0165  WorkspaceEventStore + EventStoreJournalWriter
69235b5b  event-store gate facts + plugin fixture canary
a2b71ec5  FALLBACK-013 abort residue fix
41d7f1bc  Git ODB in-process（EventStore append 24→0 spawns）
13d3cfcb  e2e wall 104s→33s（真实成本移除，非超时放宽）
002e581c  session-recovery permit + PTY race fixes
ac41ef8f  session abort diagnostic
40d4905a  G4R proposal（unified E2E framework；G4 exit blocker）
17d583cb  G4R implemented（Long Stroke 唯一 e2e；旧 canary 全删）
```

## 0.3 当前证明状态（2026-08-10 EOD）

| 证明切片 | 状态 |
|---|---|
| `npm run check`（静态门 + build + unit + integration；含 unified-store-gate / student-teacher-absence / g4r-freeze） | GREEN |
| `npm run test:e2e` Long Stroke（`tests/e2e/entry.test.mjs` 唯一入口；spawn==1） | GREEN（48 steps / 5.0s；ceilings 367/367） |
| `npm run check:release` | GREEN（G4R Final outcome） |
| Storage G4 Exit Gate（§43 + §48，受 G3.5-A 修订；move completed） | **DONE**（`changes/completed/storage.md`） |
| 已知 residual | 无（旧 multi-canary 世界已删除；`manager-full-loop` flake 随旧 e2e 拓扑消失） |

## 0.4 合法中间状态（现在）

```text
✓ Causal waits 可解释；orchestrator canaries 无历史 timeout
✓ Student / Teacher / QA / SKILL = absent
✓ Meditator = reasoning only；SyncDelegate reuse Session
✓ Runtime durability = EventStore（Strategy A：AgentJournal 作 adapter surface）
✓ 无 legacy NDJSON writer / 无 dual-write / 无 migrator
✓ Storage completed（G4）
✓ JS capability-projected tools（G5；builtin 共存 + js-* 全链路）
✗ Casebook cold persistence / CaseFinalize（G6）
✗ Rulebook / Strength
```

---

# 1. 最终建议顺序

整个工程按以下十个 Gate 推进：

```text
G0  Governance + Baseline Freeze

G1  完成 Causal CE + 修复 Orchestrator canary

G2  Universal Runtime Foundation
    ReuseScope / SessionOwnership / SyncDelegate

G3  Universal Clean Break
    删除 Student / Teacher / QA / SKILL
    Meditator capability collapse

G4  Unified Storage
    EventStore / clean break cutover
    （无磁盘格式兼容；不读旧档）

G5  JS Capability-Projected Tools
    在最终 Agent/Capability 世界上实施

G6  perm-inspector + Universal Casebook completion
    CaseRefresh + CaseFinalize

G7  Rulebook v2

G8  Strength
    K0 → Shadow → Dry Run → K1 → K2

G9  Global Convergence / Ratchet / Release
```

一句话：

> **先把“怎么看见时序”修好，再把“Session 怎么拥有 Session”修好；然后删掉确定会消失的 Student 系统；再以 clean break 统一持久化（不必兼容旧磁盘格式，甚至不需要读旧档）；再统一文件工具；最后才实现依赖这些基础设施的持久知识、Rulebook 和 Strength。**

---

# 2. 为什么不能直接先做 `perm-inspector`

这是当前最容易产生大返工的错误顺序。

旧 `perm-inspector` 自己的实施方案仍包括：

```text
Casebook domain
→ Git raw store
→ custom local CAS
→ remote
→ hook
```

其原实施顺序明确有单独的 Git raw store、local CAS、pin refs 等步骤。

但 `storage` 已经明确裁决：

```text
Casebook 不再拥有自己的 storage
Casebook 只拥有：
    event semantics
    projection
    freshness replay
    LRU
    Bookkeeper

physical persistence
→ unified EventStore
```

并明确要求删掉 Casebook 自己的 custom ref/refspec/hook/LWW storage merge。

所以如果今天直接实现旧 `perm-inspector`：

```text
先写一整套 Casebook Git storage
→ storage Proposal 落地
→ 再整套删除
```

属于确定性返工。

**结论：**

```text
禁止在 Unified Storage 前实施
perm-inspector 的 physical persistence / remote / hook 部分。
```

---

# 3. 为什么不能先做 `strength`

Strength 当前 Phase 0 仍假设：

```text
SatelliteKind.Replica
Strength facts/projection
Projection intents
replay/candidate wiring
```

但 `universal` 将重构：

```text
Session ownership
Satellite/Attached 关系
Reusable specialist Session
Sync delegation
```

同时 `storage` 又要求 Strength 的 durable facts / payload 使用统一 EventStore。

如果先 Strength：

```text
先接旧 SatelliteRuntime
先接旧 Journal/Blob
先接旧 tool world

然后：
Universal 重写 ownership
Storage 重写 persistence
JS 重写 capability surface
```

等于三次迁移。

**Strength 必须最后。**

---

# 4. 为什么不能先做 JS Tool Proposal

JS Proposal 当前仍包含：

```text
js-student
js-teacher
js-meditator
StudentCompile migration
```

其原 implementation order 甚至明确包含：

```text
17. Agent surface migration
18. StudentCompile migration
19. suppress legacy five tools
```

而 `universal` 已批准的目标是：

```text
Student deleted
Teacher deleted
Meditator no read/glob/grep
```

所以先做 JS 会导致：

```text
实现 js-student
实现 StudentCompile JS
实现 js-meditator filesystem

→ 很快全部删除
```

**JS Tool 必须等 Student/Teacher clean break 后再实施。**

---

# 5. G0 — Governance + Baseline Freeze

任何生产修改之前先完成这一阶段。

## 5.1 不要创建第七个产品 Proposal

这份 Implementation Playbook：

```text
不是 product Change
不是正式 Clause
不是新的 architecture authority
```

它只协调现有 approved Changes。

不得因为“Proposal 很多”再创建一个：

```text
mega-proposal-v2
all-proposals.md
super-universal.md
```

重新复制所有语义。

---

## 5.2 保持 Change Governance

当前治理规则已经要求：

```text
用户明确启动 proposed
→ move proposed → active
→ 原文冻结
→ 只追加 Active work
→ 正式目标进入 why/what/shape/how/proof
→ 再改代码
```

所以后续每个 Proposal 都按这个流程启动。

本 Implementation Plan 不允许实现者：

```text
直接改 Proposed 原文然后声称已经实施
```

---

## 5.3 ~~先解决一个当前 governance 异常~~ — RESOLVED（2026-08-10）

~~当前 `changes/proposed/storage.md` 正文声明 Active 但 path 仍为 Proposed。~~

**已规范化：**

```text
changes/active/storage.md     ← Active work 唯一权威路径
原 proposed 正文              ← 冻结于 active 文件上方
Amendment G3.5-A              ← clean-break 语义已写入 Active work
```

此后不得再出现 path/status 双重事实。

---

## 5.4 建 baseline

在任何新大 Change 前：

```text
npm run build
node tests/unit/run.mjs
node tests/integration/run.mjs
node tests/e2e/run.mjs
```

已知 orchestrator 三个 canary 的 RED **已在 G1 修复**（`SharedState.BloggerFlights`；见 `changes/completed/orchestrator-e2e-timeout.md`）。后续新增 RED 不得归罪于 Universal/Storage，除非能证明与 G0 baseline 无关。

### G0 Exit Gate

必须得到：

```text
known green set
known red set
current active changes
current proposed changes
storage Active/Proposed 状态唯一化
```

以后任何阶段新增 RED，都必须能与这个 baseline 区分。

---

# 6. G1 — 先完成 Causal CE — **DONE**

> **状态：DONE**（2026-08-10）。`causal-ce-observability` + `orchestrator-e2e-timeout` 均已 `changes/completed/`；G1 Exit Gate 已满足。

这是第一项真正 production work。

~~当前 `causal-ce-observability` 已经在 Active，Remaining work 明确还有：~~

已完成项（摘要）：

```text
Phase 1 RED
Phase 2 CausalWait core
Phase 3 diagnostics bridge
Phase 4 Student–Teacher pilot
Phase 5 Orchestrator→Manager→Finality→Reviewer instrumentation
Phase 6 canary root-cause repair
Phase 7 Join/Recovery/Process waits（PARTIAL — Process 仍 physical）
Phase 8 static gate + DSL docs
Phase 9 full verification
```

## 为什么必须先做

后面的 SyncDelegate 会天然产生：

```text
Meditator waits Inspector
DevOps waits Coder
Coder waits Inspector
owner close waits CaseFinalize
Casebook fetch waits Bookkeeper
```

如果先实现 SyncDelegate，然后再做 Causal CE：

```text
所有新 await 点还要重新回来 instrumentation
```

而且新的 nested synchronous ownership 出问题时仍无法解释。

因此：

> **新的 SyncDelegate 从第一天就应该直接使用最终 CausalAwait，而不是先裸 Task/TCS，之后再补观测。**

---

## 6.1 完成 CausalWait 基础

先完成：

```text
DiagnosticWait
IWaitObserver
CausalWaitRegistry
CausalAwait
frontier calculation
diagnostic bridge
```

业务代码只写：

```text
let! result =
    CausalAwait.await...
```

不得让 diagnostic state 决定 workflow。

---

## 6.2 先把现有 Orchestrator 真实 bug 找出来

`orchestrator-e2e-timeout` 已明确：

```text
修 canary
MUST wait until causal frontier makes break edge visible
```

所以顺序必须是：

```text
instrument
→ reproduce
→ frontier
→ identify broken causal edge
→ fix root cause
→ rerun
```

禁止：

```text
increase timeout
sleep
weaken strict mock expectation
fake watchdog renewal
```

---

## 6.3 关闭两个 Active Change

顺序：

```text
causal-ce-observability complete
↓
three orchestrator canaries repaired
↓
orchestrator-e2e-timeout complete
```

### G1 Exit Gate

必须：

```text
npm run check
npm run test:e2e
```

全绿。

理想情况下：

```text
npm run check:release
```

也绿。

**没有这一 Gate，不进入 Universal。** — **已满足**（Universal 已 Active 且 G2/G3 完成）。

---

# 7. G2 — Universal Runtime Foundation — **DONE**

> **状态：DONE**（2026-08-10）。`changes/active/universal.md` Active work G2 exit 已勾选；证据：`SyncDelegateRuntime`、dual-await、`inspector-oneshot` Q1/Q2 reuse e2e、`devops-mechanical-repair-loop`。

~~现在才启动 `changes/proposed/universal.md` 并 move 到 active。~~ 已启动并冻结原文于：

```text
changes/active/universal.md
```

---

# 8. G2-A — 先只改 Session architecture，不删除 Student

这是 Universal 原 Proposal 自己也建议的安全顺序：先 generic dedicated sync delegate，Student 暂时不删。

## 8.1 正式 docs 先行

先修改目标规范：

```text
agent
host
execution
companion
prompt
dsl
architecture
```

定义：

```text
ReuseScope
SessionExecutionClass
SessionOwnership
AttachmentKind
AttachedSessionRuntime
SyncDelegateRuntime
```

---

## 8.2 先解决旧 Satellite 模型

不要在旧：

```text
SatelliteKind =
    Companion
    Teacher
```

继续硬塞：

```text
Inspector
Coder
Bookkeeper
Replica
```

先完成：

```text
ExecutionClass × Ownership
```

这层解耦。

目标类似：

```text
Work + Root
Work + Attached
InternalLeaf + Attached
```

---

## 8.3 Dedicated Inspector 必须是 Work

不要复制 Teacher 的：

```text
leaf
no Companion
```

只复用：

```text
send
→ await return
→ await completion
```

调用代数。

---

## 8.4 实现通用 SyncDelegate

首先只接：

```text
Coder → Inspector
Meditator → Inspector
DevOps → Inspector
DevOps → Coder
```

要求：

```text
same ReuseScope + role
→ same delegate Session

immediate caller scope
→ one active sync delegate at a time
```

但允许：

```text
DevOps waits Coder
Coder waits Inspector
```

不要把 gate 错绑 family root。

---

## 8.5 从第一天接 CausalAwait

必须看到：

```text
DevOps D
  waits-for Coder C

Coder C
  waits-for Inspector I
```

而不是裸：

```text
Task pending
```

---

## 8.6 先不碰 Student/Teacher

此时旧 Student/Teacher 仍然正常。

这样如果 SyncDelegate 出现：

```text
recovery
cancel
return routing
completion overlap
prefix drift
```

可以独立修，不和 clean-break 删除混在一起。

### G2 Exit Gate

真实三轮：

```text
Q1
Q2
Q3
```

必须证明：

```text
same SessionId
same Agent
same model
append-only prefix
return → completion
serial calls
cancel works
owner cascade works
causal frontier works
```

通过后才能删 Student。 — **G2 Exit 已满足**（2026-08-10）。

---

# 9. G3 — Universal Clean Break — **DONE**

> **状态：DONE**（2026-08-10）。Student/Teacher/QA/SKILL 已从 production 删除；`scripts/checks/student-teacher-absence.mjs` fail-closed；Meditator = `{ Inspector }` only；catalog 24→20。

这一阶段才做破坏性删除。

## 9.1 先迁 Meditator

Meditator prompt 吸收 Student 的 epistemic style：

```text
形成理解
提反例
追问证据
区分证据与推论
综合 Inspector
```

但没有：

```text
LearningPhase
QA
Compile
return
```

---

## 9.2 删除 Meditator filesystem

目标工具：

```text
Meditator
→ inspector
```

删除：

```text
read
glob
grep
```

先做 capability tests，再删实现暴露。

---

## 9.3 然后一次性删除 Student/Teacher

一起删除：

```text
Role.Student
Role.Teacher

fast/deep-student
fast/deep-teacher

student-system.md
teacher-system.md

StudentTeacher.fs
StudentTeacherPrompt.fs
StudentTeacherRuntime.fs
StudentTeacherTools.fs

StudentQaStore
StudentSkill
QA lifecycle
SKILL compile

StudentLearn
StudentCompile
Teacher request kind
Teacher return
Student final return
```

不要留 compatibility alias。

---

## 9.4 测试不要全部直接删除

旧 Student 测试先分类：

### 删除

只证明已删除产品行为的测试：

```text
QA persistence
StudentCompile
SKILL output
Teacher identity
```

### 提升

Teacher CE 中仍有价值的：

```text
same Session reuse
return → completion
single-flight
replacement
cancel
```

迁到：

```text
sync-delegate tests
```

---

## 9.5 立刻增加静态 ratchet

扫描：

```text
Role.Student
Role.Teacher
fast-student
deep-student
fast-teacher
deep-teacher
StudentLearn
StudentCompile
StudentQaStore
StudentTeacherRuntime
```

production 中必须为零。

### G3 Exit Gate

此时允许系统处于：

```text
Meditator + hot dedicated Inspector
Casebook cold persistence 尚未实现
```

这是**合法中间状态**。

因为 Universal 仍然 Active，不能关闭。

---

# 10. G3.5 — 修订 Storage cutover scope（无旧档迁移义务）— **DONE**

> **状态：DONE**（2026-08-10）。Amendment **G3.5-A** 已写入 `changes/active/storage.md` Active work；Student QA 标记 retired；禁止 migrator / dual-write / LegacyProjection≡NewProjection。

这是整个计划中必须显式处理的交叉点。

当前 Storage Proposal 仍把：

```text
Student QA
```

列为要迁入统一 EventStore 的 domain，并可能隐含：

```text
读旧盘
→ 投影等价
→ 迁入新店
```

但本 Playbook 的持久化立场更强：

> **不必保留磁盘格式兼容性；甚至不需要读旧档。G4 是 clean break，不是 format-preserving migration。**

同时 G3 之后 Student/QA 已经被产品 Clean Break 删除。

因此：

> **绝对不要先把 Student QA 搬进 EventStore，再下一阶段删掉。也绝对不要为任何已退休或即将被 EventStore 取代的旧 on-disk 状态编写 reader / importer / dual-write bridge。**

在 Storage 真正 cutover 前，必须把它的 Active Amendment 写清：

```text
Student QA 是已退休 domain。

旧 Student QA / 旧 Journal / 旧 Blob / 旧 feature store：
- 不要求可读
- 不要求可迁
- 不进入新 active domain projection
- 不作为新 EventStore ongoing vocabulary
- 不要求 LegacyProjection == NewProjection
- 允许丢弃或原地留存但 runtime 永不打开
- 代码与测试中的旧路径按 CleanBreak 删除，不得 silent 保留兼容 shim
```

这是 Cross-Proposal integration adjustment：

```text
不是恢复 Student
也不是“先迁再删”
而是承认旧磁盘世界不在兼容边界内
```

---

# 11. G4 — Unified Storage — **IN PROGRESS（收口）**

> **状态：IN PROGRESS**（2026-08-10）。Phase 0–7 **DONE**（inventory → EventStore core → GitGateway → clean-break policy → Wave-1 `AgentJournal` adapter → NDJSON substrate delete → proposed storage sections rewrite）。Phase 8：harness EventStore cutover + EventStore 性能（git ODB/ref CAS in-process）+ FALLBACK-013 **DONE**；e2e **26/26** + `--repeat 3` 绿。Storage → `changes/completed/` **待 G4 Exit 清单最终确认**。详情见 `changes/active/storage.md` Phase 8 Active notes。

现在进入最大的基础设施 Change。

Storage 自己已经明确：

```text
所有 dynamic durable state
→ 一个 EventStore

Casebook
Rulebook runtime observations
Strength candidate material
→ 全部依赖它
```

本 Playbook 额外冻结 cutover 边界：

```text
新 EventStore = 唯一 runtime 可读可写的 dynamic durability

旧磁盘格式：
    不要求兼容
    不要求 round-trip
    不要求读旧档
    不要求把历史状态搬进 EventStore

允许：
    丢弃旧 Journal / Blob / feature-owned store 内容
    或留在磁盘但永不被 runtime / migration tool 打开

禁止：
    dual write
    fallback old store
    “临时兼容一层读旧格式”
    为证明 LegacyProjection == NewProjection 而写旧档 reader
```

一句话：

> **G4 交付的是最终持久化世界，不是旧世界的翻译器。**

---

## 11.1 可以提前并行做什么

实际上 Storage 的以下工作可以从 G2 时就由独立 lane 开始：

```text
代码/domain inventory（不是旧档扫描义务）
RED architecture gates
EventStore pure domain
K-way merge unit tests
GitRawStore primitives
GitGateway unit work
```

因为它们和 SyncDelegate 几乎不重叠。

但：

> **Storage clean-break cutover 不得早于 G3 Student deletion。**

否则又会遇到 Student QA 这种本应直接消失、而不是被读档迁入的 domain。

---

## 11.2 严格执行 Storage Phase

顺序（与 `changes/active/storage.md` Phase 4 Active notes 对齐；**无 migrator**）：

```text
代码/domain inventory
→ RED gates
→ EventStore core
→ GitGateway
→ dumb server
→ Phase 4 clean-break policy / ratchets / docs
→ Phase 6 surviving-domain rewrite onto IEventStore
→ Phase 5 cutover delete Journal/Blob writers
→ proposal storage rewrite
→ full proof
```

说明：

```text
Inventory = 盘点代码与仍存活 domain 的权威落点
不是 = 扫描并解析每一份旧 on-disk 历史

Chicken-egg：
禁止在 Application/Session 仍调用 AgentJournal 时删除 Journal Writer/Boot。
因此 domain rewrite（Phase 6）必须先于 cutover delete（Phase 5）。
Journal substrate 删除被阻断，直到 consumer lane 改写完成。
```

没有完整 **代码/domain inventory** 禁止开始 cutover。  
但 inventory **不派生**“必须实现旧格式 reader”的义务。

若 Storage Proposal 原文仍写：

```text
migration tooling
legacy reader
LegacyProjection == NewProjection
```

激活后用 Active Amendment 收口为（**不恢复 migrator 义务**）：

```text
clean break
no disk-format compatibility
no mandatory old-archive read
no migrator / legacy reader / dual-write / LegacyProjection suite
surviving domains 只在新 EventStore 上重新落盘/重建
```

---

## 11.3 Cutover 是真正的墙

切换后必须不存在：

```text
legacy Journal writer
legacy runtime reader
任何为兼容而存在的旧格式 reader
RuntimePath blob writer
Student QA backend
dual write
fallback old store
migration tool 里的旧档 importer（也不需要）
```

注意：

```text
不是“只允许 migration tool 拥有 legacy reader”
而是“legacy reader 整体不需要存在”
```

旧档若仍留在磁盘上，那是废弃物，不是兼容面。

---

## 11.4 顺手修掉所有 Proposed 的 storage 假设

Storage Phase 结束前，修改目标说明：

### perm-inspector

```text
删除 custom Casebook ref/refspec/hook/store
```

### rulebook

```text
authored Markdown 仍是 repository source

runtime Observation
delivery history
coverage
→ EventStore
```

### strength

```text
Candidate facts
FrameBundleRef
PredictorSnapshotRef
→ EventStore/payload
```

Storage 已经明确要求这三者收口。

### G4 Exit Gate

必须：

```text
spec
architecture
dsl ownership
unified-store-gate
build
unit
integration
dumb-server
e2e
npm run check
```

全绿。

并且证明：

```text
runtime 只认 EventStore
无 legacy disk-format reader
无 dual write / fallback
无“为迁旧档而存在”的 tooling 依赖
旧 on-disk 历史不在兼容边界内
```

**当前：** 上述证明切片在 Phase 8 已基本满足；**待办**为正式 G4 Exit 签收 + move `storage.md` → `changes/completed/`。

---

# 12. G5 — JS Capability-Projected Tools — **BLOCKED（G4 Exit）**

> **状态：BLOCKED**。G3 clean break 已完成，但 G4 Storage 尚未 completed。仓库中已有 **prep-only** 提交（`8319771f` capability algebra）；**禁止** move proposed → active 或按原文执行 `js-student` / `StudentCompile` 路径，直至 G4 Exit。

到这里 Agent 世界已经稳定：

```text
no Student
no Teacher
Meditator no filesystem
```

Storage 也稳定。

现在才启动 JS Proposal。

---

## 12.1 激活时先做 Amendment/Rebase

不要照原文机械执行：

```text
js-student
js-teacher
StudentCompile migration
js-meditator filesystem
```

这些已经被 Universal 删除。

新的实施输入是：

```text
current Agent catalog
+
AttemptExecutionProfile.ToolCapabilitySet
```

---

## 12.2 第一阶段只建 capability algebra

严格按照原 Implementation Order 前半：

```text
Domain capability algebra
Capability Fragment Registry
JsToolGenerator
description/base-class renderer
ToolRegistry generated-name gate
```

先证明：

```text
capability absent
→ member absent
→ description absent
→ alias absent
→ forged call denied
```

再碰 sandbox/mutation。

---

## 12.3 再做 sandbox + transaction

顺序：

```text
sandbox
FileView/anchors
glob
rewrite/write staging
transaction engine
return serialization
Synthetic TOML bridge
```

---

## 12.4 Storage audit 特别注意 durable prepare

JS Proposal 有：

```text
durable prepare
crash recovery
```

这种动态持久状态。

不得为 JS 自己发明：

```text
js-transaction.db
transaction-v2.json
special ref
```

进入 G4 后所有 durable transaction facts 都必须服从 unified storage architecture。

---

## 12.5 最后迁移 Agent surface

这时才：

```text
Coder
Inspector
Reviewer
DevOps
Browser
...
```

迁 generated surface。

Meditator：

```text
没有 filesystem capability
→ 不生成 filesystem SDK surface
```

---

## 12.6 最后删除 legacy five-tool implementations

只有：

```text
generated primary
generated aliases
runtime gate
transaction engine
tests
```

全部完成后才删：

```text
legacy read/edit/write/glob/grep implementation specs
```

alias 名可以保留，但只作为 generated alias。

### G5 Exit Gate

必须证明：

```text
no handwritten role→JS matrix
no Student/Teacher JS
no Meditator filesystem JS
five-layer equivalence
transaction atomicity
crash recovery
sandbox escape RED
legacy implementation absent
```

然后 full gate。

---

# 13. 为什么 JS 要在 perm-inspector 前

Casebook 需要观察：

```text
read
glob
grep
recognized executor
```

如果先对 legacy FileTools 接 observation capture：

```text
perm-inspector done
→ JS proposal replaces file tool architecture
→ observation capture 再迁一次
```

所以最佳顺序是：

```text
final filesystem execution primitive
先稳定
↓
Casebook observation capture
再接
```

这样 Casebook 从第一天就观察**最终执行层**，而不是 legacy tool 名。

---

# 14. G6 — perm-inspector + Universal Casebook Completion — **NOT STARTED**

> **状态：NOT STARTED**。前置 G5（JS file primitive）尚未激活；Universal 仍 Active 等待 CaseFinalize / Casebook lifecycle。

现在才正式启动 `perm-inspector`。

激活后：

```text
Original proposal freeze
+
approved Amendments
```

不得偷偷改原文。

---

## 14.1 首先写清四个 Amendment

### A. Storage

旧：

```text
Casebook custom Git store
```

新：

```text
InspectorCase* events
CasebookProjection
→ unified EventStore
```

---

### B. Lifecycle

旧：

```text
one Inspector invocation
→ one Case
```

新：

```text
non-reusable Inspector scope
→ terminal archive

reusable Inspector scope
→ calls only capture
→ ReuseScope close
→ CaseFinalize once
```

---

### C. Bookkeeper

同一个 Bookkeeper Agent：

```text
CaseRefresh
CaseFinalize
```

两个 request contract。

不新建：

```text
LearningCompiler
CaseSynthesizer
StudentReplacement
```

---

### D. Ownership

```text
Dedicated Inspector
= Work + Attached

Bookkeeper
= InternalLeaf + Attached
```

不要继续依赖旧：

```text
Satellite can only belong to WorkSession
Satellite cannot recurse
```

语义。

---

# 15. G6-A — Casebook Domain First

先做纯 Domain：

```text
Case
Observation
ObservationIdentity
normalize/dedupe
CasebookProjection
LRU
freshness classification
```

没有 Host I/O。

这里原 `perm-inspector` 中值得保留的纯算法继续保留。

但是：

```text
revision + wall_clock LWW storage merge
```

不再属于 Casebook persistence substrate。

replica convergence 交给 EventStore。

---

# 16. G6-B — Observation Capture

现在接最终执行层：

```text
generated read primitive
generated glob primitive
generated grep behavior
recognized executor
fetch flattened evidence
```

要求：

```text
capture from typed execution
never infer from transcript text
```

---

# 17. G6-C — Non-reusable Inspector Path

先把简单路径打通：

```text
Inspector terminal
→ captured Q/A/evidence
→ InspectorCaseCaptured
```

证明：

```text
archive failure
≠ Inspector call failure
```

---

# 18. G6-D — fetch hot path

然后：

```text
index
fetch
replay
no delta → exact A
access event
```

先不启 Bookkeeper refresh。

证明 cheap hot path。

---

# 19. G6-E — CaseRefresh Bookkeeper

再加：

```text
changed evidence
→ CaseRefresh
→ edit-qa*
→ stability verify
→ InspectorCaseRefreshed
```

这里的 `edit-qa*`：

```text
一个 provider transaction 内
可以 0..N 次
```

---

# 20. G6-F — Reusable Inspector CaseFinalize

最后接 Universal 的核心目标：

```text
ReuseScope close
→ freeze draft
→ exactly one CaseFinalize provider transaction
→ evidence stability verify
→ InspectorCaseCaptured
→ retire/release reusable Inspector
```

禁止：

```text
每个 return finalize
每个 owner turn finalize
idle finalize
timer finalize
token threshold finalize
```

---

## 20.1 unexpected SessionDeleted

只：

```text
cleanup
```

不要 reconstruct + synthesize。

Casebook 是 cache，不值得建立 durable pending-finalize workflow。

---

# 21. G6-G — Universal 最终关闭

到这一步才允许关闭 Universal。

它的最终 e2e 必须完整证明：

```text
Meditator
→ same reusable Inspector
→ multiple questions
→ no Student
→ no Teacher
→ no QA
→ no SKILL

ReuseScope closes
→ one CaseFinalize
→ one Case

new Session
→ new Inspector
→ sees Case
→ fetch
```

Universal 自己已经要求真实 tool/Host/return/TurnCompleted/Casebook/future fetch 路径，而不是 helper 测试。

之后：

```text
Universal → completed
perm-inspector → completed
```

可以接近同一 integration window 关闭。

---

# 22. G7 — Rulebook

Rulebook 放在这里，而不是更早。

原因不是它依赖 Casebook，而是它会大改：

```text
Blogger
Enforcer
Context projection
Synthetic surfaces
durable observation
```

与前面的 Session/Storage/Tool/Casebook 都是横切面。

不要同时改。

---

## 22.1 authored content 与 runtime state 分开

Rulebook 的核心 authored source：

```text
120 directories
240 Markdown
```

继续普通 Git source。

其领域核心是：

```text
Observation =
    WorkLog
    TipIdentity
    Evidence?
    CycleIdentity
```

且 WorkLog/Tip 必须原子产生、持久化、projection、squash、recovery。

---

## 22.2 runtime persistence 一律 EventStore

禁止实现 Rulebook 自己的：

```text
journal file
blob store
coverage state file
delivery history json
```

统一：

```text
Observation events
→ EventStore
→ Projection
```

---

## 22.3 推荐内部顺序

```text
authored directory loader
→ rule identity
→ Observation pure domain
→ Event types
→ Projection
→ Blogger producer
→ Enforcer selection
→ Main delivery
→ squash/recovery
→ context/synthetic surfaces
→ authored catalog rewrite（不是读旧 runtime 磁盘格式）
→ static gates
→ e2e
```

说明：这里的 catalog rewrite 只触及 **repository 里的 authored Markdown source**，不恢复、不读取、不兼容旧 Rulebook runtime journal/blob。

---

## 22.4 为什么不和 Casebook 并行

虽然两个 feature 领域不同，但都碰：

```text
context
prompt projection
SyntheticToml
durable events
```

同时改会让：

```text
prefix regression
provider wire regression
projection regression
```

难以归因。

代码实现可以有独立准备 lane，但 integration 仍串行。

### G7 Exit Gate

Rulebook 单独 full gate 全绿后才能开始 Strength。

---

# 23. G8 — Strength 最后实施

现在基础架构终于稳定：

```text
CausalWait stable
Session ownership stable
Student gone
EventStore stable
JS capability projection stable
Casebook stable
Rulebook stable
```

此时才启动 Strength。

---

## 23.1 激活 Strength 后首先 rebase

原文中的：

```text
SatelliteKind.Replica
Journal StrengthProjection
RuntimePath blobs
```

不得机械实现。

重写目标落点为：

```text
new Attached Session ownership model
unified EventStore
current capability projection
current CausalAwait
```

---

## 23.2 先保持 100% K0

Strength 原本就要求：

```text
Phase 0 architecture splice
all feature decisions forced K0
```

然后 Host canary，不通过不得继续。

严格遵守。

---

## 23.3 Shadow Predictor

```text
100% K0
只记录 prediction
只观察 actual primary request
```

先证明预测目标今天还存在。

---

## 23.4 Replica Dry Run

真实：

```text
spawn/read-only work
```

但不影响 Main provider。

验证：

```text
permission
latency
bytes
stability
causal graph
cleanup
```

---

## 23.5 K1

只在：

```text
control holdout
```

存在时逐步启用。

收集：

```text
cost
latency
input bytes
fallback
repair
review/finality
user-visible failure
```

---

## 23.6 K2 最后

只有：

```text
K1 economic positive
quality no significant regression
promotion/recovery zero inconsistency
stable observation window
```

才允许 K2。

Strength Proposal 自己就明确禁止“代码写完直接跳 K2”。

---

# 24. G9 — Global Convergence

所有 Proposal 都完成后，最后专门做一次**全仓收口**。

不是顺手跑个 `npm run check` 就结束。

---

## 24.1 Symbol ratchet

production 中检查不存在：

```text
Role.Student
Role.Teacher

StudentLearn
StudentCompile
StudentQaStore
StudentTeacherRuntime

feature-owned refs/wanxiang/*
Casebook custom ref
Casebook custom refspec

legacy five tool implementation specs

old SatelliteKind.Teacher
old Strength SatelliteKind.Replica
（如果目标 ownership 已取消该表达）
```

---

## 24.2 Storage ratchet

只有 Persist/Git infrastructure 能拥有：

```text
canonical store ref
raw Git storage primitives
transport convergence
```

并且 production 中不存在：

```text
legacy Journal/Blob reader
旧 on-disk format compatibility layer
dual write / fallback old store
“临时迁旧档” tooling 依赖
```

Storage Proposal 已明确要求 architecture gate 防止 feature 再长出自己的 storage。  
本 Playbook 额外要求：旧磁盘世界不在兼容边界内。

---

## 24.3 Capability ratchet

随机生成多个：

```text
Agent
RequestKind
AttemptExecutionProfile
```

证明：

```text
capability
SDK method
description
example
alias
runtime
```

五层同构。

---

## 24.4 Session ownership ratchet

枚举所有 managed Session：

```text
Companion
SyncInspector
SyncCoder
Bookkeeper
Reviewer hidden session
Strength replica
fork agent
executor child
```

每一个必须回答：

```text
谁拥有？
是否 reusable？
谁 cancel？
谁 retire？
是否有 Handle？
是否有 Companion？
crash 后怎么 reconcile？
```

如果回答需要：

```text
“这个比较特殊”
```

直接 REVISE。

---

# 25. 推荐并行策略

整个计划不是要求所有开发完全串行。

可以分 Lane。

## Lane A — Current Active / Liveness — **DONE**

```text
Causal CE              ✓ completed
Orchestrator timeout   ✓ completed
```

当前 liveness 焦点转为 G4 收口 residual flake（`manager-full-loop`）与 G4 Exit 证明。

---

## Lane B — Universal Runtime — **G2/G3 DONE；G6 待做**

```text
ReuseScope / SessionOwnership / SyncDelegate   ✓ DONE
Student/Teacher clean break                     ✓ DONE
Casebook / CaseFinalize                         ○ G6（Universal 仍 Active）
```

---

## Lane C — Storage Foundation — **DONE（G4 completed）**

```text
Inventory / RED gate / EventStore core / GitGateway   ✓ DONE
Wave-1 adapter + NDJSON delete + proposed rewrite    ✓ DONE
Phase 8 perf + harness EventStore cutover            ✓ DONE
G4 Exit（§43+§48，G3.5-A 修订）→ changes/completed/  ✓ DONE（2026-08-10）
```

---

## Integration Gate 永远串行

以下不能重叠进入主线：

```text
Universal destructive delete          ✓ DONE（2026-08-10）
Storage cutover                       ✓ DONE（2026-08-10，G4 completed）
JS legacy tool removal                ✓ DONE（2026-08-10，G5 completed）
Casebook observation integration      ○ NOT STARTED（G6）
Rulebook context migration            ○ NOT STARTED
Strength promotion                    ○ NOT STARTED
```

每完成一个都先恢复：

```text
full green
```

再进入下一个。

---

# 26. 绝对不要并行的组合

## Universal ownership × Strength session implementation

禁止。

两者都改 Session topology。

---

## JS file runtime × Casebook observation capture

禁止。

必须 JS 先稳定。

---

## Storage cutover × Feature-owned persistence implementation

禁止。

Storage 先完成。

---

## Rulebook context rewrite × Casebook prefix/index integration

尽量不要。

都修改 provider-visible/context projection。

---

## Student deletion × JS StudentCompile migration

当然禁止。

前者要删除后者。

---

# 27. Proposal 激活时的标准动作

以后每个 Proposal 开工统一执行：

```text
1. 确认前置 Gate 已完成

2. move:
   changes/proposed/X.md
   →
   changes/active/X.md

3. freeze original proposal

4. append Active work:
   Work origin
   Cross-proposal prerequisites
   Approved Amendments
   Remaining work
   Completion criteria
   Blockers

5. 修改 formal docs:
   why
   what
   shape
   how
   proof

6. 写 RED

7. implementation

8. targeted unit

9. integration

10. affected e2e

11. global gate

12. Final outcome

13. move active → completed
```

不要：

```text
实现完代码
→ 最后才补 docs
```

---

# 28. 当旧 Proposal 与新基础设施冲突时怎么办

不要“择一相信”。

按 ownership plane 判断。

## Role / Session lifecycle

owner：

```text
Universal
```

所以旧 Proposal 中：

```text
Student
Teacher
old Satellite topology
```

必须按 Universal 激活时的 approved Amendment rebase。

---

## Durable persistence substrate

owner：

```text
Storage
```

所以其它 Proposal 里的：

```text
custom journal
custom blob
custom Git ref
custom sync
```

全部让路。

并且 Storage cutover 本身也遵守：

```text
不必保留旧磁盘格式兼容性
甚至不需要读旧档
无 dual write / fallback / legacy reader 义务
```

---

## File-tool execution surface

owner：

```text
JS Capability Projection
```

所以其它 feature 不重新维护：

```text
read/glob/grep permission matrix
```

---

## Inspector persistent knowledge semantics

owner：

```text
perm-inspector
+
Universal reusable lifecycle amendment
```

---

## Enforcer knowledge/delivery

owner：

```text
Rulebook
```

---

## speculative acceleration

owner：

```text
Strength
```

如果冲突发生在**同一个 ownership plane**，而现有 approved material 又没有明确谁覆盖谁：

```text
STOP
→ Active blocker
```

不得由实现者自行发明语义。

---

# 29. 每个 Gate 的最小提交策略

不要一个 Proposal 一个 20k-line mega commit。

建议：

```text
Commit A
formal docs + RED

Commit B
pure domain / types

Commit C
runtime infrastructure

Commit D
first integration path

Commit E
destructive legacy removal

Commit F
proof + ratchets + e2e

Commit G
close Change
```

这样 revert 和 bisect 都有意义。

---

# 30. 任何阶段失败时怎么处理

假设 G5 JS 卡住。

正确：

```text
Universal completed runtime state
Storage completed
JS stays Active
record blocker
stop dependent G6 Casebook integration
```

可以继续的只有真正独立工作，例如：

```text
Rulebook authored Markdown preparation
pure domain unit work
```

不能：

```text
“JS 卡了，先做 Strength”
```

因为 Strength 依赖更深。

---

# 31. 最关键的五个不要返工规则

## Rule 0

> **不必保留磁盘格式兼容性；甚至不需要读旧档。**

所以：

```text
旧 Journal / Blob / Casebook custom store / feature-owned refs
```

都不是兼容面。

G4 cutover：

```text
直接进入最终 EventStore
→ 旧档可丢弃或原地废弃
→ 不写 legacy reader
→ 不写 dual-write / fallback
→ 不证明旧盘投影 ≡ 新盘投影
```

---

## Rule 1

> **确定会被删除的东西，不迁移。**

所以：

```text
Student QA
```

先删 domain，再 Storage cutover。  
连“读旧 QA 档再清理”都不必做——旧档不在可读兼容边界内。

---

## Rule 2

> **确定会被统一的 substrate，不先实现 feature-specific 版本。**

所以：

```text
Casebook Git store
Rulebook state store
Strength blob store
```

都不先写。

---

## Rule 3

> **观察最终 primitive，不观察即将消失的 adapter。**

所以：

```text
JS file execution
先完成
→ Casebook observation capture
```

---

## Rule 4

> **优化最后做。**

所以：

```text
Strength
```

最后。

---

# 32. 最终路线图

把整个过程压缩成一张执行图：

```text
CURRENT（2026-08-10；HEAD ac41ef8f）
│
├─ Completed: Causal CE + Orchestrator timeout
├─ Active: Universal（G2/G3 DONE；G6 Casebook 待做）
├─ Active: Storage（G4 Phase 0–7 DONE；Phase 8 收口）
├─ Proposed: JS tools（prep only；G4 Exit 前勿 activate）
├─ Proposed: perm-inspector
├─ Proposed: Rulebook
├─ Proposed: Strength
└─ Proposed: magic-todo（Playbook 外；独立 Lane）
        │
        ▼
[1] Causal CE                              ✓ DONE
        │
        ▼
[2] Orchestrator canaries green              ✓ DONE
        │
        ▼
[3] Universal Session Architecture           ✓ DONE
    ReuseScope / SessionOwnership / SyncDelegate
        │
        ▼
[4] Delete Student / Teacher / QA / SKILL    ✓ DONE
    Meditator → Inspector only
        │
        ▼
[5] Unified EventStore                       ✓ DONE
    clean break cutover
    no disk-format compatibility
    no old-archive read
        │
        ▼
[6] Capability-Projected JS Tools            ✓ DONE
    final filesystem primitive
        │
        ▼
[7] Inspector Casebook                       ◐ ACTIVE（G6）
    EventStore
    CaseRefresh
    CaseFinalize
        │
        ├── Universal closes
        └── perm-inspector closes
        │
        ▼
[8] Rulebook                                 ○ NOT STARTED
    authored rules + Observation events
        │
        ▼
[9] Strength                                 ○ NOT STARTED
    K0 / shadow / dry run / K1 / K2
        │
        ▼
[10] Full ratchet + release                  ○ NOT STARTED
```

---

# 33. Definition of Done

只有以下全部成立，才能认为这一批 Proposal 真正“收敛”，而不是“分别实现过”：

```text
Causal waits 可解释
Orchestrator canary 无历史 timeout

Student = absent
Teacher = absent
QA = absent
SKILL learning pipeline = absent

Meditator = reasoning only
Inspector = evidence
Sync specialists reuse Session

所有 dynamic durability = one EventStore
no legacy disk-format reader
no old-archive migration requirement
no dual write / fallback old store

File capability = exact generated projection
legacy five tool implementation = absent

Reusable Inspector:
    hot transcript reuse
    one CaseFinalize on scope close
    cold Casebook reuse

Rulebook runtime state = EventStore observations

Strength:
    built on final Session ownership
    built on final storage
    built on final capability surface
    only gradually enabled

no feature-owned storage
no second permission matrix
no second session runtime
no second workflow state machine
```

最后执行：

```text
npm run build
node tests/unit/run.mjs
node tests/integration/run.mjs
node tests/e2e/run.mjs
npm run check
```

环境允许时再：

```text
npm run check:release
```

并确保：

```text
zero known hang
zero compatibility shim
zero "temporary old path"
zero planned cleanup left behind
zero mandatory old-disk reader
zero format-preserving migration debt
```

---

# 34. 实施者每天只需要问六个问题

开始任何下一项工作前，只问：

```text
1. 我现在在哪个 Gate？

2. 这个 Gate 的前置 Gate 全绿了吗？

3. 我正在写的东西，会不会在后面的 Proposal 中确定被删除？

4. 我是不是正在为某个 feature 新建
   storage / authority / permission matrix / session runtime？

5. 我是不是在为旧磁盘格式做兼容、读旧档、dual-write 或 fallback？
   （答案应为否；G4 是 clean break。）

6. 这一 Gate 完成后，仓库能不能重新回到一个可解释的全绿状态？
```

任何答案不理想：

```text
不要继续向下堆功能。
```

---

# 35. 最终原则

这批 Proposal 的难点不是代码量。

真正风险是：

```text
每个 Proposal 单独看都合理
↓
分别按照自己的 Implementation Order 实现
↓
它们对旧世界做了不同假设
↓
后一个 Proposal 删除前一个刚写的基础设施
```

所以这份 Plan 最重要的作用不是“项目管理”。

而是建立一个简单的架构施工原则：

> **先稳定底层事实，再实施上层语义；先删除确定会消失的旧世界，再实现仍然存在的世界；不必保留磁盘格式兼容性，甚至不需要读旧档；所有优化都建立在最终 ownership、persistence 和 capability substrate 上。**

按这个顺序，最终不是把六个 Proposal “都做一遍”。

而是让每一层只被实现一次。
