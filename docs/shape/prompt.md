# Prompt — 所有权与边界

行为见 `what/prompt.md`。发送与恢复算法见 `how/prompt.md`。

## PROMPT-005：PromptDispatcher 唯一写入口

所有插件产生的 user-shaped message（Guard、repair、ReviewConfirmation、busy nudge、provider failure continuation、Orchestrator 冲突提示等）必须经过同一个 `PromptDispatcher`。

禁止第二 writer 直接 `prompt_async`。

持久阶段（恰好四类事实）：

```text
Claimed → Submitted → PhysicalAccepted
Claimed → Abandoned
Claimed → Submitted → Abandoned   （恢复期无法证明物理落地）
```

```fsharp
type PromptAbandonReason =
    | SendFailed of error: string
    | UnresolvedAfterRecovery
```

| 阶段 | 含义 |
|------|------|
| Claimed | 发送前持久化：PromptKey、Origin、LogicalRunId、AuthorityRoot、EffectiveAgent、payload digest |
| Submitted | Host 调用返回；`accepted-*` **不是**物理 MessageId，**不是** Authority Root |
| PhysicalAccepted | 仅真实 `chat.message` / 明确 `msg_*`；Authority Root 此时才生效 |
| Abandoned | 终局失败；不改 Active Logical Run；同 PromptKey 不再重发 |

禁止：

- `accepted-*` 升级为 PhysicalAccepted  
- 从 Submitted 推断 Authority 已生效  

## PROMPT-008：原子 AttemptExecutionProfile

一次 provider request 的执行身份必须来自**同一个不可变** profile：

```fsharp
type AuthorityExecutionProfile =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      AuthorityKind: RootAuthorityKind
      SelectedAgent: string
      PeerAgent: string
      CanonicalRole: Role
      SelectedTier: AgentTier }

type AttemptExecutionProfile =
    { Authority: AuthorityExecutionProfile
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      Origin: PromptOrigin
      EffectiveAgent: string
      SystemPromptId: SystemPromptId
      ToolCapabilitySet: Set<ToolPermission>
      RequestKind: ProviderRequestKind
      ProjectionChoice: XProjectionChoice }

type ProviderRequestKind =
    | WorkMain
    | BloggerMain
    | BloggerSquash
    | InteractionRepair
    | StrengthReplica
    // StudentLearn / StudentCompile：编号与枚举臂永久空缺（G3；见 AGENT-020/021、PROMPT-012）
```

`AuthorityExecutionProfile` 是原子 profile 内的稳定 Authority 子记录，不是第二个构造来源。下游需要更窄视图时只能从完整 profile 纯投影或直接传所需参数；禁止分别构造多个可矛盾的子记录再拼回 attempt。

`ProjectionChoice = UsePrefixProbe _` 表示本 attempt 使用候选前缀（CTX-010）；`UseCommittedEpoch` 表示明确选择已提交 epoch。二者仅对该 attempt 有效，不构成领域事实。`StrengthReplica` 不得携带 prefix probe，且成功/失败都不作为 owner fallback 证据（STRENGTH-004/015）。

`toolCapabilitiesFor(CanonicalRole, StrengthReplica)` 仅对 Strength eligible role 返回恰好 `{Read; Glob; Grep}`；其它 role 返回空集。provider schema 与 execution gate 必须读取同一 `ToolCapabilitySet`，禁止 prompt-only readonly 约束。

禁止从下列碎片临时拼装：

- mutable session cache  
- 最后一条 user message  
- Role map  
- fallback projection  

禁止从本 profile 派生 Companion 资格（COMPANION-002）。

`StudentLearn` / `StudentCompile` 与 `toolCapabilitiesFor(CanonicalRole, RequestKind)` 的 Student 双门：
**G3 已删除（absent）**（AGENT-020/021、PROMPT-012 空缺）。不得再按 request kind 投影 Student
`ToolCapabilitySet`，也不得用可变“学习/编译状态”重建 attempt。后继工具面见 SyncDelegate /
`InvocationMode`（AGENT-024、EXEC-026/028）。

Host 在当前 provider assistant id 产生后才暴露 `ProviderRunIdentity`。transform/ToolContext 首次取得 id
时只允许把它绑定一次，随后 provider schema、execution gate 与 terminal reconcile 均读取同一绑定。
