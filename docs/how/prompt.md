# Prompt — 目标实现

## Implements

行为合同见 `what/prompt.md`；本文件只描述来源解析、dispatcher 和未决发送恢复算法。

## Ownership

Authority、PromptKey 和发送 writer 边界见 `shape/prompt.md`。

---

## PROMPT-006：发送格式

每次发送固定：

```fsharp
{ Agent = Some effectiveAgent
  Model = None
  Directory = directory
  Metadata = metadata
  Tools = requestToolOverride }
```

禁止设置 `Model`。Host 按 `config.agent[effectiveAgent].model` 解析。  
PromptKey 必须写入 metadata（PROMPT-011）。

`Tools=None` 是普通请求。Student 请求必须为完整 allow/deny map：Learn 只 allow `teacher`，Compile
只 allow `read/glob/grep/write/edit/return`。不得发送局部 delta；Host 会把该 map 持久为 Session
permission，局部 delta 会让上一 request kind 的 allow 泄漏到下一次请求。

Teacher 首次问题走 `SendAgentOwnerRoot`，后续问题和 idle nudge 走 `SendContinuation`。Student compile
与 compile nudge 同样走 `SendContinuation`；它们只通过受控参数把 request tool override 交给
Dispatcher，不能旁路 Dispatcher 直接发送。

---

## PROMPT-009：来源解析优先级

```text
accepted HostMessageId
→ claimed PromptKey
→ Host compaction / synthetic
→ registered AgentOwnerRoot
→ proven external prompt acceptance (HumanRoot)
→ UnknownOrigin
```

先匹配者生效；落到 UnknownOrigin 则 fail-closed（PROMPT-004）。

---

## 未决发送恢复算法（PROMPT-011）

当 Host 已接受 prompt 但插件在写入 `Submitted` 或 `PhysicalAccepted` 前崩溃时，必须通过未决发送恢复算法在下一次启动时确定物理落地情况：

窗口和启动预算由 PROMPT-011 唯一定义；算法只使用符号
`RecoveryTailWindow` 与 `RecoveryAttemptBudget`。

具体恢复执行算法：

```text
recoverUnresolvedPrompts():
    for each pendingRecord in Journal.getPendingPrompts(): // 状态为 Claimed 或 Submitted
        promptKey = pendingRecord.PromptKey
        targetSession = pendingRecord.SessionId
        attemptCount = pendingRecord.RecoveryAttemptCount + 1

        // 1. 从 Host SDK 读取 Session 尾部 RecoveryTailWindow 条消息
        tailMessages = HostSDK.getMessages(targetSession, limit=RecoveryTailWindow)
        
        // 2. 检查是否有 role=user 消息的 metadata 携带与 promptKey 完全一致的值
        matchedMsg = tailMessages.find(msg => msg.role == "user" && msg.metadata.promptKey == promptKey)

        if matchedMsg is Some:
            // 找到匹配的物理消息 -> 补写 PhysicalAccepted 事实，恢复成功
            Journal.commit(PhysicalAccepted(matchedMsg.id, promptKey))
        else:
            if attemptCount >= RecoveryAttemptBudget:
                // 预算耗尽仍无法证明物理落地 -> 判定为 Abandoned
                Journal.commit(Abandoned(promptKey, UnresolvedAfterRecovery))
            else:
                // 仍未超预算 -> 保持 Pending，自增 attemptCount，绝对不自动重发物理请求
                Journal.updateAttemptCount(promptKey, attemptCount)
```

提交 `PhysicalAccepted` 前复查 Journal 中同一 PromptKey 是否已提交，使重入保持幂等。

---

## PromptKey 组成与强类型

```fsharp
PromptKey = digest(
    SessionId, LogicalRunId, AuthorityRootUserMessageId,
    Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence`：同一 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 下由 journal fold 派生的单调序号——使「同一 Guard 连续发两次」成为两个 key。

ID 的类型边界见 `shape/prompt.md`。
