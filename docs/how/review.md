# Review — 目标实现

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
→ verdict 执行时查询该 run 的 seal
→ 证明 IncludedToolResultDigests 含 ChallengeContentDigest
```

绑定失败 → 不写 seal，不确认 PERFECT。

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

## REVIEW-005：两条独立因果链

链 A — ConfirmationPrompt（发送身份）：

```text
Claimed → Submitted → PhysicalAccepted
```

链 B — ChallengeEvidence（模型是否消费过 skeptical 结果）：

```text
Issued → IncludedInInputSeal → ConsumedByProviderRun
```

Review 成立**只依赖链 B**。  
第二次 PERFECT 判定只能返回：`Confirmed` | `PendingIdentity` | `Rejected`。  
PhysicalBound 未完成时禁止 same-root 猜测成功。

## 端到端顺序

```text
PERFECT1 → challenge tool result
→ ReviewConfirmation 启动下一 request
→ transform 生成 seal（含 challenge digest）
→ PERFECT2 查 seal → ConfirmedReviewWitness
→ Manager Guard 只认有效 witness
```
