# dispatch-protocol — WHAT

## [001] 合成消息统一调度

插件及内部机制生成的 user-shaped 消息一律经统一调度入口发送，不得绕过调度准入直接向 Host 发 prompt。

## [002] Claim 四态

持久事实只沿 `Claimed → Submitted → PhysicalAccepted` 或 `Claimed (→ Submitted) → Abandoned` 流转。Submitted 记录传输回执，claim 仍待决；PhysicalAccepted 证明物理落地；Abandoned 结束该 claim，不再重发。

先持久确认 claim，再发送。发送前因状态变化失效，明确记 Abandoned，不冒充传输失败或成功。

## [003] Receipt 不授予物理身份

Host 的 `accepted-*` 是传输接纳回执，不是物理消息 ID、权限证据或消息已被处理的证明。

## [004] PhysicalAccepted 需要真实物理证据

只有真实物理用户消息才能建立 PhysicalAccepted：运行时取得物理消息身份，或恢复时在 Host 历史中找到携带完全相同 PromptKey 的物理用户消息。含 agent 的历史 PromptKey 只读解码供恢复匹配，不双写、不用于新调度。

## [005] PromptKey 确定性

PromptKey 由 `(SessionId, LogicalRunId, AuthorityRootId, Origin, PayloadDigest, ClaimSequence)` 确定性哈希派生；同一输入跨进程相同，任一要素改变得到新 key。不使用随机数，不包含 agent、peer 或 model；含 EffectiveAgent 的历史形式仅单向只读兼容。

## [006] 独立 Logical Act 可区分

ClaimSequence 在 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 内单调递增，注册 claim 即消费。相同 payload 的独立调用，包括放弃后的新调用，获得不同 sequence 与 PromptKey。

## [007] 结果未知不重发

恢复或核对找不到物理证据时保持 StillPending，不自动重发，也不因重启次数判放弃。物理 acceptance 前的确定 Retryable/Fatal 拒绝可记 `Abandoned(SendFailed)`；只有这种确定未发送的结果可归还 idle nudge 的 exact quiescence permit。acceptance unknown 或持久化不确定均不可 re-arm。

判断 exact occasion 是否已提醒，只认 Pending 或 Accepted 的 dispatch evidence；ClaimSequence 只区分调用，不是 effect/admission witness。

## [008] At-most-one Logical Effect

同一 runtime 内，terminal gate nudge 的 exact occasion `(SessionId, LogicalRunId, Origin, PayloadDigest)` 共享一条 claim→send flight；并发观察者等待相同 PromptKey 与结果，不重复消费 sequence、写 claim 或发送。

flight 结束后，只有 durable Abandoned 允许新 sequence；Pending、Submitted、PhysicalAccepted 均禁止重发。不以时间窗口代替 key 校验，不虚构物理 exactly-once，不为消除挂起而重发。

## [009] Detached 及时交还控制

Detached 在 durable claim 确认、Host 异步发送入栈后返回 PromptKey，不等模型容量、provider 执行或物理 acceptance。后续致命拒绝触发进程级审计报错，保留待决 claim，不自动重试；需同步获知传输拒绝时显式使用 Await。

物理消息后的 durable execution 由 managed-chat-execution 独占，dispatch 不创建或推进 execution facts。

## [010] 调度不裁决模型

Dispatch 与 Authority Root 不选择、等待或覆盖 model。发送的 Host agent 固定为不可变 participant，model 不指定（null）；continuation 与 fresh physical target 都不改变 participant。显式外部 agent 保留并与 participant 校验一致。

模型与容量只由 execution-model-routing 在 Host 执行准入时裁决。

## [011] 合成来源可辨

所有内部合成 user-shaped 消息携带合法 PromptKey 与结构化来源元数据，使无插件元数据的真实外部物理用户输入可无歧义识别。

## [012] PhysicalAccepted 交接

Dispatch 仅交接 exact `(SessionId, PhysicalUserMessageId)`、agent-free PromptKey 和 interaction-authority 的原子 AttemptExecutionProfile。profile 携 IdentitySeed 派生的不可变 participant、role、persona、catalog version 与 provenance，不用可重新推导的 metadata 替代。

每个 fresh physical execution 按固定 role 路由；capacity identity 为 `session + physical + role + participant + target + fence`。Turn 调和在本地绑定缺字段时，从同一 durable profile 恢复 participant/role；显式 Host agent 优先，缺席的 role 不得遮蔽 durable role。Accepted evidence 只含 participant+role，不含 effectiveAgent。

Execution acceptance、provider start、terminal、settlement 归 managed-chat-execution；dispatch 不复制状态机、不取容量、不建 execution binding、不解释 provider failure。

## [013] Activation 先于 Recovery

构造只装配能力，不读 journal、不调和 claim、不恢复 execution、不启动 timer/polling。durable substrate 激活成功后，才依据 durable claim 与 Host 物理证据恢复 dispatch；execution recovery 委托 managed-chat-execution。两者均不以 wall clock 推进事实。

## [014] Dispatch Fatal

仅 typed dispatch invariant incident 可请求 fatal。先保留已发送或结果未知的 durable Pending/PhysicalAccepted truth 与 exact PromptKey settlement，不改写成未发送；再调用 composition 注入的 mandatory fatal capability。同一 incident 至多一次 report/kill，不直接使用 physical adapter 或 optional/default/global fallback。

## [015] 身份 Carrier

chat.message 仅接受 JSON plain own-property record 中的正式 carrier。字符串须非空白且保留原字节；不 trim、字符串化、按 truthiness 或前缀猜身份。

SessionId 从 input、output、output.message、output.info 统一收集，形式为 `sessionID`、`sessionId`、字符串 `session`，或 plain record 的 `session.id/sessionID/sessionId`。显式类型非法、非 plain record、继承字段冒充 own field 或多个合法值不同，均 fail closed；缺失与非法区分，同值 carrier 只产生一个 opaque SessionId。

PromptKey 与 agent 多 carrier 也遵守 typed、同值唯一规则。新 wire 只传 agent-free PromptKey；含 agent 的形式仅在持久载荷边界单向只读解码，不回写、不双写。新 dispatch 不携带或投影 peer/side/fallback agent。
