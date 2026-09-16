# interaction-authority — HOW

## 架构机制与权限状态机

`interaction-authority` 通过纯函数式事实折叠维护唯一的权威状态投影：

1. **Ingress 授权把关**：
   `PromptIngress.handle` 是外部物理消息升级为权限实体的唯一入口。仅当当前无活跃 Profile 且消息显式指定合法 participant 时，Ingress 才向 `participant-identity` owner 请求为 exact root 准备 typed `ParticipantIdentityEvidence`。Authority 校验 exact keys/owner witness 后只执行一次 durable append：`AuthorityRootAccepted { root keys; ExpectedClosureKind; ParticipantIdentityEvidence; initial per-physical target/lease }`。该单一 fact 同时安装 identity 与接受 root；append 未提交时不发布 identity、root 或 profile，不存在可孤立的 identity write。活跃 run、缺失 evidence 或 evidence/run 不匹配一律 `UnknownOrigin`。显式 external agent 只作为与 participant 比对的输入，绝不独立进入 authority。

2. **来源判定管线（Resolution Pipeline）**：
   按固定顺序扫描 durable authority facts：
   - 物理确认接收的消息（`AcceptedContinuationIds`）→ 对应 Continuation 与原 identity evidence
   - 挂起的 agent-free PromptKey Claim → 已登记的意图来源
   - Host 压缩/合成提示 → HostInternal
   - 已注册 `AgentOwnerRoot` + exact typed owner-derived identity evidence → child/attached/InternalLeaf Root
   - 证明合法的物理用户输入 + identity owner 返回的 fresh evidence → HumanRoot
   - 未命中任何规则 → fail-closed `UnknownOrigin`

   Session cache、Host physical parent 与消息字段形态不进入 resolution；显式 external agent 只作为与 participant 比对的输入。
   
   `HostInternal` 的 synthetic 判定只覆盖**整条**由 Host 合成的消息（每个 part 都标 `synthetic:true`）。用户在 TUI 中提及文件时，Host 注入的 synthetic 回显 part 与真实用户 part 混存于同一物理消息；混存消息不是 HostInternal，进入正常来源判定并执行受管 admission/model 投影，用户 TUI 模型选择被覆盖。

3. **权威事实折叠（Authority Fold）**：
   identity 与 authority 投影严格从同一 `AuthorityRootAccepted` 重放 `(SessionId, LogicalRunId, AuthorityRootId, ExpectedClosureKind, ParticipantIdentityEvidence, per-physical target/lease)`，内存不维护独立可变 authority/identity 副本。它向 Host 与 execution 发布 exact profile view，而不复制身份解析规则；固定 participant/Role 来自 evidence，当前 per-physical target/lease 来自 execution binding。

4. **Durable closure interpreter**：
   HumanRoot Manager 由 `Composition/Durable/Fold.fs` 在 `RetirementCommitted.QualityCandidateAccepted=true` 时原子关闭 authority；需要后继的退休保留 active LogicalRun、claims 与 continuation mappings。`HostSessionNudge` 只读取 active profile，正式 managed chat admission 再校验同一 exact identity，不能回退历史 profile。

   其他 lifecycle 在 acceptance 时写定唯一 `ExpectedClosureKind`。terminal interpreter 只接受与该 kind 及 exact root keys 匹配的 typed durable outcome：其他 HumanRoot `ManagedLogicalRunTerminal`、AgentOwner child Work `ChildLogicalRunTerminal`、AgentOwner attached Work `AttachedLogicalRunTerminal`、AgentOwner InternalLeaf `InternalLeafTerminal`；每个 terminal 穷尽其 lifecycle 的 Completed/Cancelled/Failed 合法结果。它幂等追加唯一 `AuthorityLogicalRunClosed`；只有该 append 确认后的 fold 才清空 active run/claims、释放 identity binding并归档 profile。terminal-source durable 而 closure 未确认时，reconciliation 重放 source 并重试同一 append；association removal、cancel request、idle/timeout、wall clock 与 Host observation 不进入 closure decision。同一 SessionId 的 fresh root 必须先观察 exact closure，不能读取归档 identity。

5. **Gate nudge 因果分层**：
    普通 nudge 的 durable occasion key 为 `gate kind + exact ProviderRunIdentity`（Manager idle 额外携带 Life/condition，Reviewer guard 额外携带 barrier）。是否“已经 admission”只由该 exact payload 的 Pending claim 或 AcceptedDispatch 证明；`ClaimSequence` 仅用于为重试生成新的 PromptKey，不能把已 Abandoned 的明确未发送尝试永久算作提醒完成。Repair owner 根据 typed provenance 与 `TurnUnknown | TurnInProgress | TurnNeedsContinuation | terminal` 分类决定：首次缺陷→发送一次；nudge 飞行态→等待；nudge 自身形成 fresh invalid terminal→重新提醒；普通旧 turn 的重复观察→幂等吸收。只有 Blogger nudge→AABB 这类显式升级协议保留独立的有界 repair state machine，不得把它的预算语义泛化到普通 gate nudge。
