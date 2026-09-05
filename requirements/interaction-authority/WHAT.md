# interaction-authority — WHAT

## INTERACTION-AUTHORITY-001: 物理用户消息不等于 authority turn

物理 `role=user` 消息仅是传输层形态。物理消息标识符必须经由唯一的显式提升通道在物理接收已确立（`PhysicalAccepted`）后方可升级为 `AuthorityRoot`。不存在从传输收据直接转换为 AuthorityRoot 的通道。

## INTERACTION-AUTHORITY-002: 形态不是 authority 证据

零宽字符、空白排版、固定模板、时间戳、文本长度或合成配置中的注释/字段形态均不能作为 Authority 身份证明。消息的权威性仅能由系统内建的 typed 来源机制判定。

## INTERACTION-AUTHORITY-003: Root 独占权

`AuthorityRoot` 具有独占权限：
1. 创建新的 Logical Run；
2. 提交显式 managed-agent 选择，并绑定 `participant-identity` owner 为该 exact run 准备的版本化 `ParticipantIdentityEvidence`；
3. 成为新的 Fallback 根节点；
4. 重置 Interaction Repair 预算；
5. 成为后续 execution binding 的延续基准。

`AuthorityRootAccepted { SessionId; LogicalRunId; AuthorityRootId; RootKind; ParticipantIdentityEvidence; initial execution selection }` 是 root acceptance 与 identity installation 的唯一 durable fact payload，必须以一次原子 append 接受或拒绝。Authority 不得先持久化 identity 再接受 root，也不得从 agent 名称推导或拥有 canonical SelectedAgent、Role、Persona 或 provenance/version；它只校验 evidence 的 exact key/owner witness 并保管 payload。append 成功后的同一 fold 原子建立新 active root、绑定 exact evidence、清空已关闭 prior run 的 claims/continuation 映射/序列号并重置 Fallback 游标；append 未提交则两者均不存在。

## INTERACTION-AUTHORITY-004: Continuation 禁区

所有类型的 Continuation 仅用于延续已存在的 Logical Run，绝对禁止执行 Root 独占操作：不得新建 RunId、不得替换或字段级修改 `ParticipantIdentityEvidence`、不得更新底层 AuthorityProfile、不得重置 Fallback 或 repair 预算。Continuation 必须完整继承宿主 Run、Root 标识与 exact identity evidence；必要的 EffectiveAgent 变化只属于 execution binding。

## INTERACTION-AUTHORITY-005: 四类 provenance 与两种 Root

系统严格区分四类来源形式：`AuthorityRoot`（包含 `HumanRoot` 与 `AgentOwnerRoot`）、`Continuation`、`HostInternal` 与 `UnknownOrigin`。该分类为闭集合；`AgentOwnerRoot` 必须携带 participant-identity owner 为 exact child/attached/InternalLeaf run 准备的 typed owner-derived identity evidence，且 OwnerLogicalRunId、LogicalRunId 与 root key 必须精确匹配。任何 Continuation 均不可被解析为 Root，反之亦然；缺失、wrong-owner 或 wrong-run evidence 一律归入 `UnknownOrigin`。

## INTERACTION-AUTHORITY-006: HumanRoot 必须显式命名 managed agent

`HumanRoot` 必须显式指定合法的 managed agent 本名。该名称只是交给 participant-identity owner 的 root identity 请求，不是 Authority 自行推导 Persona/Role 的依据。省略名称、使用 legacy 名称、连字符/大小写变形、格式错误或缺少 owner 返回的版本化 identity evidence 必须 fail-closed，禁止静默猜测或从 Session cache 补全。

## INTERACTION-AUTHORITY-007: UnknownOrigin fail-closed

`UnknownOrigin` 绝对禁止更新执行 Profile、启用 Fallback 或发起任何 Continuation。无法证明来源合法性的请求必须立即阻断。

## INTERACTION-AUTHORITY-008: 来源解析优先级

消息来源按固定优先级严格判定：已确认的 Host 消息 > 已 Claim 的 PromptKey > Host 内部 Compaction/Synthetic > 已注册的 AgentOwnerRoot > 外部证明合法的 HumanRoot > UnknownOrigin。优先级顺序本身构成安全边界，避免真实业务消息被内部机制降级或冒充。

## INTERACTION-AUTHORITY-009: 纯函数永不推断 HumanRoot

来源判定中的纯计算函数绝不推断返回新的 `HumanRoot`。`HumanRoot` 只能在激活 Profile 缺席且携带合法显式 agent 时由 Ingress 边界授予；活跃 Run 中携带同一合法 agent 的外部用户消息只能成为绑定既有 Profile 的 `HumanMessage` continuation，缺失或漂移 agent 的未知消息必须拒绝，绝不可抬升为 Root。continuation 接纳后，Host 当前物理 user-message binding 必须推进到该消息，供 reconciler/provider-start 观察 exact 新 execution；既有 Authority Root identity 不变。

## INTERACTION-AUTHORITY-010: 自动 continuation 稳定 occasion identity 与精确 admission

自动合成的 repair、nudge、review 提示与重试消息绝不可借机抬升权限。普通 gate nudge 的持久化幂等范围必须绑定 exact terminal occasion；同一 `(SessionId, LogicalRunId, continuation kind, gate kind, ProviderRunIdentity)` 若存在 Pending claim 或 PhysicalAccepted dispatch，则 duplicate observation 被幂等吸收；若 claim 已因明确的 pre-acceptance `SendFailed` Abandoned，则该 occasion 重新可 admission，历史 ClaimSequence 不得冒充“已经提醒”。新的 ProviderRun 是新的 reminder occasion，只要业务 gate 仍未满足就必须重新具备提醒资格。需要精确 `PhysicalUserMessageId` 的调用方在 Host 只先返回 transport receipt 时，必须等待该 PromptKey 的 durable `PhysicalAccepted` 因果事实；pending acceptance 既不得伪装成功，也不得被误报为 send failure。只有 Blogger nudge→AABB 等明确写入规范的升级协议可以拥有有限预算。任何 duplicate admission 都属于 typed 幂等状态而非 transport/protocol failure。

transport receipt 只是物理进度（Submitted），不是意图身份：chat.message 的来源判定可能抢在 receipt 落盘前冻结，后续准入比较必须忽略 receipt，只比较不可变 claim 身份，否则后继会被误判为意图变更。

## INTERACTION-AUTHORITY-011: authority 是原子 profile 内的稳定子记录

每次执行的 `AttemptExecutionProfile` 必须原子携带 exact SessionId、LogicalRunId、AuthorityRootId、当前 `ExecutionBinding` selection，以及 `AuthorityRootAccepted` 中 participant-identity owner 准备的完整版本化 `ParticipantIdentityEvidence`。Authority fold 向 Host/execution 消费者逐字段精确暴露 stable SelectedAgent、Role、稳定 Persona 与 provenance/version，但不拥有、重新解析或修改这些字段；当前 EffectiveAgent/provider/model/lease 只来自 execution binding。禁止从 Session cache、物理 parent、agent 名称或分散消息拼装 profile。

## INTERACTION-AUTHORITY-012: degeneration-guard 是 continuation 而非 fallback 失败

degeneration-guard 自恢复消息（`DegenerationGuard`）等属于强类型 Continuation。它们延续当前 LogicalRun，复用既有 Root 与 Profile，不得建立新 Root、不得重置 Fallback 游标、亦不得计入模型重试失败次数。`DegenerationGuard` 不得伪装成 `ProviderRetryAttempt`。

## INTERACTION-AUTHORITY-013: 显式 continuation 绑定保持 authority continuity

同一 LogicalRun 下的强类型 continuation 推进属于权限连续演进：仅 execution binding 的 EffectiveAgent 可按规则变化；Root、identity evidence、Profile 关联与游标位置全部保持不变。SessionId 相同但 LogicalRun 不同不构成 continuity。

## INTERACTION-AUTHORITY-014: Nudge 与 JoinGuard 是 Continuation

JoinGuard、闲置 Nudge 等流转控制指令均为 Continuation，不产生新的 Authority。在存在未决后台任务时仅允许发送 JoinGuard 延续等待，禁止隐式创建新 Root。Nudge 是 gate reminder 而不是一次性预算：只要对应业务 gate 仍未满足，每个新的合法 terminal occasion 都必须重新获得提醒资格；幂等范围只允许收窄到同一 exact `ProviderRunIdentity` occasion，禁止用 Session、LogicalRun、Life 或 barrier 本身永久压掉后续 fresh terminal。

## INTERACTION-AUTHORITY-015: external-user ingress 不授予 authority

处于运行中途的外部用户消息仅作为低权限唤醒信号打断等待，不取消当前运行时，不直接赋予 Prompt authority，亦不重置 LogicalRun 或新建生命周期。

## INTERACTION-AUTHORITY-016: Root claim 不进入 continuation 映射

接受 `AgentOwnerRoot` 的 claim 不会将消息写入 Continuation 查找映射。曾经作为 Root 的物理消息不能作为后续判定 Continuation 的依据。

## INTERACTION-AUTHORITY-017: continuation 只能接续 active run

Continuation 只能挂靠当前活跃的 `ActiveLogicalRun`，绝对禁止回退挂靠已归档或结束的历史 Profile。

## INTERACTION-AUTHORITY-018: 每种 AuthorityRoot lifecycle 都收敛为 exact durable closure

每个 `AuthorityRootAccepted` 必须原子记录唯一 `ExpectedClosureKind`。闭集合为 `HumanRootManagerLife`、`HumanRootManagedRun`、`AgentOwnerChildWork`、`AgentOwnerAttachedWork`、`AgentOwnerInternalLeaf`；前两种按 HumanRoot 的实际 lifecycle 穷尽，InternalLeaf 无论物理 Ownership 为 Root 或 Attached 都只能使用最后一种。各 kind 唯一合法 source witness 分别是 exact Manager `LifeCompleted(LifeId, FactId)`、`ManagedLogicalRunTerminal(LogicalRunId, FactId)`、`ChildLogicalRunTerminal(OwnerLogicalRunId, ChildLogicalRunId, FactId)`、`AttachedLogicalRunTerminal(OwnerLogicalRunId, ChildLogicalRunId, AttachmentKind, AssociationGeneration, FactId)`、`InternalLeafTerminal(OwnerLogicalRunId, LeafLogicalRunId, DecisionOrTransactionId, FactId)`。每个 `*Terminal` 是该 lifecycle 对 `Completed | Cancelled | Failed` 合法结果的 durable closed outcome，不包含 request、signal 或 observation。任一 HumanRoot 或 AgentOwnerRoot 缺少或无法唯一归入该闭集合时不得被接受。

Authority 的 durable terminal interpreter 校验 source witness 与 accepted root 的 kind、owner、SessionId、LogicalRunId、AuthorityRootId 全部精确匹配后，幂等追加唯一 `AuthorityLogicalRunClosed { SessionId; LogicalRunId; AuthorityRootId; RootKind; ClosureWitness }`。append 成功后的同一 fold 清空对应 `ActiveLogicalRun` 与 run-scoped mappings，释放 active identity-evidence binding并归档历史 Profile；重复相同 closure 幂等，冲突 closure fail-closed。source terminal 已 durable 但 closure append 尚未确认时，reconciliation 只能重放该 typed source 并重试相同 closure append；不得释放 binding 或允许 SessionId 复用。lifecycle terminal 本身、association removal、cancel request、idle/timeout、wall clock、Host observation 或旧 Profile 均不得推断 closure。

## INTERACTION-AUTHORITY-019: gate nudge admission、飞行态与 fresh-terminal re-arm 必须分型

`InteractionRepair` 的 claim/Submitted/PhysicalAccepted 只建立一次 gate-nudge attempt 的 admission/物理落地证据，不建立 gate completion，更不建立“提醒预算耗尽”。当前 nudge attempt 仍为 `finish=None`、`tool-calls` 或其它明确 in-progress 观测时，重复 idle/reconcile 必须保持等待，禁止并发发送第二次 nudge。若该 attempt 自身到达新的稳定但仍不满足 gate 的 terminal（例如空/XML-only `stop` 或 `length`），该 fresh `ProviderRunIdentity` 必须重新获得一次 nudge 资格；同一 terminal 的重复观测仍严格幂等。普通旧 turn 在后续 nudge 已 admitted 后再次被观察，只能幂等吸收，不能替 fresh terminal 消耗提醒资格。普通 gate nudge 不发布 `INTERACTION_REPAIR_EXHAUSTED`。

## INTERACTION-AUTHORITY-020: repair fatal绑定exact claim settlement与注入fuse

只有typed repair invariant incident可以请求fatal；当前PromptKey claim、Submitted/PhysicalAccepted与fresh terminal判定必须先形成exact settlement evidence。InteractionRepair不得直接引用fatal physical adapter、optional/default/global fallback；composition注入mandatory capability。同一incident只允许一次report与kill，普通exhaustion或可恢复send failure不得升级为fatal。
