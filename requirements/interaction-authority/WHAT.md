# interaction-authority — WHAT

## [001] 物理接收不等于授权

物理 `role=user` 或传输收据不构成 `AuthorityRoot`。只有确立 `PhysicalAccepted` 后，物理消息标识才可经唯一显式提升通道成为 AuthorityRoot；传输收据不得直接提升。

## [002] 来源证据

Authority 只由内建的 typed 来源机制判定。零宽字符、排版、模板、时间戳、长度、合成配置的注释或字段形态均不证明权限。

## [003] Root 独占操作

只有 AuthorityRoot 可创建 Logical Run、提交显式 participant 选择并绑定 owner 为 exact run 准备的版本化 identity evidence、建立 Fallback 根、重置 Interaction Repair 预算，并成为 execution binding 的延续基准。

`AuthorityRootAccepted` 原子记录 SessionId、LogicalRunId、RootId、RootKind、完整 `ParticipantIdentityEvidence` 与初始 physical target/lease；不得先安装 identity 再接受 root。Authority 只核验 exact key 与 owner witness，不从 agent 文本推导身份。提交后的同一 fold 建立 active root、绑定 evidence、清空已关闭 prior run 的 claims、continuation 映射与序列号；未提交则 root 与 identity 均不存在。

## [004] Continuation 不升权

Continuation 只延续既有 Logical Run，完整继承 Run、Root 与 exact identity evidence，不执行 Root 独占操作，不替换或修改身份、底层 AuthorityProfile、Fallback 或 repair 预算。按规则改变 physical target/lease 不改变 participant。

## [005] 闭合来源分类

来源仅为 `AuthorityRoot(HumanRoot | AgentOwnerRoot)`、`Continuation`、`HostInternal`、`UnknownOrigin`，Root 与 Continuation 不得互认。AgentOwnerRoot 携带 identity owner 为 exact child、attached 或 InternalLeaf run 准备的 owner-derived evidence，OwnerLogicalRunId、LogicalRunId 与 root key 精确匹配；缺失、wrong-owner 或 wrong-run evidence 均为 UnknownOrigin。

## [006] HumanRoot 的 participant 依据

HumanRoot 必须有确定的合法 participant，由 `participant-identity` owner 校验并返回版本化 evidence。外部显式选择与 [009] 的既有 durable Profile 是仅有来源；无此依据、legacy 名称、大小写或连字符变形、格式错误或缺 evidence 均 fail-closed，不从 Session cache 猜测，不自行推导 Persona/Role。

## [007] UnknownOrigin

无法证明合法来源立即阻断，不更新执行 Profile、不启用 Fallback、不发起 Continuation。

## [008] 来源解析优先级

固定优先级为：已确认 Host 消息 > 已 Claim 的 agent-free PromptKey > Host 内部 Compaction/Synthetic > 已注册 AgentOwnerRoot > 外部证明合法的 HumanRoot > UnknownOrigin。

## [009] 新授权与用户续行

来源判定的纯计算不推断 HumanRoot，仅 Ingress 可授予。外部消息省略 participant 时，Ingress 必须从当前 Session 的 durable active 或历史 Profile 确定既有 participant；无确定证据、格式错误或身份漂移均拒绝，不从进程缓存猜测。

活跃 Run 中，同一 participant 的合法消息成为 `HumanMessage` continuation；无活跃 Run 时，以经校验的 participant 为新任务建立 HumanRoot。continuation 接纳后推进 Host 的 exact physical user-message binding，既有 Root identity 不变。

## [010] Repair occasion identity

自动 repair 不提升权限，其持久化 identity 精确绑定 `(SessionId, LogicalRunId, request, ProviderRunIdentity, terminal kind)`。同一 occasion 重复观测幂等吸收，任一字段改变都是新 occasion；transport receipt 只表示物理进度，不参与该 identity。普通 gate nudge 的飞行态与重新提醒资格按 [019]。

## [011] 原子执行 Profile

`AttemptExecutionProfile` 原子携带 exact SessionId、LogicalRunId、AuthorityRootId、当前 physical target/lease 及 accepted root 的完整版本化 identity evidence。Authority 精确保管和暴露 participant、Role、Persona、provenance/version，不重新解析或修改；target/lease 仅来自 execution binding。禁止从 Session cache、物理 parent、agent 文本或分散消息拼装 Profile。

## [012] DegenerationGuard

`DegenerationGuard` 是同一 Logical Run 的 typed Continuation，复用 Root 与 Profile，不改变 physical target/lease、不计入模型重试失败次数、不伪装成 `ProviderRetryAttempt`。

## [013] 权限连续性

强类型 continuation 只接续同一 Logical Run；仅 execution binding 的 physical target/lease 可按规则改变，participant、Root、identity evidence 与 Profile 关联不变。SessionId 相同而 LogicalRun 不同，不构成 continuity。

## [014] Nudge 与 JoinGuard

Nudge、JoinGuard 均为 Continuation，不创建 Root。存在未决后台任务时，只能发送 JoinGuard 延续等待。业务 gate 未满足时，每个新的合法 terminal occasion 重新获得提醒资格；只对同一 exact ProviderRunIdentity occasion 去重，不以 Session、Run、Life 或 barrier 永久压制后续提醒。

## [015] 外部消息到达不自行授予权限

运行中外部消息的到达只作低权限唤醒，不取消当前运行、不直接授予 Prompt authority、不重置 Run 或新建 lifecycle。符合 [009] 后才可作为既有授权的用户续行。

## [016] Root claim 不充当续行证据

AgentOwnerRoot claim 接受后不进入 Continuation 查找映射；曾作为 Root 的物理消息不能成为后续 Continuation 的依据。

## [017] 只接续 active run

Continuation 只能挂靠当前 ActiveLogicalRun，不能回退使用已结束或归档的 Profile。

## [018] Exact durable closure

每个 AuthorityRootAccepted 原子记录唯一 ExpectedClosureKind；缺失或无法唯一归类不得接受。闭合集合及唯一合法 durable source witness 为：

| Kind | Witness |
|---|---|
| HumanRootManagerLife | Manager LifeCompleted(LifeId, FactId) |
| HumanRootManagedRun | ManagedLogicalRunTerminal(LogicalRunId, FactId) |
| AgentOwnerChildWork | ChildLogicalRunTerminal(OwnerLogicalRunId, ChildLogicalRunId, FactId) |
| AgentOwnerAttachedWork | AttachedLogicalRunTerminal(OwnerLogicalRunId, ChildLogicalRunId, AttachmentKind, AssociationGeneration, FactId) |
| AgentOwnerInternalLeaf | InternalLeafTerminal(OwnerLogicalRunId, LeafLogicalRunId, DecisionOrTransactionId, FactId) |

HumanRoot 按实际 lifecycle 归类；InternalLeaf 不因物理 Root/Attached ownership 改类。Terminal witness 仅表示 `Completed | Cancelled | Failed` 的 durable closed outcome，不包含 request、signal 或 observation。

解释器精确核对 kind、owner、SessionId、LogicalRunId、AuthorityRootId，幂等追加唯一 `AuthorityLogicalRunClosed`（含上述 root identity 与 ClosureWitness）。同一 fold 才清除 active run、run-scoped mappings 与 active identity binding，并归档 Profile；相同 closure 幂等，冲突拒绝。source 已提交而 closure 未确认时，只重放该 typed source 并重试相同 append，不释放 binding、不复用 SessionId。不得由 lifecycle terminal 本身、association removal、取消请求、idle/timeout、墙钟、Host 观察或旧 Profile 推断 closure。

## [019] Gate nudge 的飞行态与重新提醒

InteractionRepair 的 claim、Submitted、PhysicalAccepted 只证明提醒准入或落地，不证明 gate 完成或预算耗尽。在途 attempt（`finish=None`、`tool-calls` 等）重复 idle/reconcile 继续等待，不并发再发。该 attempt 到达仍不满足 gate 的新稳定 terminal（含空/XML-only stop、length）时，新的 ProviderRunIdentity 获得一次提醒资格；同一 terminal 重复观测幂等。后续 nudge 已准入后，旧 turn 不消费 fresh terminal 的资格；普通 gate nudge 不发布 `INTERACTION_REPAIR_EXHAUSTED`。

## [020] Repair fatal 的证据与边界

只有 typed repair invariant incident 可请求 fatal，先形成 exact PromptKey claim、Submitted/PhysicalAccepted 与 fresh-terminal 判定的 settlement evidence。物理熔断能力由 composition 必须注入，repair 不直接依赖物理退出、不用 optional/default/global fallback。同一 incident 只 report 与 kill 一次；普通耗尽或可恢复发送失败不升级 fatal。

## [021] 历史不改写、不升权

历史 EventStore 事实原样保留，旧身份仅作审计、回溯与不变重放，其解码限于历史边界。活跃权限和准入不把旧 Inspector/Coder 等身份升为 Engineer；新交互只接受 `participant-identity` 的当前合法集合，旧或非法身份拒绝。

## [022] DevOps 延续固定绑定

DevOps 模型配置与 Persona 由道路初始化固定，整个生命周期及 resume、continuation、崩溃恢复沿用，不借续行换模型。多次物理尝试收束为同一逻辑执行权威，不得并行生效多个 DevOps 权威。

## [023] ProviderRetryAttempt 抑制只持续到本次 attempt 终结

`missing-final-report` 与 `interaction-repair` 对 ProviderRetryAttempt 的抑制，仅在本次 attempt 为 `TurnUnknown` 或 `TurnInProgress` 且无 exact durable terminal 时成立；此时继续等待，不并发 nudge。

稳定 terminal 观测（Completed、Failed、Aborted 或任一 NeedsContinuation）与 exact durable ChatExecution Terminal 各自足以立即解除抑制，无须等待另一侧。gate 未满足时，每个新 ProviderRunIdentity terminal occasion 按 [019] 重新取得提醒资格，同一 occasion 仍幂等。

不得因该 physical 曾接受 ProviderRetryAttempt，或因 continuation kind、Session、LogicalRun、ledger 条目仍存在而永久抑制。
