# Todo — 所有权与边界

行为：`what/todo.md`（TODO-001..015）。程序：`how/todo.md`。证明：`proof/todo.md`。

本文件只划**唯一 owner** 与 **禁止平行 owner**；不复述条款语义。治理指针见 TODO-014。

## 模块所有权

| 关注点 | 唯一 owner | 边界 / 禁止 |
|------|------|------|
| Provider-visible `todowrite` wire（`obligations: [{ name, work }]`；schema / description / decoder / result renderer 同源） | Magic Todo definition / codec module | 禁止 `kind`/`id`/`status`/`priority`/`reviewing` 回流；禁止 before/after/test 另写 schema（TODO-002、TODO-012） |
| `CurrentObligations` | `MagicTodoProjection`（Journal fold；**last `TodoWriteAccepted` → matching Prepared.Submitted**） | Reviewer verdict / Host `TodoTable` **都不是** writer；不得反向 adopt 或 rollback（TODO-005/007） |
| Checkpoint + process-review obligation SSOT | `TodoWriteAccepted` | `Prepared` alone 不派生 Rk；Host store 已写 ≠ Accepted（TODO-004、TODO-006） |
| ProviderInput / BaseObligations / Submitted / ReviewFrontier 冻结 | `TodoWritePrepared` | `ProviderInputDigest` = canonical `{obligations:[{name,work}]}` digest；禁止事后重猜 frontier（TODO-004、TODO-006） |
| Accepted supersession | `MagicTodoProjection.foldAccepted`：Current ← matching Prepared.Proposed | `TodoReviewConcluded` 只封口 review；禁止 reviewer settlement / semanticMerge / accepted-but-not-current（TODO-005） |
| ConsumableReview | `TodoReviewConcluded`（≡ ConsumableReview） | `VerdictKnown` 属 Reviewer 域，不得冒充可消费；禁止 AwaitingReport Stage（TODO-006、TODO-012） |
| Process-review assignment range | `TodoProcessReviewAssigned.ReviewWorkStartCursor` | exclusive end after assignment authority；禁止 session head / Opening 冒充（TODO-006、TODO-008） |
| OpeningPolicy = BlindPlan（Manager） | GLORY-074；本域消费 commitment = first accepted todowrite | Companion / Prompt 不另造 BlindPlan PC |
| `WorkRecordStart` / Opening Blogger floor | 自 `LifeOpened` / Opening cursor **纯推导**（TODO-001） | 禁止绑回 `WorkActivated`；禁止平行 stage floor（TODO-012） |
| BlindPlan T1 commitment + entrustment revelation | TODO-015（first `TodoWriteAccepted` → canonical T1 result） | 交托只经 conversation tool result；禁止 system prompt / Persona / Role Law 切换（PROMPT-014；GLORY-075） |
| Pre-T1 / T1 / Post-T1 冻结文案 | TODO-015 分段 owner（Planning Table / T1 revelation / Living Mission / idle） | SURFACE-004；不得并入全局 pair guideline 或 Role Law |
| `MagicTodoManagerGuideline` | TODO-013（Manager-only fragment） | 禁止并入 `host/pair-programming-guideline` |
| Process-review 工作证据 | 既有 canonical LWR + `RecordCoverage` | 三段标题 COMPANION-003；禁止第二 renderer / session-head LWR（TODO-008、TODO-012） |
| Lag-1 prefix 可替换性 | 既有 `PrefixCoverage` + `PrefixRebaseCommitted`（`EvidenceKind=TodoCheckpoint`）→ `ActivePrefixEpoch` | 禁止 todo-only 第二套 ActivePrefixEpoch；禁止 RawGap 做 prefix replacement（TODO-008/009/012） |
| Desired lag-1 cutoff 事实源 | Accepted checkpoint 链纯推导 | 禁止 `NeedRebase` Stage；Accepted **不**直接 commit epoch（TODO-006/009/012） |
| Dedicated process reviewer 逻辑身份 | `DedicatedTodoReviewerEnlisted`（+ proven-loss `Replaced`） | 每 Life 一个 logical id；物理 retention ≠ Finality graduate（TODO-008、TODO-010） |
| Finality drain 入口 | Manager `suicide` 前序（GLORY Finality CE 扩展） | 零 `TodoWriteAccepted` 的 first unblessed path fail closed；不另造 mechanical todo-completeness gate（TODO-010） |
| Manager-visible process surface | enriched `todowrite` tool result + safety-sealed ProcessReviewLWR | 允许 outcome / report；禁止泄漏 reviewer session / barrier / witness / 2N（TODO-013） |
| Compatibility sink | Host `TodoTable` writer（membrane projection + drift repair） | 只投影；REVISE 不 rollback；repair **不**产生 checkpoint/review（TODO-007） |
| V1 membrane 执行路径 | Host tool hooks（definition / before / after）；细节 HOST-* | V2 runner 无 hook parity 应在启动/构造 gate 阻止；若运行时仍错入则 `Diagnostic.fatal`，不得 tool red（TODO-004） |
| Legacy seed | 一次性 `LegacyTodoSeedAdopted`（仅升级瞬间 legacy open Life） | 正常新 Life canonical 空；禁止同 session 后续 Life 再 adopt Host table（TODO-011） |
| Obligation checkpoint projection | `MagicTodoProjection` | **只** Accepted→Current + review obligation；没有 settlement writer / semanticMerge；**不是**工作记录 renderer（TODO-005/007/008） |
| Process-review / Finality 证据物化 | `LifecycleWorkRecordProjection`（既有 `lifecycleWorkRecord` range API） | 禁止在 Todo 树新增平行 work-record module（TODO-008） |
| Journal durable facts | 既有 EventStore / fact owner | 禁止另造 JSON 状态文件或 ephemeral 当 durable（TODO-004/012） |

## 建议模块落点（ownership 不变）

实现路径随仓库命名；**不得因路径不同改变上表 owner**（TODO-014）：

```text
Domain/        TodoCheckpoint · TodoIdentity · TodoSettlement · TodoReview · TodoPrompt
Application/   TodoCheckpointProgram · TodoProcessReviewProgram
Infrastructure/OpenCode/
  TodoWrite{Definition,Before,After}Hook · TodoCheckpointBridge
  TodoCheckpointProjection · DedicatedTodoReviewerRuntime
```

CE 表达 facts 上的递归等待（Journal 变化 → 同 snapshot 重读），**禁止**一阶 `WaitingReview|Settling|…` 大状态机（TODO-012；算法见 `how/todo.md`）。

## 事实边界（交叉引用）

```text
LifeOpened + WorkRecordStart     → Life / Opening floor（TODO-001）
OpeningPolicy=BlindPlan + T1     → Opening closes；WorkRecordStart（TODO-015；GLORY-074；COMPANION-014）
TodoWriteAccepted                → checkpoint + Rk obligation（TODO-004/006）
MagicTodoProjection              → CurrentObligations（TODO-002/007）
frontier/request-range LWR       → process / Finality 证据（TODO-008）
PrefixCoverage + ActivePrefixEpoch(TodoCheckpoint) → lag-1 Y 替换（TODO-009）
既有 Finality witness/cohort     → 终末 2N（TODO-010；GLORY/REVIEW）
```

## 禁止的平行 owner

同一 Manager Life **不得**并行存在第二套：

| 禁止平行 | 唯一真相 |
|------|------|
| LWR / process-review evidence renderer | 既有 canonical LWR machinery（range / includeOpening / coverage 分型）（TODO-008） |
| PrefixEpoch / ActivePrefixEpoch SSOT | 既有 PrefixRebaseCommitted 合同 + `EvidenceKind=TodoCheckpoint`（TODO-009） |
| Stage / PC（TodoStage、ReviewStage、AwaitingTodoReview、NeedTodoRebase、HasPendingReview…） | facts 推导：Accepted 缺 Concluded ⇒ obligation pending（TODO-006/012） |
| PlanningTail / ManagerWorkActivation / 新 WorkActivated 业务资格 | 删除；Opening floor = `WorkRecordStart`；BlindPlan 文案 = TODO-015（TODO-001） |
| provider 冷状态机（kind/id/status/priority/reviewing/semanticMerge） | `obligations: [{name, work}]` only；account 只描述 mission debt，不描述“计划/分析/写 todo”这类 meta-work（TODO-002/003/015） |
| Host TodoTable 作 canonical / recovery SSOT | Journal projection only（TODO-007） |
| ephemeral JS bridge 作 durable truth | Journal Prepared/Accepted only（TODO-004/012） |
| ordinal winner 仲裁同 message 多 todowrite | 全部作为 syntax/protocol error 拒绝；这是允许 provider 红字的类别（TODO-004） |
| process PERFECT 计入 terminal dual-PERFECT | 分型保留；enlist 后 fresh 2N（TODO-010） |
| Sphinx Kernel / MCP observation 假装 Magic Todo 层 | Sphinx 独立认识状态 owner；不得写入 Todo owner 表（SPHINX-005） |

## 数据流边界（成功路径）

```text
provider todowrite(obligations)
→ definition（schema owner，TODO-002）
→ before：捕获 live obligations + V1 sink 投影 + 启动 deferred prepare
→ deferred prepare：materialize input → await/synchronize R(k-1) + admission + Prepared + bridge
→ Host executor（compatibility sink，TODO-007）
→ after / recovery：Accepted → Current:=Submitted → ensure Dedicated → ensureReview(Rk)
→ T1：canonical revelation result → Opening closes（TODO-015）
→ desired cutoff 可推导（尚未 PrefixEpoch，TODO-009）
→ 下一 provider transform：PrefixCoverage Y → seal 前 PrefixRebaseCommitted(TodoCheckpoint)
→ Manager 继续独立工作 ∥ Dedicated process review（TODO-006/008）
→ 下一次 before / suicide 消费 TodoReviewConcluded（TODO-006/010）
```

## 与邻域关系

- **GLORY**：OpeningPolicy/BlindPlan 定义在 GLORY-074；删除 Activation 业务路径（TODO-001）；`suicide` 前序接 TODO-010 drain；Dedicated Finality enlist/graduate 见 TODO-010；Manager 面泄漏边界收窄为 TODO-013（GLORY-030/SURFACE-005 例外 = process report 词面，非 reviewer 编排）。System prompt 稳定 = GLORY-075 / PROMPT-014。
- **COMPANION**：OpeningMaterial / 三段标题 / constitutive T1 call+result 属 COMPANION-014/015；Todo 只推导 `WorkRecordStart`，不拥有 Opening 重建权。
- **REVIEW**：`VerdictKnown` 复用 Reviewer 域（TODO-006）；process review 一次 verdict，不进 dual-PERFECT witness 代数；Finality cohort 规则不因 Magic Todo 发明永不 graduate（TODO-010）。
- **CONTEXT / PERSIST / ARCH-004**：lag-1 cold boundary 仅经既有 PrefixEpoch（TODO-009）；transform 只 render，不另造 epoch owner。coverage 分型 TODO-008。
- **HOST**：membrane canary、tool 身份、pair-programming 通用 marker、`SessionProviderLanguage` 绑定仍 HOST-*；MagicTodoManagerGuideline / BlindPlan 文案为 Manager-only（TODO-013/015），不得并入全局 pair 正文 owner。admission/V2 门禁语义 TODO-004。
- **PROMPT / PROJ**：只交叉引用 TODO-013/015 表面与 TODO-007 投影边界，不复制 settlement/cadence 语义；不得经 Prompt 伪造 Activation / checkpoint。
