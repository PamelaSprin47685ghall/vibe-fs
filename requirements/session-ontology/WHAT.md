# session-ontology — WHAT

## SESSION-ONTOLOGY-001: Session 分类由 ExecutionClass 与 Ownership 正交决定

物理 managed session 的本体分类由 `SessionExecutionClass`（`Work | InternalLeaf`）与 `SessionOwnership`（`Root | Attached`）两个正交维度联合决定。该分类描述容器能力与 durable association，不描述 logical participant run 或 `ParticipantIdentity`。

## SESSION-ONTOLOGY-002: ExecutionClass 与 Ownership 为穷尽正交组合

每个 managed session 必须且仅能落在 `Work | InternalLeaf` 与 `Root | Attached` 的四格穷尽组合之一；`Attached` 状态恒携带且仅携带一个 `ownerSessionId` 与一个 `AttachmentKind`。

## SESSION-ONTOLOGY-003: Dedicated SyncInspector 与 SyncCoder 属于 Work + Attached

Dedicated SyncInspector 与 SyncCoder 属于 `Work + Attached`，具备完整的执行能力路径与上下文支持，可拥有独立的 Companion，不得实现为无 Companion 的 InternalLeaf。

## SESSION-ONTOLOGY-004: Companion / Bookkeeper / StrengthReplica 属于 InternalLeaf + Attached

Companion、Bookkeeper 与 StrengthReplica 属于 `InternalLeaf + Attached`；InternalLeaf 节点不得持有 Companion、不得递归附挂子叶节点，亦不得挂载其他 Attached 实体。

## SESSION-ONTOLOGY-005: Attached 节点单一 Owner 且禁止自链

每个 Attached session 严格归属于单一 `ownerSessionId`；Attached session 的 ID 绝对不得等于其 owner ID，任何自链接操作均直接拒绝。

## SESSION-ONTOLOGY-006: 物理 Host Parent 恒为 Family Root 且逻辑归属由 Journal 承载

所有 managed child 在 Host 物理层均挂在 family root 下（物理树深度恒为 2）；层级归属完全由持久化 journal association 承载。物理 parentID 既不得推断 logical ownership，也不得作为 child/attached/InternalLeaf 的 Role、Persona、Peer、identity provenance 或 owner-derived identity evidence。

## SESSION-ONTOLOGY-007: Durable 关联事实与正交分类派生视图解耦

持久化 `SessionAssociation` 是关联关系的最小事实编码；`ExecutionClass × Ownership` 为纯派生视图（`SessionOwnershipClassification`）。派生逻辑只读且由 durable facts 决定，不得反向改写 association 或另建身份状态。

## SESSION-ONTOLOGY-008: 关联写操作不变量与原子拒绝集

关联写入操作严格原子校验并拒绝以下情形：自链（SelfLink）、给 Companion 递归附挂 Companion、替换已有有效 Companion 链接、冲突占用已被其他 Work 占用的 Companion，以及同 child 以冲突 kind 注册。同一对 (owner, child) 重复链接保证幂等。

## SESSION-ONTOLOGY-009: Work Root 唯一 Companion 规则

`Work + Root` 主会话至多且恰有一个 Companion（且 ID ≠ owner）；`Work + Attached` 的 Companion 为可选；未记录仅代表未延迟初始化，绝不表示匿名或未绑定状态。

## SESSION-ONTOLOGY-010: Runtime 拓扑不决定业务分类与角色

Session 的物理本体分类严格仅由 `ExecutionClass × Ownership` 决定，不得受 Role、Tier、Persona、工具暴露面或 Logical Run 影响；Companion 资格不设角色白名单。反向亦然：classification、association、Session cache 与 Host parent 均不得生成或修改 `ParticipantIdentity`。

## SESSION-ONTOLOGY-011: StrengthReplica 为 Universal 内部叶子且不跨决策复用

StrengthReplica 属于进程内 `InternalLeaf + Attached`，不属于持久化 satellite kind；每个 owner 至多存在一个 active 副本，决策完成即销毁，禁止跨决策复用 transcript。

## SESSION-ONTOLOGY-012: Bookkeeper 绑定具体 TransactionId

Bookkeeper attachment 必须显式携带目标 transactionId，专用于临时取数审计，禁止与 Companion 或 Sync* 身份混用。

## SESSION-ONTOLOGY-013: Canonical Durable Role Label 稳定性

持久化事实中的角色标签必须由规范的 role catalog 唯一确定，不得随内部类型枚举或代码重命名而漂移。

## SESSION-ONTOLOGY-014: Student 与 Teacher 角色及拓扑彻底消除

系统内不存在 Student / Teacher 角色及对应绑定机制；任何解析、映射及运行时均严格拒绝旧式 Teacher 拓扑与未预期 kind。

## SESSION-ONTOLOGY-015: SessionId 是可复用物理容器，不是 identity scope

`SessionId` 只命名物理容器；logical participant run identity 由 exact run 与 `AuthorityRootAccepted` 内 participant-identity owner 的版本化 evidence 命名。同一 SessionId 只有在 `interaction-authority` 已为 exact `(SessionId, LogicalRunId, AuthorityRootId)` 持久化 `AuthorityLogicalRunClosed` 并释放 active identity binding 后，才可承载 fresh root 与不同 identity。Session ontology 只发布容器分类与 durable association；它不缓存或解析 identity，不发布 run closure，也不把 association removal、detach/attach、classification、idle/timeout 或 Host 观察冒充 lifecycle terminal 或 closure。
