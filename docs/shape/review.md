# Review — 所有权与边界

## REVIEW-006：自包含 ReviewWitness

Manager Guard **不得**依赖外围 Map 补齐身份。

```fsharp
type ConfirmedReviewWitness =
    { ManagerJobId
      ManagerSessionId
      ReviewerSessionId
      WorktreeIdentity
      ReviewBarrierId
      GitTreeHash
      FirstProviderRunId
      FirstToolCallId
      ChallengeResultDigest
      SecondProviderRunId
      SecondProviderInputDigest
      SecondToolCallId }
```

一个 witness 必须独立回答：谁审的、为哪个 Job、哪棵 tree、两次 provider run、第二次是否真的看过 challenge、是否属于当前 barrier。

**confirmed 只能从 witness 派生，禁止赋值「已确认」标志。**

本类型只服务 **FinalityReview**（及 Orchestrator 终末复审）。TodoProcessReview 不产生、不消费 `ConfirmedReviewWitness`（REVIEW-013/020）。

## REVIEW-007：Manager 面无 Review Guard

Manager completion **不**检查 review witness（GLORY-070）：`ManagerWorkflow` 只按序判
joinOutstanding → JoinGuard、finalityOutstanding → deferred、managerPlanning → Activation、
managerJobHandedOff → 完成，其余 → `ManagerIdleEncouragement`。`TurnCompletionProgram` 不引用
Manager / Reviewer / Student–Teacher 业务。

`ReviewerWorkflow` 是 Reviewer terminal 后 `ReviewerGuard` / `ReviewConfirmation` continuation 的
唯一业务 writer；`HostReviewGuard` 只是 transport primitive。durable REVISE 立即令 Finality cohort 撤销对应
Reviewer continuation capability 并关闭 cohort，不发送 challenge 或 guard；`FinalityRejected` 留待 GLORY-072
record-ready，二者不得倒置。
REVIEW-006 的 `ConfirmedReviewWitness` 只由 Reviewer 侧与 Finality cohort 消费。

Magic Todo 过程同步点在 todowrite before / suicide drain（TODO-006/010），**不是** Manager completion 上的 Review Guard。Manager 不得因「隐藏 Rk 未结束」被 completion 路径拦下；仅下一 checkpoint admission 与 finality 前序等待 ConsumableReview。

## REVIEW-010：ProviderInputSeal 的 fail-closed

若 Host 无法把一次 transform 输出可靠绑定到 `ProviderRunIdentity`，必须 fail closed。  
禁止退回 same-root 或 physical-message 猜测。

Seal 类型与绑定流程见 `how/review.md`。

## REVIEW-012：Reviewer 提示词资源权威来源

Reviewer 角色的系统提示词由 `resources/provider/role/reviewer/{en,zh-CN}.md`（经 PromptResources：Common Law → Role Law → Examiner's Ledger）权威承载，在 Session 加载时作为 Reviewer 系统的 System Prompt，负责向模型灌输 REVIEW-011 Examiner's Ledger 判断方向与工具规范。  
Persona（Examiner/Auditor）session 创建一次绑定（AGENT-028）；本域不因 Fallback/Strength 重写。

双 PERFECT 流程不得写入 Reviewer 提示词（REVIEW-003）：屏障由 Host 侧执行，Reviewer 只需针对当前 tree 给出独立判断；提前告知流程会诱导模型自行扮演确认方。  
禁止 formal report schema / 固定八段标题 / Pass 表（REVIEW-011）。

TodoProcessReview 的 assignment instruction 由 Host 按 RequestKind 注入（过程一次判断、有界 LWR 输入、old/proposed todo）；不得把 Finality challenge/2N/cohort 编排写入过程 prompt，也不得要求 Reviewer 描述隐藏 session / barrier / 消费方（REVIEW-013，TODO-013）。

## 模块所有权（过程 / 终末）

| 职责 | Owner | 边界 |
|------|-------|------|
| `judge` 工具（旧名 `verdict` 非法） | Reviewer tool surface | 参数字段 `verdict` 保留；成功回执不 echo；描述归 prose / WorkRecord |
| Reviewer 域 durable verdict | 既有 Reviewer / Journal | `VerdictKnown` 复用此域；不另造 Magic Todo Stage（REVIEW-014，TODO-012） |
| `TodoProcessReviewAssigned` / `TodoReviewConcluded` | Magic Todo journal facts（TODO-006） | Concluded 仅 record-ready 后 append |
| Dedicated enlist / replace | Host-owned hidden runtime + durable facts | Manager handle 面不可见（REVIEW-015/019，TODO-008/013，GLORY-002） |
| Process ensureReview / assignment | Host process-review orchestrator | 幂等重入；与 FinalityController 分型 |
| Finality cohort / dual-PERFECT / Rejected | `FinalityController` + REVIEW-003/006 | process PERFECT 不入 witness（TODO-010，GLORY-058） |
| Canonical LWR materialize | 既有 LWR planner（可 `range` 扩展） | 禁止第二 renderer（REVIEW-016，TODO-008/012） |
| Safety seal（Manager-facing LWR） | 与 Finality 相同 seal 路径 | 不清洗；不能证明安全则 fail closed（TODO-013） |
| Manager 可见过程报告 | todowrite tool result / suicide 返回 | 仅 PERFECT/REVISE + WorkRecordRef 内容；无内部 id（TODO-013） |
| `HostReviewGuard` | Reviewer 面 transport（openBarrier/read/`judge`） | Manager 面已删（GLORY-070）；不得再写 `verdict` 工具名 |
| Prefix rebase | context / todo（TODO-009） | 本域不拥有 epoch commit |

## RequestKind 与 controller 边界

```fsharp
type ReviewerRequestKind =
    | TodoProcessReview of TodoWriteId
    | FinalityReview of FinalityRequestId * ReviewBarrierId
```

- Process controller：观察一次 terminal verdict → 驱动 record-ready → `TodoReviewConcluded`；**无** challenge writer。
- Finality controller：REVISE 关 cohort；PERFECT 走 `ReviewConfirmation` + seal + witness（既有路径）。
- 禁止共享「看 pendingChallenge 猜业务」的单体状态机。
- `HostReviewProgram` / transport 可复用；业务 RequestKind 与终端条件必须分型（REVIEW-013）。

## LWR 与 coverage 边界

```text
                 XTrace / Y
                     │
            ┌────────┴────────┐
            ▼                 ▼
     RecordCoverage      PrefixCoverage
            │                 │
        Y + RawGap         proven Y only
            │                 │
            ▼                 ▼
   Process / Finality     Manager lag-1
   bounded LWR            prefix rebase
```

- Process/Finality LWR：**不得**用 PrefixCoverage 填 gap，**不得**用 session head（TODO-008）。
- Prefix rebase：**不得**用 RecordCoverage/LWR RawGap 证明可替换（TODO-009；rebase 细节归 context/todo）。
- Opening 经 OpeningRaw 旁路；LWR 一律 `includeOpening=false`（TODO-001）。
- `ReviewFrontier(k)` 在 Prepared/Accepted 冻结；后续并发工作不得改写已冻结 Rk 输入界。

## 隐藏面与泄漏边界

Manager 允许感知（TODO-013；GLORY-030 窄例外 → 此条）：

- checkpoint 存在过程审查义务（todowrite 文案层）
- 上次 ConsumableReview 的 PERFECT/REVISE + canonical 报告正文
- REVISE merge preview 等 todowrite 结果字段（归 todo 文档）

Manager 禁止感知：

```text
DedicatedTodoReviewer / ReviewerSessionId
fast-reviewer / deep-reviewer / hidden task
barrier / witness / dual PERFECT / 2N / roster
ensureReview / record-ready / Journal waiter
```

fork schema 无 Reviewer；隐藏 target 统一 unavailable（GLORY-002）。动态 LWR 正文不做 forbidden-word 清洗（GLORY-048）；safety 不能证明则 fail closed（REVIEW-016，TODO-013）。

## Dedicated 生命周期边界

```text
LifeOpened
  → 首次 Accepted：Enlisted（logical + physical）（TODO-008）
  → 多次 process assignment（同 logical；同 physical 优先）
  → 首次 Finality：ordinary enlist（若尚未 graduate）（TODO-010，GLORY-003/045）
  → graduate ≠ Dispose
  → Blessing / Rejected 后继续 process duty
  → LifeCompleted 或 proven-loss Replaced
```

替换入口唯一：REVIEW-019。Finality ordinary release 规则不得误删仍负有 process duty 的 dedicated session（TODO-008/010）。