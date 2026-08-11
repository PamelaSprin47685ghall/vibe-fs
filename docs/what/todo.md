# Magic Todo — 可观察行为

条款前缀：`TODO-`。  
本文件是稳定 `TODO-*` 语义的**唯一**所有者；跨域机制只引用条款，不复制合同。  
所有权 / 算法 / 证明见 `shape/todo.md` · `how/todo.md` · `proof/todo.md`。  
理由与被拒方案见 `why/todo.md`。

## TODO-001：生命周期与 WorkRecordStart

Magic Todo 仅约束 **canonical role = Manager** 且 `todowrite` 对 provider 可见的 Life。

```text
HumanRoot → LifeOpened → 立即真实工作
→ todowrite checkpoint → 工作 → …
→ suicide / Finality
```

删除生产路径上的 planning-only → Activation 两阶段：`PlanningTail`、`ManagerWorkActivation`、`WorkActivated` 业务资格、Birth/Labor compression floor、Activation-only suicide gate。历史 Journal 中的 `WorkActivated` 可 decode，但是 **inert legacy fact**——新决策不得用它决定能否工作、压缩或 Finality。

持续节拍唯一来源：每一次成功受理的 `todowrite` = 一个 Todo Checkpoint（`TodoWriteAccepted`）。

**WorkRecordStart（Opening 结构性 floor）**：

```text
ManagerLife.WorkRecordStart
  = Opening HumanRoot semantic range 的 exclusive end
```

由 `LifeOpened` / XTrace Opening cursor **纯推导**，不是 Stage fact。

```text
Manager Blogger effectiveStart = max(RecordCoverage, Life.WorkRecordStart)
```

Opening 永久 raw：永不被 Y 替换；不随 TodoCheckpoint rebase 消失；不经 process-review LWR 再复制（`includeOpening=false`）。  
删除的是 planning/labor stage floor，**不是** Opening protection。

## TODO-002：Tagged schema / identity

Provider-visible `todowrite` 使用结构上不可混淆的 tagged union，禁止 `id?: string` 靠缺字段猜新旧：

```text
kind:"existing" + id  → 必须引用 canonical old list 已有 identity
kind:"new"            → 禁止携带 id；仅 Host 分配 id
```

同一 proposed list：`existing` 之间 duplicate id → fail closed。  
禁止靠 content 文本猜 identity。  
`tool.definition` 是 provider-visible V2 schema 的语义合同锚点；membrane 投影细节见 Host / `how/todo.md`。

## TODO-003：状态代数与 completed 门禁

```text
TodoStatus = pending | in_progress | reviewing | completed | cancelled
```

生产进度链：`pending < in_progress < reviewing < completed`。

**Completed gate（硬）**：proposed `completed` 时，old 必须恰好是 `reviewing`（或已是 `completed` 的幂等保持）。  
禁止 `pending|in_progress|cancelled → completed` 直接跳跃。

除 completed gate 外，其它 status 转移在 Host 层均允许（含 `completed→in_progress`、`cancelled→pending`、`reviewing→cancelled` 等）；真实性由 process review 判断，不另造机械枚举机。

## TODO-004：Admission、replay 与 V2 门禁

1. **同 message 多 todowrite**：同一 assistant message 出现 >1 个不同 `ToolCallId` 的 `todowrite` → **全部 fail closed**；无 ordinal winner、无 hook 到达顺序/wall-clock 仲裁。  
2. **单 inflight**：同一 Manager Life 同时最多一个新 checkpoint admission。  
3. **Same ToolCallId replay**：幂等为同一 `TodoWriteId` / 同一 obligation；input digest（及 Life / BaseTodo / ordinal 合同）必须与既有 Prepared 一致，否则 identity corruption fail closed；不新增 checkpoint / review。  
4. **不同 ToolCallId**：即使 list 相同也是新 checkpoint。  
5. **Physical success 双路径**（live after-success / recovery completed ToolPart）必须收敛到同一 `TodoWriteId + input digest + output digest` 才可 `TodoWriteAccepted`。

**V2 fail-closed**：在 V2 runner 获得与 V1 等价的 tool definition / before / after hook contract 及 canary 证明之前，Magic-Todo Manager Attempt **不得**使用 V2 todowrite execution path。不是「V1 有协议、V2 暂时裸奔」。无 hook parity → 禁止上线；长期不维护两套不同 Magic Todo 语义。

## TODO-005：Settlement 与 semantic merge

```text
settle(Ck, Pk, PERFECT) = Pk          // 完全替换，不 merge
settle(Ck, Pk, REVISE)  = semanticMerge(Ck, Pk)
```

记号：`Ck` = Tk 开始时已结算 canonical old list；`Pk` = 本轮 normalized proposed list。

REVISE merge（冻结裁决）：

- union by stable id；progress 取 **min**（productive chain）；  
- same-id 的 content / priority：**proposed 赢**；真正迟滞的是 status；  
- 任一侧 `cancelled` 且 status 冲突 → **保留 old.status**（未经 PERFECT：不自动取消也不自动复活）；  
- 仅 old / 仅 proposed 的成员按协议并入（细节 `how/todo.md`）。

tool result 必须可稳定呈现：上一 ConsumableReview 的 verdict + canonical ProcessReviewLWR、settled current、submitted list、以及当前若 REVISE 时的 merge preview；并明示 PERFECT 时 preview 不生效、以 submitted 为准。

## TODO-006：评审节拍与 ConsumableReview

严格 **1:1 lag-1**：

```text
Tk = 第 k 个 TodoWriteAccepted
Rk = Accepted(Tk) 派生的第 k 次 process-review obligation

TodoWrite k  consumes Review(k-1)
             creates  Review(k)
```

Rk **不**阻塞 Tk 返回；Manager 可立即做后续独立工作。  
T(k+1) 到来时若 Rk 尚未形成 ConsumableReview → **必须阻塞**直至 `TodoReviewConcluded(k)` durable。

```text
VerdictKnown(k)
  = Reviewer 域已有 durable process verdict
  → 立即决定业务 outcome（PERFECT | REVISE）
  → 不单独构成可消费结论；不携带 WorkRecordRef
  → 不进入 Finality dual-PERFECT witness 代数

ConsumableReview(k) ≡ TodoReviewConcluded(k)
  = VerdictKnown(k)
    AND 该 verdict frontier 的 canonical ProcessReviewLWR 已 record-ready
    AND 同 snapshot 物化 WorkRecordRef / Digest
  → 才允许下一 TodoWrite / suicide drain 消费上一报告
```

禁止用同一个 `TodoReviewConcluded` 表达「只有 verdict、尚无 report」的中间态。  
禁止另造 `AwaitingReview` bool / `ReviewStage`。  
`VerdictKnown` 复用 Reviewer 域 durable verdict（REVIEW-*），不新增 Magic Todo Stage。

## TODO-007：Canonical 投影 vs Host sink

```text
MagicTodoProjection / Journal facts  = canonical semantic truth
Host TodoTable                       = compatibility sink only
```

禁止用 Host TodoTable 恢复或反推 canonical todo truth。  
`kind` / Magic id 在 before 投影到 Host 前剥离；canonical status 不得被 sink 策略改写。

**Compatibility sink reconciliation（P0）**：REVISE 被消费且 canonical settlement 改变后，Host TodoTable 必须幂等投影到 settled current。该项 repair：

```text
不产生 checkpoint
不触发 process review
不改 canonical truth
```

Accepted 后 sink 可短暂显示 working `Pk`；settlement 消费后必须 reconcile。

## TODO-008：Dedicated reviewer、bounded LWR 与 coverage 分型

每个 Manager Life 复用 **一个** dedicated process reviewer physical session，至少保留到 `LifeCompleted`（或 proven-loss replacement）。  
Evidence SSOT：process review input / reviewer report / Finality work record 一律复用既有 canonical `LifecycleWorkRecord`（LWR）。  
**禁止**第二套工作记录 renderer / `TodoProcessReviewEvidenceProjection`。

共享唯一证据源：XTrace、Y frames、frontier identity、既有 LWR planner。

| 用途 | frontier | materialize | coverage |
|------|----------|-------------|----------|
| Process Rk | `ReviewFrontier(k)=Before(Tk)` | canonical LWR `includeOpening=false` | `RecordCoverage`（允许 canonical RawGap） |
| Manager lag-1 rebase（TODO-009） | `TodoRebaseCutoff(Tk)=Before(T(k-1))` | proven Y prefix only | `PrefixCoverage`（禁止 RawGap） |

二者不得互转：禁止用 RecordCoverage 证 prefix 可替换；禁止用 PrefixCoverage 填 LWR gap。  
Process 与 Finality 的 dedicated LWR 均 **request-range bounded**，不得取 session head。  
Y 未覆盖 frontier ≠ review 不可开始：合法 canonical RawGap 已是完整 process evidence。  
PERFECT 与 REVISE 在 verdict 前都必须产生本 request 的 canonical ProcessReviewLWR（无 prose 的 PERFECT fail closed）。

跨域对 LWR / coverage split 的引用统一指向本条；rebase commit 见 TODO-009；Finality enlist/graduate 见 TODO-010。

## TODO-009：PrefixEpoch 与 lag-1 rebase

Accepted checkpoints 只使 **desired** lag-1 cutoff 可推导；**不**在 todowrite after 提交 PrefixEpoch。

```text
desiredCutoff(Tk) = Before(T(k-1) tool-call)   // T1 无 prior
```

下一 provider attempt seal/绑定前原子 `PrefixRebaseCommitted`（`EvidenceKind=TodoCheckpoint`），进入既有 `ActivePrefixEpoch` SSOT；provider 成败不回滚已 seal epoch。  
rebase 只消费 PrefixCoverage 可证明的 Y prefix；禁止把 LWR RawGap 冒充可替换 prefix（coverage 分型见 TODO-008）。  
不另造第二套 PrefixEpoch SSOT；不要求物理改写 OpenCode history——Journal fact = durability，`messages.transform` = renderer。  
Cutoff 取 `Before(previous TodoWrite)` 而非 `After(result)`，以保留上一 checkpoint call/result 至少一个 raw X 节拍。

跨域对 TodoCheckpoint / PrefixEpoch rebase 的引用统一指向本条。

## TODO-010：Finality 尾 drain 与 dedicated 毕业

`suicide` 是最后一个尚未被下一次 todowrite 消费的 process review 的**唯一** tail drain（禁止再调一次 todowrite flush——会创造 R(k+1)）。

```text
first unblessed suicide
  AND this Life has zero TodoWriteAccepted
  → fail closed
```

有 checkpoint 时：await latest ConsumableReview ≡ TodoReviewConcluded → settle；REVISE → sink reconcile（TODO-007）、返回报告、**不**建 FinalityRequest、Life 继续；PERFECT → 进入既有 Finality 前置。  
「至少一次 TodoWriteAccepted」≠ 机械要求 todos 全 `completed`。未完成项真实性交给 process PERFECT/REVISE，不另造机械 terminal-todo completeness gate。  
Blessed 之后的再次 suicide 同样 drain latest process review。

**Dedicated 与 Finality**：

```text
首次进入 terminal Finality 时作为 ordinary cohort member enlist
之后完全遵循 ordinary graduate 规则
不发明「每轮强制回流 / 永不 graduate」特例

Finality cohort membership     → 可 graduate
Dedicated process-review duty  → 直到 LifeCompleted
```

process PERFECT ≠ terminal first PERFECT；enlist 后仍要 fresh FinalityRequestId / Barrier / tree / dual-PERFECT。  
Finality REVISE / Blessing 后 process-review session 不 Dispose；ordinary cohort 仍走既有 carryover/release。  
Dedicated LWR 形状与 retention 语义分别见 TODO-008 与本条。

## TODO-011：Legacy migration

```text
正常新 Life          → MagicTodo canonical 初始为空
                       绝不从 Host TodoTable 自动 adopt 上一 Life 旧 todo
仅升级瞬间 legacy open Life
                     → 允许一次 LegacyTodoSeedAdopted
```

必须在该 Life **首次 Magic provider request 之前**完成：从 Host old TodoTable 取 seed → 分配全新 Magic ids → append `LegacyTodoSeedAdopted` → 把带 ID 的 current list 注入 Manager provider-visible context。  
禁止 position/content 猜 identity；禁止等第一轮 todowrite 才 adopt（模型未见 id 无法 `kind:"existing"`）。  
同 session 后续新 Life 禁止再次从 Host TodoTable 反推 seed。  
已 completed Life 不回放 Magic Todo；升级后 `WorkActivated` 仅 inert（TODO-001）。

## TODO-012：恢复与禁止的程序计数器

恢复只从 durable facts 重建（`TodoWritePrepared` / `TodoWriteAccepted` live-or-recovery / physical ToolPart / `VerdictKnown` / `TodoReviewConcluded` 等），不靠内存 Stage、布尔组合、时间猜测或「下次还应发生」。

不得新增或恢复为控制状态：

```text
TodoPlanningStage / ReviewStage / AwaitingReview bool
TodoStage 程序计数器
第二套 PrefixEpoch / ActivePrefixEpoch SSOT
平行 LWR / process-evidence 投影
以 session.compacted 冒充 TodoCheckpoint
以 session head LWR 冒充 frontier/request-range bounded LWR
desired rebase 写成 Requested Stage / 未 seal 声称已 committed
provider 成功当作 epoch commit 条件
机械 Finality terminal-todo completeness gate
```

## TODO-013：Manager 表面与 guideline

正式投影：

```text
general pair-programming guideline（HOST-013）
+
if canonical role = Manager AND todowrite provider-visible
then MagicTodoManagerGuideline
```

`MagicTodoManagerGuideline` 是 **Manager-only** fragment：要求持续用 `todowrite` 保持 list 真实；规划与执行同一连续活动；todo 须经 `reviewing` 才能 `completed`；前序评审进行时继续独立下一阶段工作。  
**禁止**把 Magic Todo 文案并入全局 `ProjectionConstants.PairProgrammingGuidelineText`。

**隐藏 reviewer 表面（窄例外）**：Manager **可以**看到过程 review 的 outcome 与 canonical ProcessReviewLWR 报告正文（checkpoint tool result / suicide drain 回传）。  
Manager **不可**知道：隐藏 dedicated reviewer 身份、session/barrier/witness、2N 编排、assignment 内部细节。  
ProcessReviewLWR 复用既有 Finality safety-seal：canonical LWR **不** regex 清洗；无法证明 Manager-facing safety → fail closed；不得伪造「洗过的报告」。  
Glory/Prompt 表面交叉时，本条是 Manager 可见面的语义锚（含对 GLORY-030 / SURFACE-005 等的窄例外指向），不是 reviewer 编排向 Manager 泄漏的许可证。

## TODO-014：治理与证明指针

`TODO-*` 语义只在本文件定义一次。  
`shape/todo.md`：所有权、唯一 writer、边界。  
`how/todo.md`：before/after、merge、rebase、recovery 算法。  
`proof/todo.md`：canary、性质、静态门禁与反例。  
`why/todo.md`：理由与被拒方案。  

跨域（Host membrane、Review verdict、Context prefix、Glory Finality、Prompt/Projection 表面）**只交叉引用**本文件条款，不得复制或改写 `TODO-*` 合同。实现与 release 以 Active Change `changes/active/magic-todo.md` §47 门禁为准；变更冻结裁决须另开 Change。
