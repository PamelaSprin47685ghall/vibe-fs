# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可重写而不改 WHAT。

## 类型与函数地图（dispatch-protocol）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/002 | `Application/Prompting/PromptDispatcher.fs`（`Runtime`：`RegisterAuthority`、`AcceptHumanRoot`、`Abandon`、`AcceptContinuation`、`AcceptAgentOwnerRoot`）+ `Application/Prompting/PromptDispatcherSend.fs`（`RecordSendOutcome`） | 唯一写入口；四态事实由 `Runtime.Persist` 落 `PluginPromptClaimed/Submitted/PhysicalAccepted/Abandoned` |
| 003/004 | `Domain/PromptAuthority.fs` → `PromptClaim`（`Receipt: TransportReceipt option`）；`PromptAuthorityRun.submitClaim`；`Kernel/Identity.fs` → `TransportReceipt.isAdmissionShaped` | receipt 只记不解决；admission 形态可判别 |
| 005/006 | `Domain/PromptAuthority.fs` → `claimScopeDigest`、`nextClaimSequence`、`derivePromptKey`；`PromptDispatcherSend.deriveKey` | 确定性幂等身份；序列在注册时消费 |
| 007/008 | `Interaction/Dispatch/Recovery.fs` → `reconcile`、`reconcileClaim`、`findPhysical`（tail window 内 `role=user` + PromptKey metadata 匹配）；`RecoveryGate`（post-init 单飞） | Proven / StillPending(hasReceipt) / GaveUp / Unreadable |
| 009 | `PromptDispatcher.AwaitMode`（Await/Detached）；`RecordSendOutcome` 的 `acceptanceCallback` 分支 | Detached 不回调；claim/submit 照常 |
| 010 | `Domain/PromptAuthority.fs` → `AuthorityExecutionProfile` 无 model 字段；`PromptDispatcherSend.fs` 发送 options 恒 `Model = None` | 「Root 不得选 model」结构性不可表达 |
| 011 | `Interaction/Authority/PromptFactFold.fs`（`foldPromptClaimed` 用 `projection.RuntimeStartCount` 盖章）+ `Domain/PromptAuthority.fs` → `recoveryAttempts`/`recoveryBudgetSpent` + `PromptAuthorityRun.abandonClaim` | budget 由 workspace 水印派生，不写恢复事实 |

事实折叠：`PromptAuthorityLedger.foldPromptClaimed/Submitted/PhysicalAccepted/Abandoned` 是
`PromptAuthorityProjection` 的纯 fold。`PromptDispatcher.Runtime` 无可变 authority 状态——每次读
走 fold（`ProjectionFor`），journal 是唯一 writer。

## 关键机制

### 四态事实链（PROMPT-005）

```text
Claimed → Submitted → PhysicalAccepted
Claimed → Abandoned
Claimed → Submitted → Abandoned   （恢复期无法证明物理落地）
```

`RecordSendOutcome` 对 `AdmittedWithReceipt` 只写 Submitted（claim 保持 pending，等
`chat.message`）；`AdmittedWithPhysicalMessage` 写 Submitted 再立刻 Accept；
`Retryable/Fatal` 写 Abandoned(SendFailed)；`AcceptanceUnknown` **什么都不写**——保持 pending
让恢复去找，abandon 会许可重发。

### PromptKey 组成（PROMPT-011）

```text
PromptKey = digest(SessionId, LogicalRunId, AuthorityRootUserMessageId,
                   Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence` 以 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 为 scope（`claimScopeDigest`
是 `\u001f` join 串，非 hash，测试可读组件），在 claim 注册时消费——abandon 后同 payload 再发
得到新序号新 key，不会撞同一个幂等锚。Key 进 Host metadata（`PromptMetadataCodec`），不占对话字节。

### 恢复协议（PROMPT-011）

`reconcile` 在插件启动后单飞跑一次：对每个 pending claim 读目标 Session 尾部
`RecoveryTailWindow` 条，找 `role=user` 且 metadata PromptKey 完全一致的消息：

```text
找到 → 按 claim.Origin 补写 PhysicalAccepted（AgentOwnerRoot 同时注册 authority）
未找到 且 attempts < RecoveryAttemptBudget → StillPending（绝不重发）
未找到 且 attempts ≥ budget → Abandoned(UnresolvedAfterRecovery)
```

`attempts = RuntimeStartCount - ClaimedAtRuntimeStartCount`：由 fold 在 claim 注册位置盖章
（`foldPromptClaimed projection.RuntimeStartCount`），后续 start 只推进 workspace 水印，O(1)。

## 约束

- 无第二 writer：`Abandon` 是唯一 abandon 写点（recovery 与 send-fail 共用），禁止在
  `RecordSendOutcome` 内另造事实形状。
- 恢复只证明或放弃，从不 resend：`reconcile` 没有发送端口。
- `RecoveryGate` 是 latch（Task 已完成态），不是 stage/phase 计数器（ARCH-001）。

## 历史与弃权

- **精确常数**：`RecoveryTailWindow=50`、`RecoveryAttemptBudget=3`（`PromptAuthority.fs` 顶部
  `let` 而非 `[<Literal>]`，保证可断言）——COVERAGE 判 HOW/GARBAGE；WHAT 只要求「有界尾部窗口 +
  有界启动预算 + no blind resend」。数值若变，本包文档不随之升格为命题。
- **`postPromptFireAndForget` 旁路**：GARBAGE——已被 `AwaitMode.Detached` 取代，禁止重建。
- **PROMPT-012 残留**：Student/Teacher 已删（GARBAGE）；「插件 user-shaped message 一律经
  PROMPT-005，不得直接 `prompt_async`」保留为 DISPATCH-PROTOCOL-011（corrective §3.4 closed-world
  producer invariant 的发送侧）。
- **恢复执行时序**（post-init 单飞门、构造函数内跑会与 Host 抢事件循环）：HOW——时序可改，
  at-most-one 语义不变。
- **claim 恢复的存储级重放顺序**：`loadJournalEnvelopes` 按 `compareSortKey`（同 RuntimeId 按
  LocalSeq，异 RuntimeId 按 ObservedAt）排序后整体重放，`ClaimedAtRuntimeStartCount` 在重放位置
  重新盖章。测试用远未来 boot 日期固定顺序（见 recovery 测试头注释）。
