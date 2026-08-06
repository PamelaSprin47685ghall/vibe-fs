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
```

`AuthorityExecutionProfile` 是原子 profile 内的稳定 Authority 子记录，不是第二个构造来源。下游需要更窄视图时只能从完整 profile 纯投影或直接传所需参数；禁止分别构造多个可矛盾的子记录再拼回 attempt。

`ProjectionChoice = UsePrefixProbe _` 表示本 attempt 使用候选前缀（CTX-010）；`UseCommittedEpoch` 表示明确选择已提交 epoch。二者仅对该 attempt 有效，不构成领域事实。

禁止从下列碎片临时拼装：

- mutable session cache  
- 最后一条 user message  
- Role map  
- fallback projection  

禁止从本 profile 派生 Companion 资格（COMPANION-002）。
