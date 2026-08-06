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

## REVIEW-007：Manager Guard 边界

Manager 每次 assistant terminal 后检查 review witness。  
`isTopLevelManager` 按 `CanonicalRole = Manager`；Orchestrator 下的 manager 子会话仍进入 guard。

Guard **不**替 Manager 选 coder/reviewer，**不**读 todo。只问：当前 tree 是否有已确认 PERFECT。

顺序：EXEC-016 JoinGuard 优先——仍有 outstanding 后台未 join 时，本 turn 不做 review 检查。

## REVIEW-010：ProviderInputSeal 的 fail-closed

若 Host 无法把一次 transform 输出可靠绑定到 `ProviderRunIdentity`，必须 fail closed。  
禁止退回 same-root 或 physical-message 猜测。

Seal 类型与绑定流程见 `how/review.md`。
