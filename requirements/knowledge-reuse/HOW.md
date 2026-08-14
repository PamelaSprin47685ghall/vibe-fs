# HOW — knowledge-reuse 的实现模型与约束

> 非 normative。描述当前实现如何满足 WHAT；实现可整体替换（`17-repository.md` INDEPENDENT CHANGE：Case maintenance 换 deterministic merge + optional LLM 而 reuse semantics 不变）。

## 模块地图（当前实现）

### Domain（纯决策；零 Host I/O）

`src/Wanxiangshu/Domain/Casebook.fs`：

| 类型/模块 | 内容 |
|---|---|
| `Observation` | `FileRead(path, contentHash)` / `GlobResult(pattern, paths)` / `GrepResult(pattern, matches)` —— typed observation（KNOWLEDGE-REUSE-003） |
| `ObservationIdentity` | 同路径同内容去重的规范化身份（`read:` / `glob:` / `grep:` 前缀 + 排序后内容） |
| `Case` | `{ SessionId; Q; A; Observations; LastAccessOrder }`（LastAccessOrder 是 monotonic counter，不是 wall clock） |
| `CasebookEvent` | `CaseCaptured` / `CaseRefreshed` / `CaseAccessed` / `CaseEvicted` —— fold 输入（KNOWLEDGE-REUSE-007） |
| `ReplayResult` | `Fresh` / `Stale`（KNOWLEDGE-REUSE-004/005） |
| `Observations.normalize` | 按 identity 去重 + 稳定排序，同一证据折叠同一字节 |
| `Observations.classifyReplay` | 存储与重放集合精确相等 → Fresh，否则 Stale |
| `CasebookProjection.fold` | Captured 插入/替换、Refreshed 替换 Q/A/observations、Accessed 派生访问序、Evicted 移除；同 Case 多 head 由 EventStore 层表达 DomainConflict |
| `CasebookProjection.evict` | LRU：按 LastAccessOrder 淘汰，返回被淘汰 session id（tombstone 事件由调用方 append） |

### Infrastructure（Host 适配 + EventStore）

| 文件 | 内容 |
|---|---|
| `Infrastructure/CasebookCapture.fs` | `contentHash`；`ofReadExecution` / `ofGlobExecution` / `ofGrepExecution` / `ofExecCommand`（executor 命令 tokenize 识别：`cat`/`head`/`tail`/`sed` 单文件正例；`sh -c`/`bash -c`/命令替换安全跳过）；`capture(toolName, args, output)` |
| `Infrastructure/CasebookReplay.fs` | `replayOne`（当前 worktree 只读重放单个 observation）；`replayAll`（List.choose，捕获缺失的 observation 跳过） |
| `Infrastructure/CasebookWorkflow.fs` | `CasebookFeature.isEnabled`（marker = `.wanxiang/casebook` 目录）；`archiveInspectorResult`（Append Captured）；`fetchCase`；`checkFreshness`；`refreshCase`（Append Refreshed）；`needsRefresh`；`drainCollectorAndArchive`；`finalizeCase`（exactly-once）；`touchCaseAccess`（Append Accessed） |
| `Infrastructure/CasebookIndex.fs` | `Snapshot`（shelfmark + canonical question only）；`shelfmarkFor`；`resolve`（shelfmark → 内部 Case）；`refresh` / `invalidate`（epoch 推进）；frozen snapshot 进程内缓存 |
| `Infrastructure/CasebookStore.fs` | `CasebookStream = "casebook"`；事件类型 `InspectorCaseCaptured` / `InspectorCaseRefreshed` / `InspectorCaseAccessed` / `InspectorCaseEvicted`；`appendCaptured/Refreshed/Accessed/Evicted`；`loadEnvelopes` / `loadEvents` / `project`（fold） |
| `Infrastructure/CasebookLifecycle.fs` | `collector`；`setEnabled`；`notePrompt` / `noteAnswer`（draft 收集）；`tryFinalizeInspector`（ReuseScope close → exactly one finalize）；`cleanupInspector`（unexpected delete：零 EventStore 写）；`touchAccess` |
| `Infrastructure/CasebookSessionDraft.fs` | `CasebookDraftStore`（session → Q/A turns 的内存 draft） |
| `Infrastructure/CasebookBookkeeper.fs` | `refreshStale`（CaseRefresh：freeze → transaction → stability verify → Refreshed） |
| `Infrastructure/BookkeeperStaging.fs` | `beginTransaction` / `snapshot` / `apply` / `take` / `abort`（js-bookkeeper 的 staged 变换） |
| `Infrastructure/BookkeeperRuntime.fs` | `BookkeeperRequest = CaseRefresh | CaseFinalize`；`bindSession` / `unbindSession` / `tryTxId` / `runTransaction`（CreateChildSession + `js-bookkeeper` only + staging） |
| `Infrastructure/OpenCode/Tools/JsBookkeeperTool.fs` | `js-bookkeeper(program)` spec + execute：case SDK（`setQuestion`/`setAnswer` 各至多一次）+ runtime base class；无 filesystem capability |
| `Infrastructure/OpenCode/Tools/FetchTool.fs` | `fetch(shelfmark)` spec + execute：shelfmark 解析 → replay → Fresh/Refreshed/Stale consequence；`fetchGate`/`fetchInFlight`（same-worktree single-flight） |

### Session 交叉（不归本包 HOW 主体）

Bookkeeper child 生命周期（`fast-bookkeeper`/`deep-bookkeeper`、Clerk/Curator Persona、InternalLeaf + Attached）由 Session/Process 侧持有（历史 shape/casebook Bookkeeper 身份边界）；本包只消费 `BookkeeperRequest` 契约（KNOWLEDGE-REUSE-006）。

## 主流程

```text
Inspector 调用（复用或非复用 scope）
→ typed observation capture（read/glob/grep 工具执行）
→ scope terminal（非复用）或 ReuseScope close（复用）
→ freeze draft（Q 逐字 + A 逐字 + observations）
→ exactly one finalize/archive provider transaction
→ Append InspectorCaseCaptured（大正文 PayloadRef → store payloads）
→ CasebookProjection fold 更新 index

后续 fetch(shelfmark)：
→ CasebookIndexSnapshot（当前 epoch 冻结；provider 只含 shelfmark + canonical Q）
→ shelfmark 解析到内部 Case
→ 对当前 worktree replay observations（只读，不写）
→ no-delta → Fresh consequence + exact canonical A（freshness hint，非正确性证明）
→ delta → Bookkeeper CaseRefresh（js-bookkeeper* 0..N → stability verify → Refreshed）
→ 失败 → Stale consequence + 保留旧 canonical A（older account）
```

## 依赖（DEPENDS ON，逐条理由）

| 依赖 | 理由 |
|---|---|
| `repository-investigation` | fetch 的 replay 是真实观察（`CasebookReplay.replayAll` 对当前 worktree 重放 typed observations）；freshness hint 依赖「当前事实由真实观察建立」的保证；hint 永远不是 fact。 |
| `durable-events` | Case 事实以 `InspectorCase*` events + PayloadRef 进入统一 EventStore；durable authority、CAS、fold 由它提供（KNOWLEDGE-REUSE-007）。 |
| `durable-convergence` | replica 收敛 = EventStore set union；同 Case 并发 fork 的 DomainConflict 表达由 convergence 物理层提供（KNOWLEDGE-REUSE-011）。 |

## 历史与弃权

### 被拒方案（详见历史 change（perm-inspector）、历史 why/casebook 条款）

独立 Git store / refs / hook；timestamp / revision 决定 freshness 与 merge winner；逐调用 finalize；从 transcript 文本推断 observation；full knowledge base；无 marker 也运行；`edit-qa` 双文档字符串替换；Bookkeeper 借用 Inspector self-model；`(revision, wall_clock)` LWW。均记录于 `WHY.md` §历史拒绝方案。

### 判定为 HOW（非 normative；不入 WHAT）

- marker 目录名 `.wanxiang/casebook`、LRU capacity / prune key 权重、`CompletionTimeoutMs = 600_000` 等常数。
- `fast-bookkeeper`/`deep-bookkeeper` 机器身份、Clerk/Curator Persona、`js-bookkeeper` 工具的具体 JS SDK 形态 → 当前实现词汇（`participant-identity`/`session-ontology` 交叉）。
- digest synthesizer：历史 change（perm-inspector）曾规划 LearningCompiler / CaseSynthesizer；G6 Product Exit 明确 **synthesizer gone**——「不新建 LearningCompiler/CaseSynthesizer/StudentReplacement」是当前 absence（KNOWLEDGE-REUSE-006 边界），无合成器可迁移。

### 判定为 GARBAGE（migration/clean-break 沉积）

- Student/Teacher/QA bootstrap（`PROMPT-012` absence）：Casebook 的 G6-G 验证「无 Student/Teacher/QA/SKILL」是 migration ratchet，不进入永久 WHAT。
- 旧名 `edit-qa` 的兼容 alias：`js-bookkeeper` clean-break 后 `edit-qa` 非法；absence 由工具面保证（KNOWLEDGE-REUSE-006 现状），不另立命题。

### 不归本包（COVERAGE 交叉确认）

- 并发 DomainConflict 的一般收敛律与 `tests/unit/persist/event-store-merge*` → `durable-convergence`。
- Semble 低信任 hint 与 warm-start 管线 → `repository-investigation`（AGENT-027/032）。
- Inspector 的取证权（read/glob/grep 能力、Inquiry→Inspector 分层）→ `repository-investigation`/`office-capability`/`capability-enforcement`。
- ReuseScope / SyncDelegate / Attached session 生命周期 → `managed-session-lifecycle`/`session-ontology`/`delegation`。
