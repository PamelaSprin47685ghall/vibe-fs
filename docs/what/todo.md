# Magic Todo — 可观察行为

条款前缀：`TODO-`。  
本文件是稳定 `TODO-*` 语义的**唯一**所有者；跨域机制只引用条款，不复制合同。  
所有权 / 算法 / 证明见 `shape/todo.md` · `how/todo.md` · `proof/todo.md`。  
理由与被拒方案见 `why/todo.md`。

## TODO-001：生命周期 · BlindPlan · WorkRecordStart

Magic Todo 仅约束 **canonical role = Manager** 且 `todowrite` 对 provider 可见的 Life。

```text
HumanRoot → LifeOpened → BlindPlan Opening（Planning Table）
→ first accepted todowrite = T1 commitment → Living Mission
→ todowrite checkpoint → 工作 → …
→ suicide / Finality
```

Manager OpeningPolicy = BlindPlan（GLORY-074）。删除生产路径上的 planning-only → Activation 两阶段：`PlanningTail`、`ManagerWorkActivation`、`WorkActivated` 业务资格、Birth/Labor compression floor、Activation-only suicide gate、Planning→Working system prompt 切换。历史 Journal 中的 `WorkActivated` 可 decode，但是 **inert legacy fact**——新决策不得用它决定能否工作、压缩或 Finality。

持续节拍唯一来源：每一次成功受理的 `todowrite` = 一个 Todo Checkpoint（`TodoWriteAccepted`）。  
T1 = 本 Life **第一次** `TodoWriteAccepted`；关闭 Opening（TODO-015）。

**WorkRecordStart（Opening 结构性 floor）**：

```text
ManagerLife.WorkRecordStart
  = OpeningBoundary
  = Opening exclusive end
  = BlindPlan 下含 T1 commitment call + canonical accepted result
```

由 `LifeOpened` / XTrace Opening cursor **纯推导**，不是 Stage fact。Immediate 角色 Opening = InitialCharge；本条只约束 Manager BlindPlan。

```text
Manager Blogger effectiveStart = max(RecordCoverage, Life.WorkRecordStart)
```

Opening 永久 raw：永不被 Y 替换；不随 TodoCheckpoint rebase 消失；不经 process-review LWR 再复制（`includeOpening=false`）。  
删除的是 planning/labor stage floor，**不是** Opening protection。  
WorkRecord 三段标题见 COMPANION-003。

## TODO-002：obligations wire

Provider-visible `todowrite` 表达「mission 还欠什么」：

```text
todowrite(obligations: [{ name: string, work: string }])
```

| 字段 | 语义 |
|------|------|
| `name` | human-readable；同一 obligation 存续期间稳定 |
| `work` | 自然语言描述该义务仍欠什么 |

```text
CurrentObligations = last accepted obligations list
```

删除 provider 冷状态：`kind` / `id` / `status` / `priority` / `reviewing`、settled/proposed/`semanticMerge` 枚举机。无 alias。

Keep while owed；remove when earned by work。不得仅为让路看起来更短而删除仍欠义务；不得在已真正 discharge 后仅为「曾出现在计划中」而保留。

**Obligation 层级**：每一项必须描述「为了让用户请求真正满足，仍需成为真的工作 / 证据 / 条件」。禁止把形成这份账本身写成 obligation：`make a plan`、`analyze the request`、`write todos`、`decide next steps`、`先规划`、`先分析` 等 meta-work 不是 mission obligation。需要这些认知动作时直接完成它们；不要把它们占成 todo。

**T1 特例不是新状态机**：第一次 `todowrite` 是 Planning Table 已完成后的整份 obligation account 提交。`todowrite([{name="plan", work="make a plan"}])` 之类调用等于尚未完成 T1 前置认知，不应发出。Host 不用关键词分类器猜语义；该边界由 Planning Table、tool description、Manager Role Law 三个 provider decision boundary 同时表达，并由 golden/static proof 守住（TODO-015）。

`tool.definition` 是 provider-visible schema 的语义合同锚点；membrane 投影细节见 Host / `how/todo.md`。

## TODO-003：义务账与禁止状态机

```text
Obligation = { name, work }
```

禁止把进度伪装成 provider-visible status 枚举（`pending` / `in_progress` / `reviewing` / `completed` / `cancelled`）。真实性由 process review 与 Manager 下一轮 truthful account 判断，不另造机械枚举机。

同一 proposed list 内 duplicate `name` → 语法拒绝，可作为本次 tool 红字返回。  
禁止靠 `work` 文本猜 identity。Host 内部若需稳定 id，不得穿过 provider horizon。

## TODO-004：Admission、replay 与 V2 门禁

1. **同 message 多 todowrite**：同一 assistant message 出现 >1 个不同 `ToolCallId` 的 `todowrite` → **全部作为调用语法/协议错误拒绝**；这是允许出现 provider 红字的类别。无 ordinal winner、无 hook 到达顺序/wall-clock 仲裁。  
2. **单 inflight**：同一 Manager Life 同时最多一个新 checkpoint admission。  
3. **Same ToolCallId replay**：幂等为同一 `TodoWriteId` / 同一 obligation account；`TodoWritePrepared.ProviderInputDigest` 及 Life / BaseObligations / ordinal 合同必须与既有 Prepared 一致。若冲突，这是内部 identity corruption / replay contract 破坏，属于基础设施不变量失败：**fatal OpenCode**，不得降格成 tool 红字。不新增 checkpoint / review。  
4. **不同 ToolCallId**：即使 list 相同也是新 checkpoint。  
5. **Physical success 双路径**（live after-success / recovery completed ToolPart）必须收敛到同一 `TodoWriteId + input digest + output digest` 才可 `TodoWriteAccepted`。

`TodoWriteAccepted.PreparedFactRef` 必须是 append 对应 `TodoWritePrepared` 返回的真实 Journal `EventId`；不得重猜、伪造或用逻辑 id 代替。fold 必须拒绝不匹配的引用。

**失败分型（最终裁决）**：

```text
Syntax / call-shape failure
  → 只拒绝当前 todowrite
  → provider 红字允许

Semantic review failure = REVISE
  → 正常成功路径
  → 自然语言反馈 + WorkRecord
  → 绝不红字

Wanxiangshu / infrastructure invariant failure
  → Diagnostic.fatal
  → 打印诊断后直接杀死整个 OpenCode 进程
  → 绝不能作为 tool error 返回给 LLM
```

基础设施类包括但不限于：缺 AgentJournal / snapshot port / processReview runtime、snapshot/locality/materialization 失败、blob/journal I/O 或 digest 破坏、projection inconsistency、Prepared/Accepted identity corruption、hidden reviewer producer/assignment/runtime 异常。这样调试人员能立即看到真实故障，而 LLM 只会看到自己真正写错的调用语法或 reviewer 对工作语义的正常反馈。

**V2 fail-closed**：在 V2 runner 获得与 V1 等价的 tool definition / before / after hook contract 及 canary 证明之前，Magic-Todo Manager Attempt **不得**使用 V2 todowrite execution path。这里的「fail-closed」属于部署/启动不变量：若错误进入该路径，应 fatal OpenCode，而不是给 LLM 一次 todowrite 红字。

## TODO-005：Accepted supersession 与 CurrentObligations

`GrandRewrite` 的 clean break：**CurrentObligations = 本 Life 最后一次 `TodoWriteAccepted` 对应的完整 submitted account**。

```text
C0 = []（或一次性 legacy seed）
Tk = Accepted(Pk)
CurrentObligations(after Tk) = Pk
```

`TodoWritePrepared` 只冻结调用发生前的 `BaseObligations = C(k-1)` 与本次 `Submitted = Pk`；它本身不改变 Current。`TodoWriteAccepted` 一旦 durable，Pk 立即 supersede C(k-1)。这不是等待 reviewer 批准的 proposal，也不存在「accepted 但尚未成为 current」的半态。

Process review **不拥有 obligation state**：

```text
Review(k) = PERFECT | REVISE + canonical ProcessReviewLWR
PERFECT   → 证明本 checkpoint 的 account/work 没有需要纠正的实质问题
REVISE    → 告知 Manager 该 checkpoint 的 account/work 留有实质问题
两者都不回滚 Tk，不重写 CurrentObligations，不 semanticMerge
```

Manager 看到 REVISE 后，用后续 `todowrite` 写出新的完整 account；新的 Accepted 再自然 supersede 当前账。历史 checkpoint 仍是真实发生过的事实，不因后来评审被涂改。

禁止恢复旧 `settled/proposed/semanticMerge` 三态、status min-merge、reviewer 决定 Current 的写权或「preview 尚未生效」文案。`BaseObligations` 只用于 replay identity 与 reviewer 对照，不是待恢复的旧 current。

Provider result 遵循 GrandRewrite 的自然 surface：普通 accepted 成功只需 `# Keep working.`（或等价空成功）；上一 ConsumableReview=PERFECT 静默；上一 ConsumableReview=REVISE 才返回 `# An earlier account of the work left something unresolved.` + canonical ProcessReviewLWR。不得回显 `settled/proposed/preview/status` DTO，也不得声称 reviewer 批准后 account 才生效。T1 额外包裹 TODO-015 revelation。

## TODO-006：评审节拍与 ConsumableReview

严格 **1:1 lag-1**：

```text
Tk = 第 k 个 TodoWriteAccepted
Rk = Accepted(Tk) 派生的第 k 次 process-review obligation

TodoWrite k  synchronizes Review(k-1)
             creates      Review(k)
```

Rk **不**阻塞 Tk 返回；Manager 可立即做后续独立工作。  
T(k+1) 到来时若 Rk 尚未形成 ConsumableReview → **必须作为合法因果等待**直至 `TodoReviewConcluded(k)` durable；不得把这一等待渲染成 provider 红字。Rk 成为 ConsumableReview 后，T(k+1) 才继续 admission。若等待链暴露出 reviewer/runtime/snapshot/Journal/locality 等基础设施异常，则这不是 tool failure：Host 必须 `Diagnostic.fatal` 记录后直接杀死整个 OpenCode 进程，交给调试人员处理；绝不得把「万象术自身失败」伪装成一次 todowrite 红字继续跑。

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

Process review REVISE 交付：`# An earlier account of the work left something unresolved.` + Reviewer WorkRecord。  
Process review PERFECT：静默（不改变下一步行动）。

## TODO-007：Canonical 投影 vs Host sink

```text
MagicTodoProjection / Journal facts  = canonical semantic truth
Host TodoTable                       = compatibility sink only
```

禁止用 Host TodoTable 恢复或反推 canonical obligations truth。  
provider `name`/`work` 在 before 投影到 Host 前按 membrane 合同映射；canonical account 不得被 sink 策略改写回 status 枚举。

Compatibility sink 在每次 accepted 前可由 before 乐观投影本次 submitted account；Accepted 后 canonical 与 sink 指向同一个 Pk。Process review verdict 不回滚 canonical，因此也不得因 REVISE 把 sink 回刷到旧 account。

若 Host sink 因 crash / replay 与 canonical `CurrentObligations` 漂移，只允许做**纯投影修复**：不产生 checkpoint、不触发 process review、不改 canonical truth。
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
PERFECT 与 REVISE 在 verdict 前都必须产生本 request 的 canonical ProcessReviewLWR。无 prose 的 verdict 不得形成 Concluded；若 hidden reviewer 已结束而仍无法物化合法 prose/LWR，这是 reviewer infrastructure/protocol 不变量失败 → `Diagnostic.fatal` kill OpenCode，不得变成 Manager 的 todowrite 红字。

三段标题固定 `Opening / Chronicle / Recent work`（COMPANION-003）。正式陈述 = Recent work 最后一条助手文本（散文 claim；无 Closing report 段）。

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

T1 本身即该 Life 的第一次 accepted commitment，计入协议入口。

有 checkpoint 时：await latest ConsumableReview ≡ TodoReviewConcluded。REVISE → 返回 canonical report、**不**建 FinalityRequest、Life 继续；CurrentObligations 仍是 latest Accepted account，由 Manager 后续 checkpoint 修正。PERFECT → 进入既有 Finality 前置。  
「至少一次 TodoWriteAccepted」≠ 机械要求 obligations 清空。未完成项真实性交给 process PERFECT/REVISE，不另造机械 terminal-todo completeness gate。  
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
正常新 Life          → MagicTodo CurrentObligations 初始为空
                       绝不从 Host TodoTable 自动 adopt 上一 Life 旧 todo
仅升级瞬间 legacy open Life
                     → 允许一次 LegacyTodoSeedAdopted
```

必须在该 Life **首次 Magic provider request 之前**完成：从 Host old TodoTable 取 seed → 投影为 obligations `{name,work}` → append `LegacyTodoSeedAdopted` → 把 current account 注入 Manager provider-visible context。  
禁止 position/content 猜 identity；禁止等第一轮 todowrite 才 adopt。  
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
Activation / WorkActivated 资格门 / system prompt 切换
kind/id/status/priority/reviewing provider 冷状态
```

## TODO-013：Manager 表面与 guideline

正式投影：

```text
general pair-programming guideline（HOST-013）
+
if canonical role = Manager AND todowrite provider-visible
then MagicTodoManagerGuideline
```

`MagicTodoManagerGuideline` 是 **Manager-only** fragment，但不得抹平 BlindPlan 的关系边界：Pre-T1 只要求把 obligation account 做完整，**不得**用 todowrite 写「我要规划」这种 meta-account；Post-T1 才要求在执行中持续保持 living obligations 真实。前序评审进行时可继续独立工作。Pre-T1 / T1 / Post-T1 文案分属 TODO-015；这三者是 conversation relation，不是 system identity / persisted phase。  
**禁止**把 Magic Todo 文案并入全局 `host/pair-programming-guideline`。

**隐藏 reviewer 表面（窄例外）**：Manager **可以**看到过程 review 的 outcome 与 canonical ProcessReviewLWR 报告正文（checkpoint tool result / suicide drain 回传）。  
Manager **不可**知道：隐藏 dedicated reviewer 身份、session/barrier/witness、2N 编排、assignment 内部细节。  
ProcessReviewLWR 复用既有 Finality safety-seal：canonical LWR **不** regex 清洗；无法证明 Manager-facing safety 说明内部 projection/safety 不变量无法成立 → `Diagnostic.fatal` kill OpenCode；不得伪造「洗过的报告」，也不得作为 todowrite 红字甩给 Manager。  
Glory/Prompt 表面交叉时，本条是 Manager 可见面的语义锚（含对 GLORY-030 / SURFACE-005 等的窄例外指向），不是 reviewer 编排向 Manager 泄漏的许可证。

## TODO-014：治理与证明指针

`TODO-*` 语义只在本文件定义一次。  
`shape/todo.md`：所有权、唯一 writer、边界。  
`how/todo.md`：before/after、settle、rebase、recovery 算法。  
`proof/todo.md`：canary、性质、静态门禁与反例。  
`why/todo.md`：理由与被拒方案。  

跨域（Host membrane、Review verdict、Context prefix、Glory Finality、Prompt/Projection 表面）**只交叉引用**本文件条款，不得复制或改写 `TODO-*` 合同。实现与 release 以 Active Change `changes/active/GrandRewrite.md` 门禁为准；变更冻结裁决须另开 Change。

## TODO-015：BlindPlan T1 commitment

本 Life 第一次 accepted `todowrite` = T1 commitment（CommitmentContract，GLORY-074）。

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

T1 call + canonical accepted result 属 constitutive OpeningMaterial（COMPANION-014）。  
交托只经 conversation tool result；**禁止** system prompt / Persona / Role Law 切换（GLORY-075；PROMPT-014）。

冻结文案分属唯一 owner（SURFACE-004）：

| 段 | 用途 |
|----|------|
| Planning Table | Pre-T1：替另一 Manager 写诚实义务账；可调查；不得开始扛路 |
| T1 revelation | accepted 后揭示「The Manager who will carry it is you.」 |
| Pre-T1 fork/join returns | 调查回报仍在 Planning Table；勿开始执行 |
| Living Mission | Post-T1：持续保持 living obligations 真实 |
| Pre-T1 / Post-T1 idle | 分别鼓励写完账户 / 继续有用工作或求终 |

每个新 Life（含 Reawakening）重新进入 BlindPlan Opening。  
generic `lifecycle.activation` 若作语义资产 key，只指「新 owner 获 responsibility」，不得承载 Manager phase 或触发 prompt 替换。
