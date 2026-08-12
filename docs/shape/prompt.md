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

`ProjectionChoice = UsePrefixProbe _` 表示本 attempt 使用候选前缀（CTX-010）；`UseCommittedEpoch` 表示明确选择已提交 epoch。二者仅对该 attempt 有效，不构成领域事实。`StrengthReplica` 不得携带 prefix probe（`mayCarryProbe=false`），且成功/失败都不作为 owner fallback 证据（`clearsFailureCountOnSuccess=false`；STRENGTH-004/015）。

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

## Prompt composition · Persona · Library · Language 所有权

行为：`what/prompt.md` PROMPT-014..019；Persona 矩阵：`AGENT-028/029`；语言绑定写：`HOST-026`。  
本节只划唯一 owner；不复述层文案。

| 关注点 | 唯一 owner | 边界 / 禁止 |
|------|------|------|
| Persona 矩阵（Role × initial tier → SessionPersona） | `PersonaCatalog`（AGENT-028） | Prompt 不另造第二矩阵；Binding 名不得冒充 Persona 自称（AGENT-029） |
| `SessionPersona` 绑定写 | session 创建路径（一次） | 创建后不可重绑；Fallback / Strength / BlindPlan T1 / review 不得改写（PROMPT-014） |
| System prompt 身份字节（同一 Life） | Prompt composition 装配（PROMPT-014/015） | byte-identical；不得因 T1 / Peer Fallback / Strength / compaction / reanchor 替换 |
| Composition 层权威 | PROMPT-015 五层：World / Role / Library / Runtime / Mission | 层可互告知，不得互冒充；冲突按语义所有权裁决，**不**设「更靠近 system 者胜」全序 |
| Common Law / Role Law 资源 | `resources/provider/<semantic>/{en,zh-CN}.md`（SURFACE-004；PROMPT-017/019） | 文件名只存 localized representation；semantic identity 稳定；旧 `resources/prompts/*-system.md` 已删除 |
| Office Library | PROMPT-016 + canonical volumes | 知识≠权威；不扩 Role 权；fast/deep 同书；他处已有 SSOT 则组合引用，不造第二真源 |
| Tools surface | 当前 generated tool schema（Attempt profile） | Tools **不是** Role Prompt 章节；capability 变化不改人格 |
| Lifecycle orient 文本 | Activation / Reawakening / Continuation / Handoff / Fission / Departure 各 owner | 只 orient；generic Activation ≠ Manager BlindPlan；不得触发 system prompt 替换（TODO-015） |
| `ProviderLanguage` 类型 | PROMPT-017（`English` \| `SimplifiedChinese`） | protocol id / tool 名 / wire field / enum / path / command **永不翻译** |
| Provider-visible prose（Class A） | 各 semantic owner + `ProviderResources`（PROMPT-019） | 禁巨型 `TranslationRegistry`；禁 feature `match lang` prose；bound 缺 locale → fail closed |
| `SyntheticToml` | layout / escaping only（PROMPT-019） | **不**拥有 prose 语义；只渲染已本地化串 |
| `ToolHostCodec` | wire 编解码（PROMPT-019） | 接收 already-localized Description；**不**拥有 / 不翻译 tool prose |
| `SessionProviderLanguage` 绑定写 | HOST-026（session 创建瞬间） | Prompt / Library / guideline **只读**已绑定语言；禁止 transform 重读全局偏好 |
| child / attached / InternalLeaf 语言 | 继承 owner / commissioner（HOST-026） | 不得各自再绑 |
| Magic Todo Manager-only fragment | TODO-013/015（`MagicTodoManagerGuideline` + Pre-T1/T1/Living Mission） | 禁止并入全局 `PairProgrammingGuidelineText`（HOST-013） |
| Pair Hint semantic payload | HOST-013 `ProjectionConstants.PairProgrammingGuidelineText` | Cursor/ordinary 只换 wire renderer；User/System 实验 role 不成为 PromptIngress authority |
| Assistance continuation | PROMPT-018 `NeedHelpEscalation` / `NeedHelpAdvice` | 只延长现有 LogicalRun；显式 EffectiveAgent binding，不写 FallbackCursor |
| Companion / Blogger system | COMPANION-004（`resources/provider/role/blogger` via PromptResources） | 不经本五层 composition 冒充；禁止动态 token/预算注入 |
| Provider prose ownership ratchet | ARCH-016 Gate E | 已知 provider-surface owner 禁新增 NL literal；baseline 只减不增 |

禁止平行 owner：

```text
OpeningPromptRaw / AssignmentText 拼接冒充 Mission 或 Opening
Sphinx Kernel / MCP observation 假装 Prompt composition 层；Sphinx 是独立认识状态 owner（SPHINX-005）
把 ExecutionBinding 变化写成 Persona / system prompt 换人
把 Office Library 写成 Role Law 或 universal bible
SyntheticToml / ToolHostCodec / TranslationRegistry 冒充 prose semantic owner
```