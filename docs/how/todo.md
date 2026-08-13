# Todo — 目标实现

## Implements

行为：`what/todo.md`（TODO-001..015）。边界：`shape/todo.md`。证明：`proof/todo.md`。

本文件只写 BlindPlan T1、obligations wire、before/after/recovery、同 snapshot 结论、V1 bridge、PrefixEpoch seal、sink reconciliation、reviewer/finality、migration **算法**；语义以 TODO 条款为准，不平行定义（TODO-014）。

程序形态：facts 上的 CE / 递归等待（Journal 变化 → 同 snapshot 重读）。禁止一阶 `WaitingReview|Settling|Submitting|StartingReview|WaitingRebase` 大状态机与 wall-clock poll（TODO-012）。

## Ownership

唯一 writer / 禁止平行 owner 见 `shape/todo.md`。

---

## 词汇（算法用）

```text
Tk = TodoWriteAccepted(k)     // 先有 Prepared
C(k-1) = CurrentObligations immediately before Tk
Pk = submitted complete obligation account at Tk
Rk = process-review obligation of Tk
ConsumableReview(k) ≡ TodoReviewConcluded(k)
T1 = first TodoWriteAccepted on this Life     // BlindPlan commitment（TODO-015）
```

Accepted supersession（TODO-005）：

```text
Prepared(Tk)  freezes Base=C(k-1), Submitted=Pk; Current unchanged
Accepted(Tk)  => CurrentObligations := Pk immediately
Review(k)     => verdict/report only; never rewrites Current
```

没有 `semanticMerge`、reviewer settlement、status progress min 或 accepted-but-not-current 半态。

---

## BlindPlan T1（TODO-001/015）

Manager OpeningPolicy = BlindPlan（GLORY-074）。算法：

```text
LifeOpened
  → BlindPlan Opening（Planning Table；Pre-T1 guidance）
  → todowrite(T1) = first accepted on this Life
  → validate obligations wire（TODO-002/003）
  → durably TodoWriteAccepted(T1)
  → render canonical T1 result containing entrustment revelation
  → persist exact provider-visible result
  → return（conversation tool result only）
  → Opening closes；WorkRecordStart = OpeningBoundary
  → Living Mission guidance（Post-T1）
```

T1 call + canonical accepted result ∈ constitutive OpeningMaterial（COMPANION-014）。  
交托 **禁止** system prompt / Persona / Role Law 切换（PROMPT-014；GLORY-075）。  
每个新 Life（含 Reawakening）重新进入 BlindPlan Opening。  
T1 无 prior lag-1 replacement（desiredCutoff(T1) 无 prior）。

---

## Before 程序（admission + Prepared + sink 投影）

归属 TODO-002/003/004/005/006/007。

```text
todoCheckpointBefore:
  1. require open Manager Life；V2 runner 无 hook parity → fail closed（TODO-004）
  2. admitSingleCheckpointOrFail
       - 同 assistant message >1 不同 ToolCallId todowrite → 全部拒绝，无 ordinal winner（TODO-004）
       - 同 Life 同时最多一个新 admission
  3. same ToolCallId replay：
       - 已有 Prepared/Accepted → 校验 input/Life/BaseObligations/ordinal digest
       - 一致 → 同一 TodoWriteId / 同一 obligation account / 不新增 checkpoint
       - 冲突 → identity corruption fail closed
  4. synchronizePreviousReviewIfAny：
       - 若存在 Accepted(k-1) 且尚无 TodoReviewConcluded(k-1)
         → 阻塞至 ConsumableReview(k-1) durable（TODO-006）
         → 该阻塞是合法因果等待，不是 `MagicTodoReject` 对 Manager 的 fail-closed 红字
       - 仅 VerdictKnown 不足
       - 读取 verdict/report 供本次 tool result 交付；**不**因此改 CurrentObligations
  5. C(k-1) = projection 当前最后 Accepted 对应 account（TODO-005）
  6. 读 provider input → decode obligations: [{ name, work }]（TODO-002）
       - duplicate name → fail closed；禁止靠 work 文本猜 identity（TODO-003）
       - provider account 必须描述 mission debt，不得用 `plan/analyze/write todos` meta-work 代替 T1 完整道路账
       - 禁止 kind/id/status/priority/reviewing 回流 provider 真值
  7. Pk = normalized submitted obligations
  8. append TodoWritePrepared
       - 冻结 tagged provider arguments 的 canonical `ProviderInputDigest`
         与 BaseObligations / Proposed digests
       - 返回真实 Journal `EventId`；after/recovery 仅以它填 `TodoWriteAccepted.PreparedFactRef`
       - ReviewFrontier = 本 tool-call 前 exclusive cursor（绑 ManagerLifeId）
         pending before-hook：= next-assigned + 同 message 中本 call 之前的可捕获 part 数
         （不得把 frontier 冻在最后一条助手文本将占用的 cursor）
  9. install ephemeral bridge（process-local Map + hidden Symbol；非 durable）
 10. mutateArgsInPlace → Host TodoTable V1 compatibility sink only
       （content/status/priority 投影；canonical 仍是 obligations；reviewing sink 策略见 HOST canary）
```

`Prepared` ≠ checkpoint；不派生 Rk。provider-visible 真值 = obligations，不是 sink 枚举。

---

## V1 bridge（ephemeral only）

归属 TODO-004/007/012；Host 细节 HOST-*。

```text
key = sessionID + ":" + callID
carrier[Symbol] = {
  baseObligations, submittedObligations, previousReview,
  compatibilityProjection
}
```

- after 成功消费后 delete entry；tool/turn failure cleanup 清残留。
- **禁止** bridge 表示 Prepared/Accepted/obligation/settlement。
- crash recovery **忽略** bridge；只读 Journal + physical ToolPart（TODO-012）。
- Canary：before 改 args 不得 alias 改写 durable pre-before ToolPart.input（HOST membrane；失败则 membrane 不得上线）。

---

## After / recovery → Accepted + ensureReview

归属 TODO-004/006/007/008/009。

Physical success **双路径**收敛同一 `TodoWriteId + input digest + output digest`（TODO-004）：

```text
live path     = executor 成功返回并进入 tool.execute.after
recovery path = 完整 SDK snapshot 中该 call ToolPart completed
```

严格顺序：

```text
1. recover bridge 或从 Prepared + physical evidence 重建渲染输入
2. ensure TodoWriteAccepted（幂等；继承 Prepared 的 ReviewFrontier）
     - Prepared + live/recovery success + digest 对齐 → Accepted
     - fold Accepted 时 CurrentObligations **立即切到 Prepared.Submitted**（TODO-005）
     - Prepared + failed/absent/digest mismatch → 永不 Accepted，Current 不变
3. ensure DedicatedTodoReviewer（每 Life 一次 logical；proven-loss 才 Replace）（TODO-008）
4. ensureReview(Tk)：
     Accepted 且尚无 TodoReviewConcluded(Tk)
       → Rk 必然义务（TODO-006）
       → ensure TodoProcessReviewAssigned（同 TodoReviewId 幂等 range）
       → 提交/续跑 process reviewer
     ManagerCheckpointLWR 允许 RawGap；不得等 Manager Y 追平（TODO-008）
5. desired lag-1 cutoff 由 Accepted 链纯推导；此处不 commit PrefixEpoch（TODO-009）
6. render tool result：
     - 若 Tk = T1 → canonical entrustment revelation（TODO-015；conversation only；system 字节不变）
     - 上一 ConsumableReview 的 verdict + ProcessReviewLWR（若有）；REVISE 是反馈，不是 rollback
     - 本次 Accepted 后的 CurrentObligations = Pk
     - 不出现 `settled/proposed/preview/reviewing` 状态机词
7. cleanup bridge → return
```

禁止：先启动 reviewer 再 Accepted（幽灵 review）。  
禁止：Host TodoTable=Pk 冒充 Accepted。

`ensureReview` 可在 after / restart / 下一 todowrite before / suicide **任意重入**（TODO-012）。

---

## 同 snapshot 结论（VerdictKnown → ConsumableReview）

归属 TODO-006/008。

```text
VerdictKnown(k)          // Reviewer 域 durable PERFECT|REVISE；立即定业务 outcome
  → await canonical ProcessReviewLWR(k) record-ready
       range = ReviewWorkStartCursor .. ReviewerRecordFrontier
       includeOpening = false
       与 verdict / frontier 同一 Journal snapshot
  → append TodoReviewConcluded(k){ Verdict, WorkRecordRef, Digest, ... }
  → 此 fact 只封口 review obligation；不得写 CurrentObligations
  → 此后才可被 Tk+1 / suicide 同步
```

递归 CE（禁止一阶 PC / wall-clock poll；TODO-012）：

```text
awaitConsumableReview(checkpoint):
  snap = readJournalSnapshot(checkpoint)
  if snap.tryTodoReviewConcluded(TodoReviewId) = Some c
       → return c                          // ConsumableReview ≡ Concluded
  ensureProcessReviewAssigned(checkpoint)  // 幂等；ReviewWorkStartCursor = assignment authority 后 exclusive end
  match snap.tryVerdictKnown(TodoReviewId) with
  | None →
       evidence = lifecycleWorkRecordRange(
            ManagerSessionId,
            LifeOpeningCursor,             // OpeningMaterial 另附；LWR includeOpening=false
            ReviewFrontier(k)              // never crosses frozen frontier
          )                                // RecordCoverage；RawGap 允许
       ensureReviewerPrompt(checkpoint, evidence)
  | Some _ →
       // VerdictKnown 但 ProcessReviewLWR 尚未 record-ready
       // 不得在此 append TodoReviewConcluded
       ()
  awaitJournalChange(checkpoint)
  → awaitConsumableReview(checkpoint)      // 同 snapshot 重读普通程序
```

- PERFECT 与 REVISE **都必须**有 prose work record；无 prose 不得 Concluded（TODO-008）。
- 禁止提前 append 空壳 Concluded；禁止 raw terminal/summary 顶替 WorkRecordRef（TODO-012）。
- Manager-facing LWR：复用 Finality safety-seal；不 regex 清洗；无法证明安全 → fail closed；仅放宽 process 协议允许的 PERFECT/REVISE/review 词面（TODO-013）。

Process Rk 输入（TODO-008）：

```text
OpeningMaterial
+ ManagerCheckpointLWR(k)  // through ReviewFrontier(k); includeOpening=false; RecordCoverage
+ Ck + Pk
```

报告 LWR：request-range only；不得 session head / dedicated reviewer head；不得塞入 R(k-1) history / assignment prompt 自身。  
`ReviewWorkStartCursor` = assignment authority 落地后的 exclusive end（TODO-006/008）。

---

## PrefixEpoch seal 时点（lag-1）

归属 TODO-001/008/009。

```text
desiredCutoff(Tk) = Before(T(k-1) tool-call)   // T1 无 prior replacement
```

Accepted 只导出 desired policy。下一 **真实** provider attempt：

```text
messages.transform
  → derive latest required Todo cutoff from Accepted 链
  → await PrefixCoverage-proven Y（若未 ready）
  → materialize proven Y prefix only（禁止 LWR RawGap 进入 replacement）
  → 在 attempt seal / ProviderRun 绑定之前
       原子 append PrefixRebaseCommitted(
         EvidenceKind=TodoCheckpoint,
         EpochId/PrefixSnapshot/Cutoff/SealRoot/YBundle...
       )
  → ActivePrefixEpoch 切换
  → 该 attempt 与全部 retry 使用新 epoch
  → provider request
```

- provider 成败 **不**回滚已 seal epoch。
- todowrite after **不**提交 PrefixEpoch。
- 禁止：先发新 prefix 后补 committed；禁止 provider-success-gated commit；禁止第二套 ActivePrefixEpoch（TODO-012）。

投影形态（严格 lag-1）：

```text
Opening forever raw + byte-stable
+ proven Y(after Opening .. Before(T(k-1)))
+ raw X[T(k-1) .. current]
```

Blogger `effectiveStart = max(RecordCoverage, WorkRecordStart)`（TODO-001）。

---

## Compatibility sink repair

归属 TODO-007。

```text
Canonical Current = latest Accepted account
Host TodoTable     = compatibility projection only
```

before 可先把 submitted Pk 投影给 builtin executor；只有 physical success → Accepted 后 Pk 才成为 canonical。若 executor 失败、crash 或 replay 令 Host sink 与 projection 漂移，下一安全边界可幂等重投影 canonical Current：

```text
Host TodoTable := project(CurrentObligations)
不产生 checkpoint / review
不改 canonical
```

REVISE **不是** sink rollback 触发器。Host store 永不参与 canonical recovery。

---

## Reviewer / Finality 行为

归属 TODO-008/010/013；Finality 细节 GLORY/REVIEW。

### Process 并行

```text
Accepted(Tk) 后 Manager 可立即独立工作
Rk 不阻塞 Tk 返回；只阻塞 T(k+1) / suicide drain（TODO-006）
```

### Dedicated 生命周期

```text
process duty / physical session：至少到 LifeCompleted（或 proven-loss Replace）（TODO-008/010）
Finality：首次进入 terminal Finality 时作 ordinary cohort member enlist
         → 之后 ordinary graduate；不强制每轮回流（TODO-010）
process PERFECT ≠ terminal first PERFECT；enlist 后 fresh barrier + dual-PERFECT
Finality dedicated LWR：FinalityReviewWorkStartCursor..VerdictFrontier，includeOpening=false（TODO-008）
Blessing / ordinary graduate 不 Dispose process session
```

### Suicide 前序（tail drain）

`suicide` 是尚未被下一 todowrite 消费的 process review 的**唯一** tail drain（禁止再调 todowrite flush——会创造 R(k+1)）（TODO-010）。

```text
suicide:
  requireAtLeastOneTodoWriteAcceptedOnFirstUnblessed(life)
       // first unblessed path ∧ 零 Accepted → fail closed（TODO-010）
  review = drainLatestProcessReview(life)
       // = awaitConsumableReview(latest Accepted) 若尚无 Concluded
       //   否则读取已有 TodoReviewConcluded
  match review.Verdict with
  | REVISE →
       return processRevisionResult(review)    // 含 ProcessReviewLWR；不 create FinalityRequest；Life 继续
                                               // Current 仍为 latest Accepted；Manager 后续改账
  | PERFECT →
       return existingFinalityWorkflow(CurrentObligations)
       // 无机械 terminal-todo completeness gate（TODO-010）
```

Blessed fast path **同样先** `drainLatestProcessReview`，然后才 rest in peace。  
二次 suicide：仍 drain 最新 process review；REVISE → 继续 Life；PERFECT → 不新开 2N，rest in peace / LifeCompleted 后才可释放 Dedicated process reviewer（TODO-010）。

### 基础设施失败（非 verdict）

```text
create/resume/assignment/LWR materialization failure
  → 永远不是 REVISE/PERFECT
  → 不推进 ConsumableReview
  → obligation 保持 outstanding
  → event-driven ensure（禁 wall-clock polling）
  → 不可证明 → typed infra failure；Finality/下一 TodoWrite 不得越过（TODO-012）
```

---

## Crash recovery（只从 facts）

归属 TODO-004/006/007/009/012。

| 裂缝 | 动作 |
|------|------|
| Prepared，physical 未完成/失败 | 不 Accepted；下次 before 从 Journal canonical 覆盖 sink |
| Prepared + live/recovery success，无 Accepted | ensure Accepted（digest 对齐） |
| Accepted，无 ConsumableReview | derive Rk → ensure Dedicated/Assigned/ensureReview；VerdictKnown 无 LWR → await journal 同 snapshot 再结 |
| reviewer prompt 已发，plugin crash | 证明原 session → resume；永久丢失 → Replace；不确定 → fail closed |
| Concluded 已落，尚无下一 todowrite | 无需额外 fact；Current 早已由 Accepted 确定；下一 before/suicide 只消费 review feedback |
| executor 成功，无 Accepted | 仅 Prepared+completed ToolPart 可 ensure Accepted；否则 checkpoint 未成立 |
| desired cutoff 有、PrefixEpoch 未 commit | 下一次 transform seal 前原子 commit（无 RebasePending Stage） |
| 仅 VerdictKnown，waiter 丢 | 重建同一等待；禁空壳 Concluded |

禁止：TodoStage/ResumeAt/HasPendingReview 等 PC（TODO-012）。

---

## Migration 算法

归属 TODO-001/011。

```text
completed Life
  → 保持 completed；不回放 Magic Todo

open Life + legacy WorkActivated
  → LifeOpened 有效；WorkActivated = inert legacy
  → Opening floor → WorkRecordStart
  → 不改写历史 provider bytes

open Life 尚未 WorkActivated
  → 直接 single-stage active Life
  → 禁止再发 Activation continuation

正常新 Life
  → CurrentObligations = []
  → 禁止从 Host TodoTable adopt

升级瞬间已存在的 legacy open Life（一次性）
  → 首次 Magic provider request 之前：
       Host old table 作 seed
       投影为 obligations `{name, work}`
       append LegacyTodoSeedAdopted
       带 current account 注入 Manager provider-visible context
  → 之后只认 obligations account；name 稳定续存
  → 同 session 后续新 Life 不得再次 adopt
```

---

## 端到端节拍（对照）

```text
LifeOpened → BlindPlan Opening（Planning Table）
→ T1 Prepared/Accepted → entrustment revelation（conversation）→ WorkRecordStart
→ ensureReview(R1) → desired cutoff 可推导（T1 无 prior replacement）
→ Manager 工作 ∥ D reviews R1（TODO-006/008）；system prompt 字节不变
→ T2 before 等 TodoReviewConcluded(R1) → 读取 feedback；Current 不回滚
→ Accepted T2 → Current=P2 → R2 → 下一 attempt seal 前 PrefixEpoch(TodoCheckpoint)（TODO-009）
→ …
→ suicide drain latest ConsumableReview → REVISE 继续 / PERFECT → Finality（TODO-010）
→ Blessed 后仍可 todowrite；Dedicated process 保留
→ 二次 suicide drain → PERFECT → LifeCompleted → 可释放 Dedicated process
```
