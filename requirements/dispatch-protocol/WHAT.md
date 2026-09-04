# dispatch-protocol — WHAT

## DISPATCH-PROTOCOL-001: PromptDispatcher 是唯一写入口

所有由插件或内部机制生成的 user-shaped 消息（包括 Guard、repair、Finality steer、nudge、重试及 Orchestrator 提示等）必须通过统一的 `PromptDispatcher` 发起，绝对禁止任何旁路机制直接绕过滤网向宿主发送提示。

## DISPATCH-PROTOCOL-002: 四态 claim 生命周期

单次调度的持久化事实严格遵循四态流转：`Claimed → Submitted → PhysicalAccepted` 或 `Claimed (→ Submitted) → Abandoned`。`Submitted` 记录传输回执但保持 Claim 处于待决状态；`PhysicalAccepted` 证明物理落地并完成 Claim；`Abandoned` 代表调度放弃且不再重发。在物理发送前若因状态变更失效，必须显式记录为放弃，禁止伪装成传输失败或成功。

## DISPATCH-PROTOCOL-003: transport receipt 不等于物理消息身份

宿主返回的 `accepted-*` 仅表示传输层已接纳该请求，不是物理消息标识符，亦不是权限生效的证明。系统不能仅凭传输收据推断消息已被实际处理。

## DISPATCH-PROTOCOL-004: physical acceptance 只由真实物理证据建立

`PhysicalAccepted` 状态必须且只能由真实的物理消息证据确立（例如运行时捕获明确的物理消息 ID，或在恢复阶段在宿主历史中匹配到包含完全一致 `PromptKey` 的物理用户消息）。

## DISPATCH-PROTOCOL-005: PromptKey 是确定性幂等身份

`PromptKey` 是由 SessionId、LogicalRunId、AuthorityRootId、Origin、EffectiveAgent、载荷摘要及 ClaimSequence 派生的确定性哈希，禁止使用随机数生成。相同逻辑交互在任何进程中派生完全一致的 Key，任意要素变动均会导致 Key 发生迁移。

## DISPATCH-PROTOCOL-006: 同 payload 的两个独立 logical act 仍可区分

`ClaimSequence` 在 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 作用域内单调递增，且在 Claim 注册时立即消费。即使相同载荷的消息在放弃后再次发送，也会获得新的序号与新的 `PromptKey`，确保同载荷的多次独立调用能够被精确区分。

## DISPATCH-PROTOCOL-007: uncertain physical outcome 不自动重发

在崩溃恢复或证据核对中若未能检索到物理落地证据，Claim 必须保持 `StillPending` 状态，绝对禁止系统自动重发，亦不得因进程重启次数累积而静默判定放弃。反之，Host 若在物理 acceptance 之前给出确定的 `Retryable/Fatal` 拒绝，则该 attempt 可显式 `Abandoned(SendFailed)`；对 idle-derived gate nudge，只有这种“确定未发送”结果允许把 exact quiescence permit 归还为可重试，任何 acceptance-unknown / 持久化不确定性都不得 re-arm。业务层判断 exact occasion 是否已经提醒，只能依赖仍 Pending 或已 Accepted 的 dispatch evidence；历史 ClaimSequence 只区分重试 PromptKey，不是 effect/admission witness。

## DISPATCH-PROTOCOL-008: at-most-one logical effect 不虚构 exactly-once

协议坚守至多一次（at-most-one）逻辑执行保障与未知结果 fail-closed 原则。禁止伪造物理投递的 exactly-once，禁止以时间窗口粗暴替代 PromptKey 校验，禁止为消除挂起状态而盲目重发。

## DISPATCH-PROTOCOL-009: Detached 在 durable claim 后立即交还控制

在分离模式（`AwaitMode.Detached`）下，调度器在完成 durable claim 记录与宿主异步调用入栈后即刻返回 `PromptKey`，调用方不得阻塞等待模型容量调度、provider 执行或物理落地证据。若异步入栈后续发生致命拒绝，系统应触发进程级审计报错，且保留 Claim 待决记录而不自动重试。需要同步获知传输拒绝分支的场景必须显式使用 `AwaitMode.Await`。物理消息落地后的 durable execution 由 `managed-chat-execution` 独占，dispatch 不创建或推进 execution facts。

## DISPATCH-PROTOCOL-010: Root 与 dispatch 不得选择、等待或覆盖 model

调度与 Authority Root 阶段严禁指定、修改或等待底层物理模型 ID。发送参数固定为未指定模型，具体的模型分配与算力租赁严格延迟至宿主执行准入阶段由专门路由模块裁决。

## DISPATCH-PROTOCOL-011: 插件 user-shaped message 一律经 PROMPT-005

所有内部生成的合成用户消息必须携带合法的 `PromptKey` 与结构化来源元数据。此举保证缺乏插件元数据的消息能够被无歧义地识别为真实的外部物理用户输入。

## DISPATCH-PROTOCOL-012: PhysicalAccepted 后只交接 exact identity

Dispatch 在建立 `PhysicalAccepted` 后只向 `managed-chat-execution` 交接 exact `(SessionId, PhysicalUserMessageId)`、`PromptKey` 与 `interaction-authority` 发布的原子 `AttemptExecutionProfile`；该 profile 必须包含完整版本化 `ParticipantIdentityEvidence`，不得退化为可重新推导的 authority metadata。Turn reconciliation 在 process-local binding 缺字段时必须从同一 durable authority profile 恢复 canonical role/identity；显式 Host 证据优先，但 `Role=None` 不得遮蔽 durable canonical role，否则同一 Manager continuation 会被误路由为 Ordinary。`managed-chat-execution` 独占 durable execution acceptance、provider start、terminal 与 settlement；dispatch 不复制其 transition law，不获取容量，不建立 execution binding，不解释 provider failure。

## DISPATCH-PROTOCOL-013: Construction 纯 wiring，recovery 晚于 durability activation

插件构造阶段只装配 dispatcher 与 handoff ports，不读 journal、不调和 pending claim、不恢复 execution、不启动 timer 或 polling。durable substrate 激活成功后，dispatch recovery 才可依据 durable claim 与 Host physical evidence 运行；execution recovery 委托 `managed-chat-execution`，且两者都不得以 wall clock 推进事实。

## DISPATCH-PROTOCOL-014: dispatch fatal先保留claim truth再经注入fuse执行

只有typed dispatch invariant incident可以请求fatal。已发生或outcome unknown的send必须先保留durable Pending/PhysicalAccepted truth与exact PromptKey settlement；fatal不得把它重写为未发送。dispatch runtime只接受composition注入的mandatory fatal capability，不得直接引用physical adapter、optional/default/global fallback；同一incident只允许一次report与kill。
