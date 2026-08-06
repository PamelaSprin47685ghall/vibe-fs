# Prompt — 目标实现

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

## PROMPT-011：未决发送恢复

Host 已接受 prompt，但插件在写 Submitted / PhysicalAccepted 前崩溃时：

### PromptKey

```fsharp
PromptKey = digest(
    SessionId, LogicalRunId, AuthorityRootUserMessageId,
    Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence`：同一 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 下由 journal fold 派生的单调序号——使「同一 Guard 连续发两次」成为两个 key。

### 恢复步骤

对每个 `Claimed` 或 `Submitted` 的 PromptKey：

1. 读目标 Session 尾部 `RecoveryTailWindow = 50` 条消息  
2. 查找 metadata 含同一 PromptKey 的 `role=user`  
3. 找到 → 补写 `PhysicalAccepted`（真实 `msg_*`）  
4. 未找到 → 保持 Pending，**不自动重发**  

### 边界

```text
RecoveryAttemptBudget = 3   // 跨越 3 次插件启动
```

第 3 次启动仍无法证明物理落地 → `Abandoned(UnresolvedAfterRecovery)`。

合同：

```text
at-most-one logical effect + fail-closed unknown outcome
```

禁止：假装 exactly-once；用时间窗口代替 PromptKey；把 `accepted-*` 当物理落地；为清理挂起而重发。
