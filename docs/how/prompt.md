# Prompt — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
OpenCode Host 接收到的 Physical User Message（`role=user`）仅仅是运输格式，无法直接证明用户意图、选定的 Agent 身份或权限层级。如果直接将 Physical User Message 当作 Authority，会导致 Continuation 或 Repair 错误升权为 HumanRoot、多次重置 Fallback 预算以及身份窃取。Prompt Authority 模块旨在将物理消息转化为原子化的 `PromptOrigin` 与强类型 `PromptKey`，并通过四阶段 Dispatcher 完成安全发送与未决发送崩溃恢复（PROMPT-011）。

### 2. 输入输出与规则边界
- **输入**：Host Physical User Message、Continuation 请求、registered AgentOwnerRoot 分配、Pending PromptKey。
- **输出**：`PromptOrigin`（`HumanRoot` | `AgentOwnerRoot` | `Continuation` | `HostInternal` | `UnknownOrigin`）、`AttemptExecutionProfile` 剖面、`PromptKey` 与物理发送事实。
- **核心边界与不变量**：
  1. 物理消息 ≠ AuthorityTurn（PROMPT-001）：绝对禁止根据文本长度、空白、零宽字符或 Synthetic TOML 注释推断 Authority。
  2. PROMPT-009 解析顺序仅为内部证据匹配优先级，严格遵守 PROMPT-001 规则，禁止从正文反推身份。
  3. 未决恢复 at-most-one（PROMPT-011）：物理落地未证明时保持 Pending，严禁为清理挂起而盲目自动重发。
  4. 绝对强类型：所有 SessionId、LogicalRunId、MessageId 必须为单 Case 强类型，禁止裸 `string` 计算与比对。

---

## PROMPT-006：发送格式

每次发送固定：

```fsharp
{ Agent = Some effectiveAgent
  Model = None
  Directory = directory
  Metadata = metadata }
```

禁止设置 `Model`。Host 按 `config.agent[effectiveAgent].model` 解析。  
PromptKey 必须写入 metadata（PROMPT-011）。

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

```text
RecoveryTailWindow    = 50   // 读目标 Session 尾部以检索同 PromptKey 的 user 消息
RecoveryAttemptBudget = 3    // 跨越 3 次插件启动
```

具体恢复执行算法：

```text
recoverUnresolvedPrompts():
    for each pendingRecord in Journal.getPendingPrompts(): // 状态为 Claimed 或 Submitted
        promptKey = pendingRecord.PromptKey
        targetSession = pendingRecord.SessionId
        attemptCount = pendingRecord.RecoveryAttemptCount + 1

        // 1. 从 Host SDK 读取 Session 尾部 RecoveryTailWindow (50) 条消息
        tailMessages = HostSDK.getMessages(targetSession, limit=RecoveryTailWindow)
        
        // 2. 检查是否有 role=user 消息的 metadata 携带与 promptKey 完全一致的值
        matchedMsg = tailMessages.find(msg => msg.role == "user" && msg.metadata.promptKey == promptKey)

        if matchedMsg is Some:
            // 找到匹配的物理消息 -> 补写 PhysicalAccepted 事实，恢复成功
            Journal.commit(PhysicalAccepted(matchedMsg.id, promptKey))
        else:
            if attemptCount >= RecoveryAttemptBudget (3):
                // 连续 3 次启动均无法证明物理落地 -> 判定为 Abandoned
                Journal.commit(Abandoned(promptKey, UnresolvedAfterRecovery))
            else:
                // 仍未超预算 -> 保持 Pending，自增 attemptCount，绝对不自动重发物理请求
                Journal.updateAttemptCount(promptKey, attemptCount)
```

合同约束：
1. **at-most-one logical effect**：绝对禁止为清理挂起记录而盲目自动重发。
2. **幂等护栏**：在写入 `PhysicalAccepted` 之前必须复查 Journal 确保该 PromptKey 未被提交过。

---

## PromptKey 组成与强类型

```fsharp
PromptKey = digest(
    SessionId, LogicalRunId, AuthorityRootUserMessageId,
    Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence`：同一 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 下由 journal fold 派生的单调序号——使「同一 Guard 连续发两次」成为两个 key。

所有 ID 字段（`SessionId`、`LogicalRunId`、`AuthorityRootUserMessageId` 等）在实现中必须使用强类型单 Case 包装，不得使用裸 `string` 进行计算或比对。
