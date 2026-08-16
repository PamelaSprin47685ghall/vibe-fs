# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可重写而不改 WHAT。

## 类型与函数地图（dispatch-protocol）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/002 | `Application/Prompting/PromptDispatcher.fs`（`Runtime`：`RegisterAuthority`、`AcceptHumanRoot`、`Abandon`、`AcceptContinuation`、`AcceptAgentOwnerRoot`）+ `Application/Prompting/PromptDispatcherSend.fs`（`RecordSendOutcome`） | 唯一写入口；四态事实由 `Runtime.Persist` 落 `PluginPromptClaimed/Submitted/PhysicalAccepted/Abandoned` |
| 003/004 | `Domain/PromptAuthority.fs` → `PromptClaim`（`Receipt: TransportReceipt option`）；`PromptAuthorityRun.submitClaim`；`Kernel/Identity.fs` → `TransportReceipt.isAdmissionShaped` | receipt 只记不解决；admission 形态可判别 |
| 005/006 | `Domain/PromptAuthority.fs` → `claimScopeDigest`、`nextClaimSequence`、`derivePromptKey`；`PromptDispatcherSend.deriveKey` | 确定性幂等身份；序列在注册时消费 |
| 007/008 | `Interaction/Dispatch/Recovery.fs` → detached `reconcile` / `reconcileClaim` / `findPhysical`（tail window 内 `role=user` + PromptKey metadata 匹配） | Proven / StillPending(hasReceipt) / Unreadable；普通 plugin lifecycle 不调用 |
| 009 | `PromptDispatcher.AwaitMode`（Await/Detached）+ `OpenCodePort.SdkClientPort/HttpPort.SendPrompt` async enqueue observer | Detached 在 claim 后调用 `prompt_async` 即返回，不等 model slot / Host Promise / PhysicalAccepted；异步 rejection → process fatal + 不重发 |
| 010 | `Interaction/Authority/Model.fs` → `AuthorityExecutionProfile` 无 model 字段；`Interaction/Dispatch/Send.fs` 发送 options 恒 `Model = None`；`Sessions.fs` send 栈不 acquire model | Root/dispatch 均不能选 model；`chat.message` execution admission 才 acquire |
| 011 | `PromptClaim.ClaimedAtRuntimeStartCount` / `RuntimeStartCount` 仅保留历史兼容与审计；restart-count abandon policy 已退役 | 重启次数不再产生业务 terminal |

事实折叠：`PromptAuthorityLedger.foldPromptClaimed/Submitted/PhysicalAccepted/Abandoned` 是
`PromptAuthorityProjection` 的纯 fold。`PromptDispatcher.Runtime` 无可变 authority 状态——每次读
走 fold（`ProjectionFor`），journal 是唯一 writer。

## 关键机制

### 四态事实链（PROMPT-005）

```text
Claimed → Submitted → PhysicalAccepted
Claimed → Abandoned
Claimed → Submitted               （若 crash 后无法证明，则保持 pending；不由重启自动补 terminal）
```

`RecordSendOutcome` 对 `AdmittedWithReceipt` 只写 Submitted（claim 保持 pending，等 `chat.message`）；`AdmittedWithPhysicalMessage` 写 Submitted 再立刻 Accept；`Retryable/Fatal` 写 Abandoned(SendFailed)；`AcceptanceUnknown` **什么都不写**——保持 pending让恢复去找，abandon 会许可重发。

OpenCode `prompt_async` adapter 的 Detached receipt 是**本地 enqueue invocation receipt**，不是 HTTP/SDK Promise 已 settle 的证明。adapter 同步调用 `promptAsync` 后立即返回 receipt，同时旁路观察其 Promise；若 Promise 后来 rejection，调用方已无法安全判断是否部分落地，因此直接 `FatalProcess/Diagnostic.fatal`，保留 pending claim，绝不重发。managed model capacity 完全不在该发送调用栈：物理 user message 到达 `chat.message` 后才进入 scheduler demand。

### PromptKey 组成（PROMPT-011）

```text
PromptKey = digest(SessionId, LogicalRunId, AuthorityRootUserMessageId,
                   Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence` 以 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 为 scope（`claimScopeDigest`
是 `\u001f` join 串，非 hash，测试可读组件），在 claim 注册时消费——abandon 后同 payload 再发
得到新序号新 key，不会撞同一个幂等锚。Key 进 Host metadata（`PromptMetadataCodec`），不占对话字节。

### 证据核对库（PROMPT-011）

`reconcile` 现在是 detached library，不在 plugin init、普通 turn/tool 或 teardown 自动运行。显式调用时只读目标 Session 尾部 `RecoveryTailWindow` 条，找 `role=user` 且 metadata PromptKey 完全一致的消息：

```text
找到 → 按 claim.Origin 补写 PhysicalAccepted（显式证明）
未找到 → StillPending（绝不重发、绝不按 restart count 自动 abandon）
读失败 → Unreadable（不改 claim）
```

`RuntimeStartCount` 与 `ClaimedAtRuntimeStartCount` 可继续作为历史审计字段，但不再是 recovery budget。进程重启本身没有权力替旧 tool 写 terminal。

## 约束

- 无第二 writer：`Abandon` 是唯一 abandon 写点（recovery 与 send-fail 共用），禁止在
  `RecordSendOutcome` 内另造事实形状。
- 证据核对只证明或保持 pending，从不 resend、从不因 restart count abandon；`reconcile` 没有发送端口。
- Detached async enqueue 的 eventual rejection 是 acceptance-unknown invariant，当前进程 fatal；不得降级成 Retryable 后自动第二次发送。
- `SendPrompt` / fork / repair 不 acquire managed model lease；capacity authority 只在 execution-model-routing 的 `chat.message` admission。
- 普通 plugin lifecycle 不接 `RecoveryGate`/reconcile；显式 session resume 由 CRASH-018 `/continue` 承担。

## 历史与弃权

- **精确常数**：`RecoveryTailWindow=50` 保留为物理证据读取上界；`RecoveryAttemptBudget` 行为已退役，restart count 不再驱动 abandon。
- **`postPromptFireAndForget` 旁路**：GARBAGE——已被 `AwaitMode.Detached` 取代，禁止重建。
- **PROMPT-012 残留**：Student/Teacher 已删（GARBAGE）；「插件 user-shaped message 一律经
  PROMPT-005，不得直接 `prompt_async`」保留为 DISPATCH-PROTOCOL-011（corrective §3.4 closed-world
  producer invariant 的发送侧）。
- **自动恢复执行时序**：已退役；constructor/post-init/普通 hook/tool 均不是 recovery trigger。
- **claim 恢复的存储级重放顺序**：`loadJournalEnvelopes` 按 `compareSortKey`（同 RuntimeId 按
  LocalSeq，异 RuntimeId 按 ObservedAt）排序后整体重放，`ClaimedAtRuntimeStartCount` 在重放位置
  重新盖章。测试用远未来 boot 日期固定顺序（见 recovery 测试头注释）。
