# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。
> 新工程师用它把命题对到代码。

## 类型与函数地图（interaction-authority）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/002 | `Kernel/Identity.fs` → `PhysicalUserMessageId.promoteToAuthorityRoot`、`TransportReceipt`（`isAdmissionShaped`） | 唯一 crossing；`TransportReceipt` 与物理 id 类型不同 |
| 003/004/011 | `Domain/PromptAuthority.fs` → `AuthorityExecutionProfile`、`PromptAuthorityProjection`；`Domain/PromptAuthorityRun.fs` → `createAuthorityRoot`、`registerAuthority`、`claimContinuation` | root 固定 profile；continuation 继承 run/root |
| 005 | `Domain/PromptAuthority.fs` → `PromptOrigin`、`RootAuthorityKind`、`ContinuationKind`、`originLabel`、`tryParseContinuationKind` | 闭世界枚举 |
| 006 | `Domain/PromptAuthority.fs` → `parseAgentNameTyped` / `AgentNameRejection`；`Domain/ManagedAgentCatalog.fs` → `legacyAgentNames`、`peerNameOf` | 显式 agent 才可成 root |
| 007/008/009/017 | `Domain/PromptAuthorityRun.fs` → `resolveKnownOrigin`（accepted → claimed → compaction → AgentOwnerRoot → UnknownOrigin）；`Application/Prompting/PromptIngress.fs` → `resolveOrigin`（唯一可授予 HumanRoot 的边界） | 纯函数永不返回 HumanRoot；ingress 只在 ActiveProfile 缺席 + 显式有效 agent 时授予 |
| 010 | `Interaction/Authority/Model.fs` → `repairFamilyPayloadDigest/repairFamilyAlreadyClaimed`（ordinary LogicalRun+family）、`repairPayloadDigest/repairAlreadyClaimed`（Blogger terminal-scoped special case）、`idlePayloadDigest/idleAlreadyClaimed`（Manager Life+business condition）；均由 `ClaimSequences` 派生 | 自动 continuation budget durable 且不能由自己产生的新 ProviderRun 扩张 |
| 011 | `Domain/PromptAuthority.fs` → `AttemptExecutionProfile`、`buildAttemptExecutionProfile`（唯一 builder） | authority 子记录原子携带 |
| 012/013 | `Domain/PromptAuthority.fs` → `ContinuationKind.NeedHelpEscalation | NeedHelpAdvice`；`Infrastructure/OpenCode/Host/AssistanceHost.fs` | assistance 续推同 run；该同步交互只 Await Host transport result（便于本调用判断拒绝），不等 provider execution/slot；abort 不推进 fallback |
| 014 | `Execution/Delegation/Fork/OpenCode/JoinGuard.fs`、`Mission/Manager/Idle.fs`；`ContinuationKind.JoinGuard | ManagerIdleEncouragement` | join/idle 续推 = continuation；JoinGuard Await transport result，以便拒绝时释放 reservation；Manager idle process key + durable claim 都按 Life + plan-commitment condition |
| 015 | `Session/JoinInterruptRegistry.fs`（`UserMessageArrived`）；`PromptIngressCodec`（ExternalUserIngressPulse 候选） | wake 低权限；ingress 不给 authority |
| 016 | `Domain/PromptAuthorityRun.fs` → `acceptClaim`（root 不入 continuation map） | root ≠ continuation |

事实折叠：`Interaction/Authority/PromptFactFold.fs` 把 `PluginPromptClaimed/Submitted/PhysicalAccepted/Abandoned` 与
`AuthorityRootAccepted` fold 进 `AgentProjectionSet`；`Interaction/Authority/Ledger.fs` 是
`PromptAuthorityProjection` 的纯 fold（`foldAuthorityRootAccepted`、`foldPromptClaimed`…）。authority
状态没有第二份内存拷贝——`PromptDispatcher.Runtime` 无可变 authority 字段，每次读都走 fold
（`ProjectionFor`）。

## 关键接线：HumanRoot 只能在 ingress 授予

`PromptIngress.handle` 是「物理用户消息成为 authority」的唯一入口（PROMPT-004）：

```text
resolveOrigin（journal 已知 provenance）
  → UnknownOrigin 时：ExplicitAgent 有效 AND ActiveProfile=None（首个外部 prompt）→ HumanRoot
  → 其余一律 UnknownOrigin（fail-closed）
```

`Runtime.AcceptHumanRoot` 再校验显式 agent，随后 `RegisterAuthority` 写 `AuthorityRootAccepted` 事实。
`AcceptAgentOwnerRoot` 要求 claim 是 pending AgentOwnerRoot（`claimAgentOwnerRoot`），且先写
`PluginPromptPhysicalAccepted` 再 `RegisterAuthority`——PhysicalAccepted 不能排在 root 生效之后
（PROMPT-005 顺序）。

## 约束

- `PromptDispatcher.Runtime` 不持有 authority 状态：防内存拷贝与 journal 分叉（PROMPT-005 的
  durability 前提）。
- continuation 归属只读 `ActiveLogicalRun`（不回退 `LastAuthorityProfile`）。
- root 的 `registerAuthority` 清空 run-scoped 映射（PendingClaims/AcceptedContinuationIds/ClaimSequences），使 PERSIST-008 有界。
- ordinary repair 的 claim scope payload 只含 repair family；LogicalRunId 已是 scope 组件，所以同 run 后续 terminal 不会重置预算。Blogger 仅因 exact-one 协议需要 terminal identity 而保留 terminal-scoped digest，并由 nudge→AABB→exhaust 状态机单独限界。
- Manager idle digest = `LifeId + conditionKey`；condition 由 Manager 是否已有 plan commitment 决定，ProviderRunIdentity 不参与自动 encouragement budget。

## 历史与弃权

- **legacy agent 名单与精确错误文案**（`ManagedAgentCatalog.legacyAgentNames`、
  `formatLegacyNameNotSupported`）：COVERAGE 判 AGENT-004 为 GARBAGE（migration ratchet）。
  本包保留 WHAT「HumanRoot 必须显式 managed agent + 拒绝是 typed 的」（AGENT-005），精确名单与
  文案只在 HOW 记录，不升格为命题。`student-teacher-absence.mjs` 等 absence ratchet 已随新世界
  基线稳定删除（CLN-Z；PROOF-MAP DELETE 清单）。
- **`PromptAuthority.fromString` / ManagerGuard 历史 journal 解析**：COVERAGE 判 HOW——仅用于
  解析历史 journal 行，生产不再发送 ManagerGuard continuation（GLORY-070）。ManagerGuard 仍是
  `ContinuationKind` 成员（可解析），但不再作为新发送的 origin。
- **PROMPT-012（Student/Teacher）**：GARBAGE——编号永久空缺，无 alias、无 deprecated 路径。
  「插件 user-shaped message 仍经 PROMPT-005」保留给 `dispatch-protocol`。
- **QuiescencePermit 资格机制**：`cache.md` 的 idle-only auto-continue 资格（SessionQuiescenceGate）归 `causal-wait`（HOST-004 分片）；本包只拥有「idle 续推是 continuation、自动预算必须稳定有界」。permit 只防同一次 idle race，不替代跨 terminal/restart 的 durable budget。
- **`AttemptExecutionProfile` record 字段集**：HOW（HANDOFF §18.4 integration structure），
  不是未来 WHAT；「字段不可从碎片拼装」才是 WHAT（INTERACTION-AUTHORITY-011）。
