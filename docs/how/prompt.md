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

未决发送恢复行为见 `what/prompt.md` PROMPT-011（GOV-011：行为归 what/）。PromptKey 组成与恢复步骤按该条款实现。

### PromptKey 组成

```fsharp
PromptKey = digest(
    SessionId, LogicalRunId, AuthorityRootUserMessageId,
    Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence`：同一 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 下由 journal fold 派生的单调序号——使「同一 Guard 连续发两次」成为两个 key。

实现点：`Claimed`/`Submitted` 恢复检索用 metadata 携带的 PromptKey；写 `PhysicalAccepted` 前复查 Journal 未接受过同 key，幂等（PERSIST-009）。
