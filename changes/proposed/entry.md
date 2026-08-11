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

**进度快照最后同步：** 2026-08-11 下班（§0.1 是 observational living status，不覆盖后文 Product Exit Gate。G0/G1/G3/G3.5/G4 DONE；G5 DONE-with-amendment（C-3 user裁决）；G2/G6/G7/G8/G9 PARTIAL。见 §0.5 交接。）

> **Living status：** §0.1 Gate 总览是观察快照，不是对历史 Gate 正文的覆盖权。后文 G2 Exit、G6-E/F/G、G7 Exit、G8、G9 等 Product Exit Gate 仍是验收基线。

当前 Change 分布：

```text
Active:
  changes/active/strength.md       — G8 PARTIAL：K0 policy/transform unit proofs in tests/unit/strength/*；非 live Host canary；**不** claim K1/K2/shadow DONE；Change 仍 active，交并行 owner 续做；勿 playbook-close

Completed（本 Playbook 相关）:
  changes/completed/causal-ce-observability.md
  changes/completed/orchestrator-e2e-timeout.md
  changes/completed/storage.md                          — G4 DONE
  changes/completed/js-capability-projected-tools.md    — G5 DONE-with-amendment(C-3 user裁决)
  changes/completed/universal.md                        — G2 PARTIAL（runtime reuse canary green；PREFIX LAW unit canary cited, not Exit）+ G3 DONE + G6 PARTIAL
  changes/completed/perm-inspector.md                   — G6 PARTIAL（BookkeeperRuntime/EditQaTool/BookkeeperStaging cited; digest synthesizer gone; Host e2e / LLM Bookkeeper open）
  changes/completed/rulebook.md                         — G7 PARTIAL（mechanical A37/A38 production 120 GREEN after authoring；HUMAN_ONLY remaining；not Exit）

Proposed（Playbook 本身 + 独立 Lane）:
  changes/proposed/entry.md        — 本文件；Integration Playbook，不是产品 Change；不迁 completed
  changes/proposed/magic-todo.md   — 不在本 Playbook Gate 序列内；保持 proposed；不入 G0–G9 gates
```

已解决的历史 anomaly：`storage` / `universal` / `perm-inspector` / `rulebook` 文件在 `changes/completed/`。文件迁 completed 不等于 Product Exit Gate 已满足；Strength 仍 active。

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
| **G2** Universal Runtime Foundation | **PARTIAL** | runtime reuse canary only：`tests/unit/session/sync-delegate-runtime.test.mjs` (`G2_inspector_Q1_Q2_Q3_same_session_serial_reuse`, `G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child`)。Inspector PREFIX LAW unit canary（**not** Exit）：`tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs` :: `G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix`（reused-child `SendPrompt` → OpenAI body from dispatcher text + Return answers → `tests/e2e/support/provider-wire.js` `wireOf`/`sealHolds` + Domain `isAppendOnlyPrefix`）。optional `SyncDelegateRuntime` `promptModel`（after G6 `onInspector*` hooks）is G2 PREFIX LAW ModelId bind：`ChatParamsHook` leaves `Model=None` so Inspector Q1–Q3 `SendPrompt` must …
| **G3** Universal Clean Break | **DONE** | Student/Teacher/QA/SKILL 删除；Meditator → Inspector only；`student-teacher-absence` ratchet green |
| **G3.5** Storage cutover scope 修订 | **DONE** | Amendment G3.5-A；Student QA retired；no migrator / dual-write |
| **G4** Unified Storage | **DONE** | `changes/completed/storage.md` Final outcome；G4R（`changes/completed/test.md`）；`unified-store-gate` |
| **G5** JS Capability-Projected Tools | **DONE-with-amendment** | `changes/completed/js-capability-projected-tools.md` Final outcome；Amendment C-3 (2026-08-10 user裁决): builtin `read`/`edit`/`write`/`glob`/`grep`/`patch` retained, coexists with js-ROLE; legacy-absent clause superseded. |
| **G6** perm-inspector + Casebook | **PARTIAL** | Observational APIs（**not** Exit）：`BookkeeperRuntime.setSessionPort` / `runTransaction` / `isAttached` / `tryTxId`；`EditQaTool.execute`（document `Q.md`\|`A.md`, unique `old_text`）；`BookkeeperStaging.begin`/`read`/`replace`/`take`/`abort`。`AttachmentKind.Bookkeeper` `txId` lives in `BookkeeperRuntime`, not child options。digest synthesizer **gone** from `CasebookBookkeeper`。`SpikePlugin` calls `BookkeeperRuntime.setSessionPort` at `createHost`；`tryFinalizeInspector` is `Task`。`G6HostPathE2E` landed（**not** Exit）：`HostSignalBootstrap` SessionDeleted awaits `tryFinalizeInspector` Task before CancelSession；`SpikePlugin` passes `CasebookLifecycle.tryFinalizeInspector` (`Task`) and `BookkeeperRuntime.setSessionPort`…
| **G7** Rulebook | **PARTIAL** | Observation events DONE。mechanical A37/A38 on production 120 **GREEN** after authoring wave（root-cause + who owns）。**not** G7 Exit。`HUMAN_ONLY_RUBRIC_ITEMS` remain paired-history 120 / A39 pair review / A40 tournament。Machine evidence（**not** human Exit）：`tests/unit/enforcer/paired-history-eval.test.mjs`（catalog+history identity, **not** true-repeat oracle）；`scripts/checks/enforcer-cross-family-collision.mjs`（lexical A40, **not** human tournament）。 |
| **G8** Strength | **PARTIAL** | Change 仍 `changes/active/strength.md`（并行 owner）。Policy/transform **unit** proofs only（not live Host canaries, not K1/K2 DONE）：`tests/unit/strength/{host-canary-k0,host-policy,replica-transform,projection-algebra}.test.mjs` — `StrengthPolicy.decideFromFacts` / eligibility / budgetOf；`StrengthSettings.load` / `HostCanaryFingerprint` / `hostCanaryHealthy`；`StrengthReplicaTransform.apply`；`StrengthReplicaTools.exactReadonlyHostToolMap`；`StrengthFrame.isAllowedTool`；`StrengthReplicaAssociationHints`；`SatelliteKind.Companion` only；`PromptAuthority.systemPromptIdFor` / `toolCapabilitiesFor(..., StrengthReplica)`。G7 未 full Exit；G8 仍 active PARTIAL。 |
| **G9** Global Convergence | **PARTIAL** | smoke-check only, not full release-close。Cite: `scripts/checks/session-ownership-ratchet.mjs` + `session-ownership-matrix.json` + `tests/unit/verify/session-ownership-ratchet.test.mjs`（wired in `scripts/check.mjs`）。Closed kinds: Companion, SyncInspector, SyncCoder, Bookkeeper, hidden Reviewer, StrengthReplica, fork agent, Executor child。Bookkeeper `evidencePath` moved to `src/Wanxiangshu/Infrastructure/BookkeeperRuntime.fs` after G6 landed that runtime（was `SessionOwnership.fs`）。Symbol/storage/capability ratchets remain separate。 |

**当前主线位置：** G0/G1/G3/G3.5/G4 DONE；G5 DONE-with-amendment（C-3）；G2/G6/G7/G8/G9 PARTIAL。entry 保持 `proposed/`。§0.1 为观察快照，不覆盖后文 Exit Gate。

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

## 0.3 当前证明状态（2026-08-11）

| 证明切片 | 状态 |
|---|---|
| 静态 ratchet：`student-teacher-absence`（含 `SatelliteKind.Replica` 禁止）+ `session-ownership-ratchet` + `unified-store-gate` + `g4r-*` | **GREEN** |
| `enforcer-rulebook-gate --require-headings --strict` + `capability-isomorphism-gate` + `session-ownership-ratchet` | **PARTIAL**（G7: mechanical A37/A38 production 120 GREEN; HUMAN_ONLY not claimed。G9: ownership ratchet smoke-check, not release-close） |
| `npm run check`（lint + build + unit + integration） | **GREEN** |
| enforcer / strength / verify unit | **256 PASS** |
| `npm run test:e2e` Long Stroke | **GREEN**（48 steps；journal ceiling 372；三连稳定） |
| Storage G4 / JS G5 | **DONE / DONE-with-amendment(C-3)** |
| Universal+perm-inspector G6 / Rulebook G7 | **PARTIAL / PARTIAL** — G6 digest gone；BookkeeperRuntime+edit-qa landed；`G6HostPathE2E` landed but **not** live Host LLM / `tool.execute.before` Long Stroke；G7 mechanical A37/A38 production 120 GREEN; HUMAN_ONLY remaining |
| 已知 residual | G2 PREFIX LAW unit canary ≠ live Host Exit；Long Stroke G2/G6 mock preFlow **已接线**于 `1ee448d3`（`entry.test.mjs` + `long-stroke.toml` + `long-stroke-oracles.mjs`），**本下班点未重跑** `npm run test:e2e`，不得把接线写成 Exit。G6 unit `G6HostPathE2E` ≠ live Host。G7 HUMAN_ONLY（paired-history 120 / A39 / A40）。G8 live Host / K1/K2。G9 smoke-check only。magic-todo 仍 proposed。 |

## 0.4 合法中间状态（现在）— **G0/G1/G3/G3.5/G4 DONE; G5 DONE-with-amendment; G2/G6/G7/G8/G9 PARTIAL**

```text
✓ Causal waits 可解释；orchestrator canaries 无历史 timeout
✓ Student / Teacher / QA / SKILL = absent
✓ Meditator = reasoning only；SyncDelegate reuse Session
✓ Runtime durability = EventStore（Strategy A：AgentJournal 作 adapter surface）
✓ 无 legacy NDJSON writer / 无 dual-write / 无 migrator
✓ Storage completed（G4）
✓ JS capability-projected tools（G5 DONE-with-amendment C-3 user裁决）
◐ G2：runtime reuse canary green；PREFIX LAW unit canary cited (`g2-inspector-provider-wire-prefix.test.mjs`)；G2 Exit 未满足
◐ G6：digest gone；BookkeeperRuntime+edit-qa landed；`G6HostPathE2E` landed（inspector-tool → SyncDelegate → lifecycle → Bookkeeper → fetch unit）；**not** live Host LLM / `tool.execute.before` Long Stroke；digest deletion ≠ synthesis Exit
◐ G7：Observation events DONE；mechanical A37/A38 production 120 GREEN after authoring（root-cause + who owns）；HUMAN_ONLY remaining；identity/lexical machine evidence ≠ human Exit
◐ Strength：K0 policy/transform unit proofs in tests/unit/strength/*；非 live Host canary；Change 仍 active/strength.md（并行 owner；G8 PARTIAL；非 K1/K2 DONE）
◐ G9：`session-ownership-ratchet.mjs` smoke-check（kinds closed；Bookkeeper evidencePath now `src/Wanxiangshu/Infrastructure/BookkeeperRuntime.fs`）；symbol/storage/capability ratchets 另轨；非 full release close
○ magic-todo — 独立 Lane；保持 proposed；不入主 Gate
○ entry — Playbook；保持 proposed；不迁 completed
```

## 0.5 下班交接（2026-08-11）

**仓库：** `master` @ `1ee448d3`；working tree clean；与 `origin/master` 同步。本记录只更新 living status，**不**把任何 Gate 升成 DONE。

**用户当场裁决（下一班必须遵守）：**

1. Long Stroke = **mock LLM + 本机已安装的 OpenCode**，不是付费真模型。这是惯用方案，G2/G6/G8 的 Host 证明走这一条，**禁止**另开第二条 e2e。
2. **不准提升超时**（含 `WATCHDOG` / wall / waitFact 预算）。`1ee448d3` 已把 `maxJournalEvents` 372→480、`maxSseEvents`→2100；那是事件天花板不是 wall timeout，下一班若 e2e 红了，先缩 scenario / 修 mock，**禁止**用加超时换绿。

**本班已落地（机器，仍非原 Exit）：**

- G2 unit PREFIX LAW：`tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs`（fake `ISessionHostPort`）。
- G6：digest synthesizer 已删；`BookkeeperRuntime` + `edit-qa` + `BookkeeperStaging`；`SpikePlugin.setSessionPort`；SessionDeleted await `tryFinalizeInspector`；unit `tests/unit/casebook/g6-inspector-tool-finalize-fetch.test.mjs`。
- G7：mechanical A37/A38 生产 120 GREEN；HUMAN_ONLY 仍是 paired-history 120 / A39 / A40 tournament。
- G8/G9：K0 unit / ownership ratchet smoke；非 K1、非 `check:release`。
- Long Stroke **接线**（`1ee448d3`）：`preFlowNativeTodoCanary` 在 native-todo + strength 之后加了 deep-coder Inspector owner → Q1–Q3 child mock → `assertG2InspectorPrefixLaw` → `deleteSession` → `InspectorCaseCaptured` → `assertG6BookkeeperFinalize` → fast-coder `fetch`。toml `preFlowTurns` 含 `g2-inspector-*` / `g6-bookkeeper-*` / `g6-fetch*`；工程文件含 `.wanxiang/casebook/.keep`。

**本班未做 / 下一班第一件事：**

1. 在不提高超时的前提下跑 `npm run test:e2e`（唯一 Long Stroke）。绿了也只是 mock-Host 证据，**仍不是**把 §0.1 改成 G2/G6 DONE 的充分条件（Exit 正文还要求真实 prefix / 语义 CaseFinalize / 人工 G7 等）。
2. 若红：修 mock lane / Inspector return 双等待 / Bookkeeper `edit-qa` 回合，不要加 watchdog。
3. G7 语义审、G8 K1、G9 release-close 仍开着；G7 的 120 true-repeat / A39 / A40 仍须人工。

**不要再做的事：** 用 unit/fake port 宣布 Exit；删 Gate 正文；把 digest/scripted Bookkeeper 写成 semantic synthesis；把 `maxJournalEvents` 再当超时预算往上加。

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

[…1649ln elided…]

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
CURRENT（2026-08-11；§0.1 observational）
│
├─ Completed files: Causal CE + Orchestrator + Storage(G4) + JS(G5 C-3)
│            + Universal + perm-inspector（G2/G6 PARTIAL；G3 DONE）
│            + rulebook（G7 PARTIAL；mechanical A37/A38 production 120 GREEN；HUMAN_ONLY Remaining）
├─ Active: strength.md（G8 PARTIAL；并行 owner；勿 playbook-close）
├─ Proposed: entry.md（本 Playbook；不迁 completed）
└─ Proposed: magic-todo（Playbook 外；不入 gates）
        │
        ▼
[1] Causal CE                              ✓ DONE
        │
        ▼
[2] Orchestrator canaries green              ✓ DONE
        │
        ▼
[3] Universal Session Architecture           ◐ PARTIAL
    ReuseScope / SessionOwnership / SyncDelegate；runtime reuse canary green；PREFIX LAW unit canary cited, not Exit
        │
        ▼
[4] Delete Student / Teacher / QA / SKILL    ✓ DONE
    Meditator → Inspector only
        │
        ▼
[5] Unified EventStore                       ✓ DONE
    clean break cutover
        │
        ▼
[6] Capability-Projected JS Tools            ✓ DONE-with-amendment(C-3)
        │
        ▼
[7] Inspector Casebook                       ◐ PARTIAL
    BookkeeperRuntime/EditQaTool cited；digest synthesizer gone；Host e2e / LLM Bookkeeper open；Host-path unit ≠ full e2e
        │
        ▼
[8] Rulebook                                 ◐ PARTIAL
    Observation events DONE；mechanical A37/A38 production 120 GREEN；HUMAN_ONLY remaining；identity/lexical machine evidence ≠ human Exit
        │
        ▼
[9] Strength                                 ◐ PARTIAL（active/strength）
    K1/K2 / holdout — 并行 owner；unit policy/transform proofs only；G7 未 full Exit，G8 仍非 full DONE
        │
        ▼
[10] Full ratchet + release                  ◐ PARTIAL（G9）
    session-ownership + headings+strict + capability-isomorphism 已接线；ratchet 仍为 smoke
```

---

# 33. Definition of Done

> **2026-08-11 living status（observational）：** Product Exit Gates in this section remain the acceptance baseline. G0/G1/G3/G3.5/G4 DONE；G5 DONE-with-amendment（C-3 user裁决）。G2/G6/G7/G8/G9 PARTIAL。G6 digest gone; BookkeeperRuntime+edit-qa landed; `G6HostPathE2E` landed (inspector-tool path unit, not live Host LLM / tool.execute.before Long Stroke); no user amendment authority as Exit. G7 mechanical A37/A38 production 120 GREEN is not G7 Exit; HUMAN_ONLY (paired-history 120 / A39 / A40) remain; identity/lexical machine evidence is not human tournament. The checklist below is the *product* convergence DoD — **not** all-green.

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
    // SUPERSEDED by Amendment C-3 (2026-08-10 user裁决): builtin read/edit/write/glob/grep/patch
    // retained; coexistence is the Exit. G5 DONE-with-amendment.

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


[Showing lines 1-500 and 2150-2649 of 2649; 1,649 middle lines (26.9KB) elided. Read artifact://465 for full output. Some lines truncated to 768 chars]

[Some lines truncated to 768 chars]

[Some lines truncated to 768 chars]