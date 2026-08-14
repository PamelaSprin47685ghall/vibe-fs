# obligation-ledger — WHAT

条款前缀：`OBLIGATION-LEDGER-`。
本文件是 obligation-ledger 包的**唯一 normative 合同**。跨域机制（finality、review-assurance、
work-record、prefix-stability、participant-horizon、effect-accounting）只引用本包命题，不复制合同。
历史与弃权、实现模型见 `HOW.md`；每条命题的证明落点见 `PROOF.md`。

词汇：`Tk` = 第 k 个 `TodoWriteAccepted`；`Pk` = Tk 提交的完整 obligation account；
`C(k-1)` = Tk 之前的 `CurrentObligations`；`Rk` = Tk 派生的过程评审义务；`T1` = 本 Life 第一次
`TodoWriteAccepted`（BlindPlan commitment）。

---

## OBLIGATION-LEDGER-001：当前义务 = mission debt，不是 workflow stage

**规范**：`CurrentObligations` 只描述「为了让用户请求真正满足，仍需成为真的工作 / 证据 / 条件」。
它不描述「我现在处于哪个工作阶段」，不携带 `kind` / `id` / `status` / `priority` / `reviewing`
之类的 provider-visible 冷状态，也没有 alias。

**含义 / 动机**：Manager 生命周期唯一诚实的进度表示是「还欠什么」，不是「走到哪一步」。
阶段机把计划、等待、评审冒充成用户债务（TODO-002/003；GrandRewrite 删除 provider 冷状态）。

**边界**：阶段语义（无持久程序计数器）由 `structured-workflow` 拥有；本包只拥有「账本不伪装进度」。
Provider surface 的 schema 形态是当前实现（HOW），不是永久合同。

**证据** → PROOF.md 行 O-1。

## OBLIGATION-LEDGER-002：obligations wire 与 CurrentObligations 定义

**规范**：

```text
todowrite(obligations: [{ name: string, work: string }])
CurrentObligations = last accepted obligations list
```

`name` 在同一 obligation 存续期间稳定；`work` 是自然语言描述该义务仍欠什么。
Keep while owed；remove when earned by work。不得仅为让路看起来更短而删仍欠义务；
不得在真正 discharge 后仅为「曾出现在计划中」而保留。

**含义 / 动机**：wire 唯一语言是 `{name, work}` account；`CurrentObligations` 由 Journal fold 从
Accepted 链纯推导，任何其它 writer 不得改动（TODO-002/005）。

**边界**：`name`/`work` 字段名与 schema 具体形态是当前 surface（HOW）；「accepted account 即当前」的
supersession 语义见 OBLIGATION-LEDGER-010。

**证据** → PROOF.md 行 O-2。

## OBLIGATION-LEDGER-003：禁止用 status 枚举伪装进度

**规范**：禁止把进度伪装成 provider-visible status 枚举（`pending` / `in_progress` / `reviewing` /
`completed` / `cancelled`）。真实性由 process review 与 Manager 下一轮 truthful account 判断，
不另造机械枚举机（TODO-003）。

**含义 / 动机**：`status` 机器描述阶段，不是债务；一旦存在，模型会被诱导填写「现在在做什么」
而不是「还欠什么」。

**边界**：Host TodoTable 的 status 字段是 compatibility sink 投影（HOW，OBLIGATION-LEDGER-015），
不得反推 canonical。

**证据** → PROOF.md 行 O-3。

## OBLIGATION-LEDGER-004：meta-work 不是 mission obligation

**规范**：`make a plan`、`analyze the request`、`write todos`、`decide next steps`、`先规划`、
`先分析` 等 meta-work 不是 mission obligation。需要这些认知动作时直接完成它们，不占成 todo。
对候选项同时做 **completion counterfactual**：若该项被完美完成而用户要求的世界状态/交付物仍未改变，
只是 Manager 更理解、有清单、有计划或知道下一步，则它只是规划认知（TODO-002）。

**含义 / 动机**：若把 Planning Table 本身写进账本，模型会把「规划」冒充成用户债务，
并过早触发 T1 揭幕（TODO-015）。

**边界**：该判定是**语义形状**，Host 不得按关键词分类或拒绝（见 OBLIGATION-LEDGER-005）。
T1 特例的 commitment framing 见 OBLIGATION-LEDGER-016。

**证据** → PROOF.md 行 O-4。

## OBLIGATION-LEDGER-005：可托付完整性（handoff-completeness）

**规范**：每项 obligation 必须单独写清楚「欠的结果是什么」以及「什么证据足以闭环」，达到另一位
称职 Manager 无需替提交者发明缺失内容即可执行和验收的程度。`placeholder: planning`、`TBD`、
裸阶段名、仅占槽位的 label、把内容推迟到未来的 name/work 都不是较小的 obligation，
而是尚未完成的 Planning Table 认知。

**含义 / 动机**：可托付完整性是 meta-work 边界（OBLIGATION-LEDGER-004）的独立 gate：
无法由另一 Manager 直接判断 outcome + closure evidence 的条目不是 debt（TODO-002）。

**边界**：该判定是语义形状，Host 不得用自然语言关键词分类器拒绝这些字符串——
分类器会把脆弱启发式重新变成隐藏状态机。`MagicTodoHostCodec` 不得出现
`placeholder: planning` / `TBD` 之类的分类样本（见 PROOF O-5 的静态断言）。

**证据** → PROOF.md 行 O-5。

## OBLIGATION-LEDGER-006：obligation identity / 连续性

**规范**：同一 proposed list 内 duplicate `name` → 语法拒绝（可作为本次 tool 红字返回）。
blank name 同样语法拒绝。禁止靠 `work` 文本猜 identity；Host 内部若需稳定 id，
不得穿过 provider horizon（TODO-003）。

**含义 / 动机**：identity 是账本连续性的基础；文本猜 identity 会把自然语言启发式变成隐藏状态机。

**边界**：语法拒绝属于「允许 provider 红字」的类别（OBLIGATION-LEDGER-009 分型）。

**证据** → PROOF.md 行 O-6。

## OBLIGATION-LEDGER-007：admission —— 同 message 多 todowrite 全拒、单 inflight

**规范**：

1. 同一 assistant message 出现 >1 个不同 `ToolCallId` 的 `todowrite` → **全部**作为调用语法/协议
   错误拒绝。无 ordinal winner、无 hook 到达顺序 / wall-clock 仲裁。
2. 同一 Manager Life 同时最多一个新 checkpoint admission。
3. 不同 `ToolCallId`（即使 list 相同）→ 新 checkpoint。

**含义 / 动机**：winner 仲裁制造不必要的排序面，且与 lag-1 单链冲突（TODO-004）。
Admission 是「一次一账」的协议纪律。

**边界**：admission 的语法/协议拒绝是 provider 红字类别；infra 不变量失败走
OBLIGATION-LEDGER-009 的 fatal 分支。

**证据** → PROOF.md 行 O-7。

## OBLIGATION-LEDGER-008：Same ToolCallId replay 幂等

**规范**：Same ToolCallId replay 幂等为同一 `TodoWriteId` / 同一 obligation account；
`TodoWritePrepared.ProviderInputDigest` 及 Life / BaseObligations / ordinal 合同必须与既有 Prepared
一致。若冲突 → 内部 identity corruption / replay contract 破坏，属于基础设施不变量失败：
**fatal OpenCode**，不得降格成 tool 红字。不新增 checkpoint / review（TODO-004）。

**含义 / 动机**：恢复与重放必须可证明是同一账；digest 冲突说明历史被撕裂，
继续执行只会制造半坏状态。

**边界**：`TodoWriteAccepted.PreparedFactRef` 必须是 append 对应 `TodoWritePrepared` 返回的真实
Journal `EventId`；fold 拒绝不匹配的引用（见 PROOF O-8）。

**证据** → PROOF.md 行 O-8。

## OBLIGATION-LEDGER-009：失败分型（三态）

**规范**：

```text
Syntax / call-shape failure     → 只拒绝当前 todowrite；provider 红字允许
Semantic review failure = REVISE → 正常成功路径；自然语言反馈 + WorkRecord；绝不红字
Wanxiangshu / infrastructure     → Diagnostic.fatal；打印诊断后杀死整个 OpenCode 进程；
                                  绝不能作为 tool error 返回给 LLM
```

基础设施类包括但不限于：缺 AgentJournal / snapshot port / processReview runtime、
snapshot/locality/materialization 失败、blob/journal I/O 或 digest 破坏、projection inconsistency、
Prepared/Accepted identity corruption、hidden reviewer producer/assignment/runtime 异常（TODO-004）。

**V2 fail-closed**：在 V2 runner 获得与 V1 等价的 tool definition / before / after hook contract
及 canary 证明之前，Magic-Todo Manager Attempt **不得**使用 V2 todowrite execution path；
若错误进入该路径 → fatal OpenCode，而不是给 LLM 一次 todowrite 红字（HOST-024）。

**含义 / 动机**：红字只属于模型写错的调用语法；REVISE 是语义反馈；系统自身故障必须 fail-fast
让调试人员立即看到真实故障（TODO-004/why「失败分型」裁决）。

**边界**：infra fatal 的进程级 kill 机制由 `crash-reconciliation` 拥有；本包只拥有分型判定。

**证据** → PROOF.md 行 O-9。

## OBLIGATION-LEDGER-010：Accepted 立即 supersede CurrentObligations

**规范**：

```text
C0 = []（或一次性 legacy seed，OBLIGATION-LEDGER-019）
Tk = Accepted(Pk)
CurrentObligations(after Tk) = Pk
```

`TodoWritePrepared` 只冻结调用发生前的 `BaseObligations = C(k-1)` 与本次 `Submitted = Pk`；
它本身不改变 Current。`TodoWriteAccepted` 一旦 durable，Pk 立即 supersede C(k-1)。
这不是等待 reviewer 批准的 proposal；不存在「accepted 但尚未成为 current」的半态（TODO-005）。

**含义 / 动机**：崩溃恢复只需重放 Accepted 链；reviewer settlement 与 merge 策略被彻底删除。

**边界**：`BaseObligations` 只用于 replay identity 与 reviewer 对照，不是待恢复的旧 current。

**证据** → PROOF.md 行 O-10。

## OBLIGATION-LEDGER-011：REVISE 不拥有 obligation state

**规范**：

```text
Review(k) = PERFECT | REVISE + canonical ProcessReviewLWR
两者都不回滚 Tk，不重写 CurrentObligations，不 semanticMerge
```

Manager 看到 REVISE 后，用后续 `todowrite` 写出新的完整 account；新的 Accepted 再自然 supersede
当前账。历史 checkpoint 仍是真实发生过的事实，不因后来评审被涂改。禁止恢复旧
`settled/proposed/semanticMerge` 三态、status min-merge、reviewer 决定 Current 的写权（TODO-005）。

**含义 / 动机**：reviewer 只判断并报告；REVISE 迫使 Manager 在后续 checkpoint 写出更真实的新
account，而不是由系统回滚（why「CurrentObligations：Accepted supersession」裁决）。

**边界**：PERFECT/REVISE 的**判断语义**归 `review-judgement`；「何时可消费」归 `review-assurance`。
本包只拥有「账本不被评审涂改」。

**证据** → PROOF.md 行 O-11。

## OBLIGATION-LEDGER-012：checkpoint + review obligation 的 SSOT = TodoWriteAccepted

**规范**：`TodoWriteAccepted` 是 checkpoint 与过程评审义务的唯一事实源。
`TodoWritePrepared` 单独不派生 Rk；Host store 已写 ≠ Accepted（TODO-004/006）。
Accepted ↔ obligation 一一对应；被拒/非 admission 不建 review。

**含义 / 动机**：Rk 的派生必须锚定在 durable Accepted 上；Prepared 只是 admission 中间态，
不能成为评审义务的源头。

**边界**：评审义务的**执行**（ensureReview、assignment、ConsumableReview 物化）属 review-assurance
与 `work-record`；本包拥有「Accepted 派生 Rk」与「1:1 对应」。

**证据** → PROOF.md 行 O-12。

## OBLIGATION-LEDGER-013：1:1 lag-1 过程评审节拍

**规范**：

```text
Tk = 第 k 个 TodoWriteAccepted
Rk = Accepted(Tk) 派生的第 k 次 process-review obligation
TodoWrite k  synchronizes Review(k-1)
             creates      Review(k)
```

Rk **不**阻塞 Tk 返回；Manager 可立即做后续独立工作。T(k+1) 到来时若 Rk 尚未形成
ConsumableReview → **必须作为合法因果等待**直至 `TodoReviewConcluded(k)` durable；
不得把这一等待渲染成 provider 红字（TODO-006）。

**含义 / 动机**：lag-1 单链保证结算链有单一公式；Manager 在评审期间可并行独立工作
（GLORY-028 交叉）。

**边界**：等待期间暴露 reviewer/runtime/snapshot/Journal/locality 基础设施异常 → 不是 tool
failure，Host 必须 `Diagnostic.fatal`（OBLIGATION-LEDGER-009）。

**证据** → PROOF.md 行 O-13。

## OBLIGATION-LEDGER-014：可消费结论 = ConsumableReview；VerdictKnown 不足

**规范**：

```text
VerdictKnown(k)       = Reviewer 域已有 durable process verdict
                        → 立即决定业务 outcome（PERFECT | REVISE）
                        → 不单独构成可消费结论；不携带 WorkRecordRef
                        → 不进入 Finality dual-PERFECT witness 代数
ConsumableReview(k) ≡ TodoReviewConcluded(k)
                      = VerdictKnown(k)
                        AND 该 verdict frontier 的 canonical ProcessReviewLWR 已 record-ready
                        AND 同 snapshot 物化 WorkRecordRef / Digest
                        → 才允许下一 TodoWrite / suicide drain 消费上一报告
```

禁止用同一个 `TodoReviewConcluded` 表达「只有 verdict、尚无 report」的中间态；禁止另造
`AwaitingReview` bool / `ReviewStage`（TODO-006/012）。

**含义 / 动机**：下一 checkpoint 不能消费空报告或竞态半态；「判断已定」与「报告可展示」必须分型
（REVIEW-014）。

**边界**：record-ready 的物化机制与同 snapshot 等待由 `review-assurance` 拥有；本包拥有
「账本消费的 gate：ConsumableReview 才可被 T(k+1)/drain 消费」。

**证据** → PROOF.md 行 O-14。

## OBLIGATION-LEDGER-015：canonical 单真相源 vs Host compatibility sink

**规范**：

```text
MagicTodoProjection / Journal facts  = canonical semantic truth
Host TodoTable                       = compatibility sink only
```

禁止用 Host TodoTable 恢复或反推 canonical obligations truth。canonical account 不得被 sink 策略
改写回 status 枚举。Compatibility sink 在每次 accepted 前可由 before 乐观投影本次 submitted
account；Accepted 后 canonical 与 sink 指向同一个 Pk。Process review verdict 不回滚 canonical，
因此也不得因 REVISE 把 sink 回刷到旧 account。若 Host sink 因 crash / replay 与 canonical
`CurrentObligations` 漂移，只允许**纯投影修复**：不产生 checkpoint、不触发 process review、
不改 canonical truth（TODO-007）。

**含义 / 动机**：Host 表是兼容 UI sink，不能决定 account identity、review cadence 或 recovery
（why「真相源」裁决）。REVISE 不是 sink rollback 触发器。

**边界**：sink 的具体字段投影（`content=name: work / status=in_progress / priority=medium`、
reviewing 降级 in_progress 等）是**兼容性实现**，属 HOW「历史与弃权」——compatibility 不写成
永久需求；未来 sink 替换不改变 canonical 语义。

**证据** → PROOF.md 行 O-15。

## OBLIGATION-LEDGER-016：T1 commitment 与 Opening 关闭

**规范**：本 Life 第一次 accepted `todowrite` = T1 commitment（CommitmentContract）。

```text
todowrite(T1)
  → validate
  → durably TodoWriteAccepted(T1)
  → derive: first accepted todo in this Life
  → render canonical T1 result containing entrustment revelation
  → persist exact provider-visible result
  → return
  → Opening closes；WorkRecordStart = OpeningBoundary
```

T1 call + canonical accepted result 属 constitutive OpeningMaterial。交托只经 conversation tool
result；**禁止** system prompt / Persona / Role Law 切换。每个新 Life（含 Reawakening）重新进入
BlindPlan Opening（TODO-015/GLORY-074）。

**含义 / 动机**：T1 是对初始判断的一次不可逆承诺——「没有第二次第一次提交」。后续 living account
仍可因现实变化、新证据或纠偏而更新，但不得用这种可更新性为 T1 的遗漏、placeholder 或延后决定
开后门（TODO-002 的 T1 特例；规范性 commitment pressure，不是 Host 禁止后续更新）。

**边界**：OpeningMaterial / WorkRecordStart 的 LWR 表示与压缩 floor 属 `work-record`；system
prompt 字节稳定属 `participant-identity` + `prefix-stability`；T1 文案具体 wording 是 HOW。

**证据** → PROOF.md 行 O-16。

## OBLIGATION-LEDGER-017：Manager BlindPlan Opening（无生产 Activation）

**规范**：Manager OpeningPolicy = BlindPlan。Pre-T1 = Planning Table：替将要扛路的另一 Manager 写
诚实义务账，可调查，**不得**开始执行所规划之路。T1 accepted 后才进入 Living Mission。
删除生产路径上的 planning-only → Activation 两阶段：`PlanningTail`、`ManagerWorkActivation`、
`WorkActivated` 业务资格、Birth/Labor compression floor、Activation-only suicide gate、
Planning→Working system prompt 切换均非生产合同（TODO-001/GLORY-074）。

**含义 / 动机**：两阶段阶段机 + 新 Todo 阶段会横向爆炸；Planning→Working 换 prompt 破坏 prefix
cache 并泄露「你已携带任务」（why「生命周期」裁决）。历史 Journal 中的 `WorkActivated` 可 decode，
但是 **inert legacy fact**——新决策不得用它决定能否工作、压缩或 Finality。

**边界**：`WorkRecordStart` 由 `LifeOpened` / XTrace Opening cursor **纯推导**，不是 Stage fact；
Opening 永久 raw 的保护语义属 `work-record`。

**证据** → PROOF.md 行 O-17。

## OBLIGATION-LEDGER-018：恢复只从 durable facts；禁止程序计数器与平行证据

**规范**：恢复只从 durable facts 重建（`TodoWritePrepared` / `TodoWriteAccepted` live-or-recovery /
physical ToolPart / `VerdictKnown` / `TodoReviewConcluded` 等），不靠内存 Stage、布尔组合、
时间猜测或「下次还应发生」。不得新增或恢复为控制状态：

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
Activation / WorkActivated 资格门 / system prompt 切换
kind/id/status/priority/reviewing provider 冷状态
```

（TODO-012；后三项分别归 finality / participant-identity / 本包 001）

**含义 / 动机**：事实 + CE 是唯一合法恢复路径；程序计数器是「君子不立危墙」的反面——
崩溃后无法重建。

**边界**：无持久程序计数器的一般法则属 `structured-workflow`；durable substrate 属
`durable-events`；平行 LWR 禁令的 LWR 侧属 `work-record`。

**证据** → PROOF.md 行 O-18。

## OBLIGATION-LEDGER-019：新 Life 账本为空；仅升级瞬间一次 legacy seed

**规范**：

```text
正常新 Life          → MagicTodo CurrentObligations 初始为空
                       绝不从 Host TodoTable 自动 adopt 上一 Life 旧 todo
仅升级瞬间 legacy open Life
                     → 允许一次 LegacyTodoSeedAdopted
```

seed 必须在该 Life **首次 Magic provider request 之前**完成：从 Host old TodoTable 取 seed →
投影为 obligations `{name,work}` → append `LegacyTodoSeedAdopted` → 把 current account 注入
Manager provider-visible context。禁止 position/content 猜 identity；禁止等第一轮 todowrite 才
adopt；同 session 后续新 Life 禁止再次从 Host TodoTable 反推 seed（TODO-011）。

**含义 / 动机**：session 级 TodoTable 会污染同 session 新 Life；模型未见 id 无法 `existing`，
故不能等首轮 todowrite 再分配（why「Legacy」裁决）。

**边界**：已 completed Life 不回放 Magic Todo；升级后 `WorkActivated` 仅 inert（OBLIGATION-LEDGER-017）。

**证据** → PROOF.md 行 O-19。

## OBLIGATION-LEDGER-020：Dedicated 过程 Reviewer 每 Life 一个 logical

**规范**：每个 Manager Life 复用**一个** logical `DedicatedTodoReviewer`（REVIEW-015/TODO-008）：

- 首次 `TodoWriteAccepted` 时若尚不存在 → Host-owned hidden session 创建并 durable enlist；
- 后续 checkpoint：同一 logical reviewer，优先同一 physical session；
- 仅 proven permanent loss 后 `DedicatedTodoReviewerReplaced`（logical id 不变）；不确定 → fail closed；
- **Finality cohort membership 可 graduate，process-review duty / session 至少保留到
  `LifeCompleted`**——Blessing 或 Finality REVISE 不 Dispose、不丢过程历史（TODO-008/010）。

**含义 / 动机**：过程历史必须连续；「偶发超时就换人」会丢上下文。process duty 与 Finality
graduate 拆开是独立生命周期。

**边界**：dedicated session 的创建/退休/graduate 的 session 生命周期归 `managed-session-lifecycle`；
Manager 不可见 dedicated session/barrier/witness 的 admission 归 `participant-horizon`。

**证据** → PROOF.md 行 O-20。

## OBLIGATION-LEDGER-021：desired lag-1 cutoff 仅由 Accepted 链推导

**规范**：Accepted checkpoints 只使 **desired** lag-1 cutoff 可推导；**不**在 todowrite after 提交
PrefixEpoch。

```text
desiredCutoff(Tk) = Before(T(k-1) tool-call)   // T1 无 prior
```

下一 provider attempt seal/绑定前原子 `PrefixRebaseCommitted`（`EvidenceKind=TodoCheckpoint`），
进入既有 `ActivePrefixEpoch` SSOT；provider 成败不回滚已 seal epoch（TODO-009）。

**含义 / 动机**：desired ≠ committed。Accepted 链是 cutoff 推导的唯一事实源；seal 时点属于
下一 provider attempt 的 transform 边界，不是 todowrite 的 after。

**边界**：PrefixEpoch / `ActivePrefixEpoch` SSOT、`PrefixCoverage` 与 rebase 机制属
`prefix-stability`；本包只拥有「desired cutoff 的事实源 = Accepted 链」。

**证据** → PROOF.md 行 O-21。

## OBLIGATION-LEDGER-022：评审义务的产生 / 消费账本侧规则（含 tail drain 义务）

**规范**：每个 Accepted 派生的 Rk 是**悬挂义务**，必须被某次 `T(k+1)` 或 suicide drain 消费；
被消费前不得有新的 checkpoint 越过它。`suicide` 是尚未被下一 todowrite 消费的 process review 的
**唯一** tail drain（禁止再调一次 todowrite flush——会创造 R(k+1)，TODO-010）。drain 的**执行**
（零 checkpoint fail closed、REVISE 回灌、PERFECT 进 Finality 前置）由 `finality` 拥有；
本包只拥有「Rk 产生后必须被消费、不得被绕过」的账本侧义务。

**含义 / 动机**：评审义务是账的一部分；Manager 无法通过「不再 todowrite」让悬挂的 Rk 消失。

**边界**：与 finality 的交界：OBLIGATION-LEDGER-022 定义义务，FINALITY-* 定义终结如何消费它。

**证据** → PROOF.md 行 O-22。

## OBLIGATION-LEDGER-023：MagicTodoManagerGuideline 的 Manager-only 语义

**规范**：`MagicTodoManagerGuideline` 是 **Manager-only** fragment，与全局 pair-programming
guideline（HOST-013）分离；不得并入全局文案。其冻结语义覆盖：obligations 增删（keep while owed /
remove when earned）、checkpoint 连续性（lag-1）、T1 首收（第一次 todowrite = 完整道路账）、
不伪造 Activation（Pre-T1/T1/Post-T1 是 conversation relation，不是 persisted phase）
（TODO-013/PROMPT-013）。

**含义 / 动机**：Manager 需要持续诚实的账本纪律指引；把它写进全局 pair 文案会污染非 Manager /
Blogger 合同（why「Manager 表面」裁决）。

**边界**：隐藏 reviewer 的**可见性 admission**（哪些可见、哪些禁止）归 `participant-horizon`；
本包只拥有 guideline 的账本语义内容。

**证据** → PROOF.md 行 O-23。

## OBLIGATION-LEDGER-024：tool.definition 唯一广告点；V2 门禁

**规范**：`tool.definition` 是 provider-visible V2 schema 的**唯一** Host 侧广告点，必须同时更新
`parameters` / `jsonSchema` / `description`；只改一处导致组装不一致 → fail closed（HOST-018）。
description 覆盖 Manager 可见纪律（与 002/003/004/006/013 一致），**禁止**泄露隐藏编排
（dedicated reviewer、hidden agent/session、Finality cohort、barrier、witness、2N，TODO-013）。
definition 改广告 schema 不自动替换原 executor decode schema；before 额外挂载 V1 compatibility view，
该 view 不得改写 provider-visible enumerable input（HOST-018/020）。

**含义 / 动机**：schema 三处同步是「唯一广告点」的机械保证；描述泄漏隐藏机制会把质量门重新变成
Manager checklist（GLORY-002 交叉）。

**边界**：description 的逐字文案与本地化属 `provider-language`；工具描述的具体措辞是 HOW。

**证据** → PROOF.md 行 O-24。

## OBLIGATION-LEDGER-025：before 合同（materialization 与 Prepared 冻结）

**规范**：`tool.execute.before` 同步阶段只做 provider args decode + 纯内存 compatibility 投影，
并启动 per-call **deferred prepare** 后立即返回；**不**等待 snapshot/Journal IO、**不**启动
reviewer、**不**写 `TodoWriteAccepted`（HOST-019/020）。

```text
before live args → decode obligations → compatibility projection → executor
                  ↘ deferred prepare:
                     pending + {} → wait + reread same callID
                     materialized canonical == captured live canonical → durable prepare/admit
                     materialized canonical != captured live canonical → fail closed
                     carrier/provider run/part 变化 → fail closed
```

`TodoWritePrepared` 冻结 canonical `{obligations:[{name,work}]}` `ProviderInputDigest` 与
BaseObligations / Submitted digests、`ReviewFrontier`（本 tool-call 前 exclusive cursor，绑
ManagerLifeId；pending before-hook 计入 next-assigned + 同 message 本 call 之前的可捕获 part 数）。
`TodoWriteAccepted.PreparedFactRef` 必须是 append 对应 Prepared 返回的真实 Journal `EventId`，
不得重猜、伪造或用逻辑 id 代替（TODO-004）。

**含义 / 动机**：OpenCode 会先创建 `state=pending,input={}` 的 ToolPart；pre-before snapshot 不保证
已 materialize 最终 input。拒绝把 `{}` 降级当输入；拒绝用 after「改回」历史 input 补救。

**边界**：ReviewFrontier 的 XTrace 表示依赖 `semantic-trace`；snapshot 唯一定位（sessionID+callID
→ ToolPart/assistant/run/ordinal/XTrace range）是 host-boundary canary（HOST-025），本包引用。

**证据** → PROOF.md 行 O-25。

## OBLIGATION-LEDGER-026：after 合同（Accepted → ensureReview → 富化 result）

**规范**：after 仅在原 executor **物理成功返回**的 live path 进入（failure 路径不保证 after；
协议不依赖 after-on-throw）。顺序合同（HOST-021）：

```text
1. 取 bridge 或从 Prepared + physical evidence 重建
2. ensure TodoWriteAccepted（幂等；live 或 recovery 双路径收敛同一 TodoWriteId + input digest + output digest）
3. ensure DedicatedTodoReviewer / ensureReview（Rk 义务；after 不必「已跑 reviewer」才算成功）
4. desired lag-1 cutoff 可从 Accepted 链推导（提交 PrefixEpoch 不在 after，OBLIGATION-LEDGER-021）
5. 富化模型可见 tool result：上一 ConsumableReview 的 ProcessReviewLWR（REVISE 是反馈不是 rollback；
   T1 时含 entrustment revelation）
6. cleanup bridge
7. return
```

禁止：先启动 reviewer 再 Accepted（幽灵 review）；把 Host TodoTable 已变成 Pk 误当 Accepted；
`Prepared + failed/absent/digest mismatch` 后仍 Accepted（TODO-004/005）。

**含义 / 动机**：Accepted 必须由物理成功 + Prepared 双证收敛；review 派生必须锚定 Accepted 之后。

**边界**：physical success 的 Requested/Accepted 双路径分型属 `effect-accounting`；富化 result 的
安全 seal（Manager-facing LWR 不 regex 清洗）属 finality safety-seal 交叉（TODO-013）。

**证据** → PROOF.md 行 O-26。
