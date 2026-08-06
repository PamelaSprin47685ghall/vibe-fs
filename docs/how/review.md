# Review — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在自动化代码审查中，单次 LLM 输出的“同意/通过（PERFECT）”极易被模型随口给出，且容易忽略潜在的死角缺陷。Review 模块旨在建立严格的双 PERFECT 验证与怀疑式质询（Skeptical Challenge）机制，要求模型在第一次 PERFECT 后必须接受怀疑式质询工具输出，并通过密码学 `ProviderInputSeal` 证明模型在第二次 Provider Run 中确实消费了该质询，才允许派生可信的 `ConfirmedReviewWitness`。

### 2. 输入输出与规则边界
- **输入**：Reviewer 响应、Git Tree Hash、Skeptical Challenge Digest、`ProviderRunIdentity`。
- **输出**：`ProviderInputSeal` 封印事实、自包含的 `ConfirmedReviewWitness` 证据。
- **核心边界与不变量**：
  1. 双 PERFECT + Seal 强制约束（REVIEW-003）：单次 PERFECT 绝对不可信；第二次 PERFECT 判定必须证明质询被成功密封进输入。
  2. Witness 自包含（REVIEW-006）：ReviewWitness 必须自带全量证据，禁止保存在外围可变 Map 中，防止并发 Job 读到空确认。
  3. Git Tree Hash 作废机制（REVIEW-008）：Git 树变基或代码变更后，旧 Witness 自动失效。
  4. 绑定 Fail-Closed（REVIEW-010）：`ProviderRunIdentity` 绑定失败或未封印时，第二次 PERFECT 严禁通过，禁止 same-root 猜测。

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
→ verdict 执行时查询该 run 的 seal
→ 证明 IncludedToolResultDigests 含 ChallengeContentDigest
```

绑定失败 → 不写 seal，不确认 PERFECT。

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

Review 成立**只依赖链 B**。  
第二次 PERFECT 判定只能返回：`Confirmed` | `PendingIdentity` | `Rejected`。  
PhysicalBound 未完成时禁止 same-root 猜测成功。

---

## 端到端顺序

```text
PERFECT1 → challenge tool result
→ ReviewConfirmation 启动下一 request
→ transform 生成 seal（含 challenge digest）
→ PERFECT2 查 seal → ConfirmedReviewWitness
→ Manager Guard 只认有效 witness
```
