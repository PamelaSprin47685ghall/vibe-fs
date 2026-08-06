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

### 失败类型与强类型边界

所有领域标识（`SessionId`、`LogicalRunId`、`MessageId`、`PromptKey` 等）必须使用单 Case 包装类型，禁止使用裸 `string` 偷渡身份。失败原因禁止直接使用裸 `string` 传递，必须收敛为强类型 DU：

```fsharp
type DispatchError =
    | TransportFailed of detail: string
    | SchemaInvalid of detail: string
    | QuotaExhausted
    | HostRefused of reason: string

type PromptAbandonReason =
    | SendFailed of error: DispatchError
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

## PROMPT-008：原子 AttemptExecutionProfile 与巨型记录拆分

一次 provider request 的执行身份必须来自**同一个不可变** profile：

```fsharp
type AttemptExecutionProfile =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: MessageId
      PhysicalUserMessageId: MessageId
      ProviderRunIdentity: ProviderRunIdentity
      Origin: PromptOrigin
      SelectedAgent: AgentName
      PeerAgent: AgentName
      EffectiveAgent: AgentName
      CanonicalRole: AgentRole
      SelectedTier: AgentTier
      SystemPromptId: SystemPromptId
      ToolCapabilitySet: ToolCapabilitySet
      RequestKind: ProviderRequestKind
      ProjectionChoice: XProjectionChoice option }

type ProviderRequestKind =
    | WorkMain
    | BloggerMain
    | BloggerSquash
    | InteractionRepair
```

### 巨型上下文治理与子记录分立

为了防止模块越界读取非本领域字段，`AttemptExecutionProfile` 在各子系统间传递时必须按边界解耦为聚焦的子记录：

1. **`AuthorityProfile`**：聚合 `SessionId`、`LogicalRunId`、`AuthorityRootUserMessageId`、`Origin`、`CanonicalRole`、`SelectedTier`，由 Authority 模块独立消费。
2. **`RequestProfile`**：聚合 `ProviderRunIdentity`、`SelectedAgent`、`EffectiveAgent`、`SystemPromptId`、`ToolCapabilitySet`、`RequestKind`，由 Transport / Dispatcher 消费。
3. **`ProjectionContext`**：聚合 `ProjectionChoice` 与 Snapshot 关联指针，由 Projection 模块消费。

`ProjectionChoice = Some _` 表示本 attempt 使用候选前缀 probe（CTX-010）；仅对该 attempt 有效，不构成领域事实。

禁止从下列碎片临时拼装：

- mutable session cache  
- 最后一条 user message  
- Role map  
- fallback projection  

禁止从本 profile 派生 Companion 资格（COMPANION-002）。
