# Review — 目标实现

## Implements

行为合同见 `what/review.md`；本文件只描述 attempt、seal、challenge、witness 派生，以及 TodoProcessReview 与 ConsumableReview 算法。  
Magic Todo admission/settlement 见 todo 文档（TODO-001..014）；Finality cohort 编排见 glory 文档。

## Ownership

Review writer、seal、RequestKind、Dedicated、LWR 边界见 `shape/review.md`。

---

## Seal 流程（归属 REVIEW-010）

fail-closed 边界定义在 `shape/review.md`。实现：

```fsharp
type ProviderInputSeal =
    { SessionId
      PhysicalUserMessageId
      SealDigest
      CanonicalVersion
      IncludedToolResultDigests: Set<string> }
```

```text
messages.transform 返回最终消息视图
→ 生成 ProviderInputSeal
→ 下一 assistant/provider run 绑定 ProviderRunIdentity
→ judge 执行时查询该 run 的 seal
→ 证明 IncludedToolResultDigests 含 ChallengeContentDigest
```

绑定失败 → 不写 seal，不确认 PERFECT。

工具名稳定为 `judge`；参数字段 `verdict` ∈ {PERFECT|REVISE} 保留；旧名 `verdict`（工具）非法、无 alias；成功回执不 echo verdict（REVIEW-001）。

本 seal 链仅服务 **FinalityReview** 二次 PERFECT（REVIEW-003）。TodoProcessReview 不签发 challenge，不查 challenge digest。

---

## REVIEW-004：ReviewAttemptIdentity

```fsharp
type ReviewAttemptIdentity =
    { ReviewBarrierId
      GitTreeHash
      ReviewerSessionId
      ProviderRunIdentity
      ToolCallId }
```

同一 `ProviderRunIdentity`（含同 assistant message 内并行/重复 tool call）中的额外 PERFECT 不计数、不写 Journal。

Finality 使用完整五元组。TodoProcessReview 仍以 `ProviderRunId`/`ToolCallId` 标识 terminal `judge` 尝试，但不分配 Finality `ReviewBarrierId` 见证语义。

---

## REVIEW-005：两条独立因果链

链 A — ConfirmationPrompt（发送身份）：

```text
Claimed → Submitted → PhysicalAccepted
```

链 B — ChallengeEvidence（模型是否消费过 skeptical 结果）：

```text
Issued → IncludedInInputSeal → ConsumedByProviderRun
```

**FinalityReview** 成立只依赖链 B。  
第二次 PERFECT 判定只能返回：`Confirmed` | `PendingIdentity` | `Rejected`。  
PhysicalBound 未完成时禁止 same-root 猜测成功。

**TodoProcessReview** 不使用链 A/B：一次 durable `judge` 即 `VerdictKnown`；可消费性走 REVIEW-014/017 的 LWR record-ready，不走 challenge seal。

---

## Reviewer continuation 唯一 writer

`ReviewerWorkflow.observe` 是 reconciled Reviewer turn 的唯一业务决策入口：

| RequestKind | prose-only | 首个 PERFECT | confirmed / 单次 terminal |
|-------------|------------|--------------|---------------------------|
| FinalityReview | `ReviewerGuard` | `ReviewConfirmation` | ConfirmedReviewWitness → cohort 消费 |
| TodoProcessReview | process guard / 重试策略（无 challenge） | **不适用** | `VerdictKnown`（经 `judge`）→ record-ready 路径 |

`HostReviewGuard` 只完成 claim / transport；`FinalityController` 与 process-review orchestrator 只等待 durable 事实，不直接冒充 Reviewer continuation writer。Finality request 关闭后，`ReviewerEvidence.continuationOpen` 以 manager/request/barrier 关系撤销未发送 capability。Process Rk 在 `VerdictKnown` 后无 confirmation capability 可发。

---

## RequestKind 路由（REVIEW-013）

```text
assignment authority 携带 ReviewerRequestKind
→ ReviewerWorkflow / Host 程序按 kind 分支
→ TodoProcessReview：禁止进入 PerfectChallengeIssued / ReviewConfirmation
→ FinalityReview：禁止在单次 PERFECT 后直接当 terminal witness
```

禁止：

```text
if state.pendingChallenge then finalityElseProcess
用同一 Stage 字段编码两种业务
```

---

## TodoProcessReview 端到端（REVIEW-014/015/016/017）

```text
TodoWriteAccepted(Tk)
→ TodoReviewId = digest(ManagerLifeId + TodoWriteId)
→ ensure DedicatedTodoReviewer（无则 Enlisted；TODO-008）
→ ensure TodoProcessReviewAssigned
     冻结 ReviewWorkStartCursor、ManagerReviewFrontier(=ReviewFrontier(k))
     同 TodoReviewId 幂等，禁止第二段 assignment range
→ materialize ManagerCheckpointLWR(k)（RecordCoverage，includeOpening=false；TODO-008）
→ 注入 OpeningRaw + LWR + Ck + Pk
→ Reviewer 本 turn：prose 工作记录 + judge(verdict=PERFECT|REVISE)
→ durable VerdictKnown(k)（Reviewer 域）
→ 业务 outcome 立即按 TODO-005 可推导
→ await 同 snapshot：ProcessReviewLWR(k) record-ready
     range = ReviewWorkStartCursor..ReviewerRecordFrontier
→ append TodoReviewConcluded(k){ Verdict, WorkRecordRef, Digest, ... }
→ ConsumableReview(k) 成立（TODO-006）
```

T(k+1) before / suicide drain：

```text
if 无 TodoReviewConcluded(k):
    ensureAssignment / ensureReview
    await Journal change 直至 ConsumableReview(k)
else:
    直接 fold 消费
→ settle（TODO-005）；REVISE 时 Host TodoTable reconciliation 见 TODO-007（不派生新 R）
```

`ensureReview` 可在 after、restart、下一 todowrite、suicide 任意重入；不创建第二 TodoWrite，不另开第二 assignment range（TODO-012）。  
Persona（Examiner/Auditor）与 system 字节跨 Fallback/Strength 不变（AGENT-028、FALLBACK-014）。

---

## record-ready 等待算法（REVIEW-017，对齐 GLORY-072/073）

```text
loop:
  snap ← Journal.currentSnapshot
  if TodoReviewConcluded(k) ∈ snap: return Consumable
  if VerdictKnown(k) 且同 snap 可物化 ProcessReviewLWR(k) 含正式 Chronicle 段:
       WriteBlob / 持有 WorkRecordRef
       append TodoReviewConcluded(k)   // 仅此刻
       return Consumable
  if coverageCanAdvance ∨ 等待 Blogger/Y 合法追赶:
       await AgentJournal.awaitChangeFrom(snap)
       continue
  else:
       typed infrastructure failure 或 FinalityUndecided 同类 fail-closed
       // 不 append 空壳 Concluded，不伪 REVISE
```

LWR 四标题：`Opening / Chronicle / Recent work / Closing report`（COMPANION-003；过程/终末 `includeOpening=false`）。

禁止 timer/sleep/re-probe 轮询；禁止用 raw terminal 文本写入 `WorkRecordRef`（TODO-012）。

Manager-facing 交付前走 Finality 同款 safety-seal：不能证明安全 → fail closed（REVIEW-016，TODO-013）。

---

## 基础设施失败与恢复（REVIEW-018，TODO-012）

| 裂缝 | 动作 |
|------|------|
| Accepted 有、ConsumableReview 无 | 由 Accepted 派生义务 → ensure Dedicated → ensure 同 TodoReviewId assignment → ensureReview；仅 `judge`/VerdictKnown 则走 record-ready 等待 |
| assignment 已发送、进程崩溃 | 从 Journal/physical 证明原 session/attempt → resume/observe；永久丢失 → REVIEW-019；不确定 → fail closed |
| create/resume/LWR 物化失败 | 非 PERFECT/REVISE；义务 outstanding；可恢复则 event-driven ensure；否则 typed infra failure，阻塞下一 TodoWrite 与 Finality |
| VerdictKnown 有、waiter 丢、尚无 Concluded | 从 assignment + frontier 重建等待；禁止提前 Concluded |
| Concluded 已有 | 直接消费，不重跑 Rk |

---

## Dedicated 替换算法（REVIEW-019，TODO-008）

```text
仅当 Host 证明 OldSessionId 永久不可恢复
→ append DedicatedTodoReviewerReplaced{ EvidenceRef, NewSessionId }
→ 新 session 加载：
     OpeningRaw
     + frontier-bounded Manager LWR 至最新已消费 checkpoint
     + 全部既往 ProcessReview WorkRecordRef
→ 其后 ensureReview 只使用 NewSessionId
→ DedicatedReviewerId（logical）不变
```

超时、单次失败、负载调度 **不得** 触发本路径。

---

## Finality 与过程交集（REVIEW-020，TODO-010）

suicide 前序（细节义务归 TODO-010 / glory；审查语义在此）：

```text
first unblessed ∧ 零 TodoWriteAccepted → fail closed
latest Rk 无 ConsumableReview → ensure + await ConsumableReview
settle latest
  REVISE → 返回 ProcessReviewLWR；不创建 FinalityRequest；Life 继续
  PERFECT → 既有 Finality preconditions
blessed fast-path 仍须 drain 最新过程 review；过程 REVISE 不得 rest in peace
```

Dedicated 首次 terminal Finality：

```text
ordinary roster enlist（若未 graduate；GLORY-003/045）
fresh barrier / tree / dual-PERFECT
Finality LWR：FinalityReviewWorkStartCursor..FinalityVerdictFrontier
// 不得塞入历史 process turns（TODO-008）
graduate 后：process session 仍保留至 LifeCompleted（TODO-010）
```

---

## 终末双 PERFECT 端到端顺序（FinalityReview）

```text
检查工作树并对照 8 大质量支柱（REVIEW-011）生成评估报告
→ PERFECT1 → challenge tool result
→ ReviewConfirmation 启动下一 request
→ transform 生成 seal（含 challenge digest）
→ 第二次检查工作树并验证不变性
→ PERFECT2 查 seal → ConfirmedReviewWitness
→ Finality cohort 消费：
   首个 durable REVISE → 立即关闭 cohort → event-driven record-ready + WriteBlob 预置 primary → 首个工具结果 FinalityRejected（FinalityPrompt.rejected）；
   后续 durable sibling REVISE → primary 预置成功后密封前逐员物化 → FinalitySiblingSteered + FinalitySteer steer prompt（comment-only Synthetic TOML，仅 `# ` 注释，无 TOML 数据块）；
   primary 或任一 sibling 硬物化失败（RecordUnavailable / coverageCannotAdvance）→ fail-closed FinalityUndecided（primary 失败时零 FinalitySiblingSteered），禁止静默丢弃；
   全员双 PERFECT → blessing（GLORY-044/060/072/073）
```

过程路径**永不**进入上表 challenge/seal/witness 段。
