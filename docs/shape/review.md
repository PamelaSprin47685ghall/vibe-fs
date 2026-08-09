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

## REVIEW-007：Manager 面无 Review Guard

Manager completion **不**检查 review witness（GLORY-070）：`ManagerWorkflow` 只按序判
joinOutstanding → JoinGuard、finalityOutstanding → deferred、managerPlanning → Activation、
managerJobHandedOff → 完成，其余 → `ManagerIdleEncouragement`。`TurnCompletionProgram` 不引用
Manager / Reviewer / Student–Teacher 业务。

`ReviewerWorkflow` 是 Reviewer terminal 后 `ReviewerGuard` / `ReviewConfirmation` continuation 的
唯一业务 writer；`HostReviewGuard` 只是 transport primitive。Finality cohort 只等待 durable verdict /
witness，并在 request 关闭时撤销对应 Reviewer continuation capability，不发送 challenge 或 guard。
REVIEW-006 的 `ConfirmedReviewWitness` 只由 Reviewer 侧与 Finality cohort 消费。

## REVIEW-010：ProviderInputSeal 的 fail-closed

若 Host 无法把一次 transform 输出可靠绑定到 `ProviderRunIdentity`，必须 fail closed。  
禁止退回 same-root 或 physical-message 猜测。

Seal 类型与绑定流程见 `how/review.md`。

## REVIEW-012：Reviewer 提示词资源权威来源

Reviewer 角色的系统提示词由 `resources/prompts/reviewer-system.md` 静态资源权威承载，在 Session 加载时作为 Reviewer 系统的 System Prompt，负责向模型灌输 REVIEW-011 的 8 大代码质量支柱与工具规范。

双 PERFECT 流程不得写入 Reviewer 提示词（REVIEW-003）：屏障由 Host 侧执行，Reviewer 只需针对当前 tree 给出独立 verdict；提前告知流程会诱导模型自行扮演确认方。

