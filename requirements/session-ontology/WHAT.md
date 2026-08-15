# WHAT — session-ontology（唯一 normative 合同）

命题前缀：`SESSION-ONTOLOGY-`。全部命题描述**当前世界必须同时成立**的事实。
来源：旧 host/companion/execution/agent 条款（HOST-008/015、COMPANION-001/002、EXEC-026/028、
AGENT-001/024，2026-08-14 归档）与历史 why/host §15。落点见 `PROOF.md`。

---

## SESSION-ONTOLOGY-001：Session 分类由 ExecutionClass × Ownership 两个正交维度决定

**规范**：长期 session 所有权由 `SessionExecutionClass`（`Work | InternalLeaf`）与
`SessionOwnership`（`Root | Attached of ownerSessionId × AttachmentKind`）两个正交维度联合决定，
不再以单一 `SatelliteKind` 轴为唯一模型（历史 shape/host 条款 HOST-008）。

**含义/动机**：Dedicated Sync* 是长期 hot-knowledge Work Session（需要 Companion/context），
Bookkeeper / StrengthReplica 是短命叶子，二者不共享执行能力边界；单轴分类必然把两种不同事实揉成一个。

**边界**：本命题只定分类轴本身；具体有哪些 `AttachmentKind`、kind 之间的生命周期机制不归本命题。

**证据**：类型 `src/Wanxiangshu/Kernel/SessionOwnership.fs`；派生视图
`Execution/Session/Association.fs` `SessionOwnershipClassification`；→ PROOF.md `SESSION-ONTOLOGY-001/002`。

## SESSION-ONTOLOGY-002：ExecutionClass 与 Ownership 是正交且穷尽的组合

**规范**：每个 managed session 恰好落在 `Work|InternalLeaf × Root|Attached` 四格之一；
`Attached` 恒携带恰一个 `ownerSessionId` 与一个 `AttachmentKind`。

**含义/动机**：四格穷尽使「这个 session 是什么」可 O(1) 分辨，不需要从 agent 名、工具面、
Logical Run、Authority、Fallback 临时推导（历史 proof/host 条款「正交投影」行）。

**边界**：分类不暗示任何 lifecycle 行为（复用/取消/retire 归 `managed-session-lifecycle`）。

**证据**：`session-ownership-ratchet` gate 问卷 `owner` 字段 + `session-ontology-classification`；
→ PROOF.md `SESSION-ONTOLOGY-002`。

## SESSION-ONTOLOGY-003：Dedicated SyncInspector / SyncCoder = Work + Attached

**规范**：Dedicated SyncInspector / SyncCoder 是 `Work + Attached(SyncInspector|SyncCoder)`，
可拥有自己的 Companion（Work 能力路径）；**不得**实现成历史 Teacher-style InternalLeaf /
no-Companion Satellite（历史 shape/host 条款 HOST-008；历史 shape/execution 条款 EXEC-026）。

**含义/动机**：Dedicated Sync* 吃 prefix/context 复用，长上下文需要 Companion；做成叶子会撞
context 问题。「复用 Teacher 的调用代数，不复用 Teacher 的 Session 分类」（universal.md §13.6）。

**边界**：SyncDelegate 的 batch/serialization/canonical 归 `delegation`；reusable vs dispose-after
生命周期归 `managed-session-lifecycle`。

**证据**：`SyncDelegateAssociationHints.dedicatedExecutionClass = Work`、
`dedicatedOwnership(owner, role)`；→ PROOF.md `SESSION-ONTOLOGY-003`。

## SESSION-ONTOLOGY-004：Companion / Bookkeeper / StrengthReplica = InternalLeaf + Attached

**规范**：Companion / Bookkeeper / StrengthReplica 是 `InternalLeaf + Attached`；InternalLeaf
不持有 Companion、不递归挂叶、不再挂其它 Attached（HOST-008；STRENGTH-004/014）。

**含义/动机**：叶子无执行能力路径，防止「叶子的叶子」把 Host 树变深或把内部拓扑伪装成业务层。

**边界**：StrengthReplica 的 decision-local 语义与 retire 时机归 `speculative-investigation`
（STRENGTH-004/014）；本命题只定分类 cell。

**证据**：`StrengthReplicaAssociationHints.executionClass = InternalLeaf`；`tryClassify` 对
Companion 给出 `InternalLeaf + Attached(owner, Companion)`；→ PROOF.md `SESSION-ONTOLOGY-004/001`。

## SESSION-ONTOLOGY-005：Attached 恰好一个 logical owner；Attached SessionId ≠ owner

**规范**：每个 Attached session 恰好属于一个 `ownerSessionId`；`Attached` 的 session id 不得等于
owner（自链被拒）。

**含义/动机**：逻辑归属是 durable 事实；「谁是我的 owner」只能有一个答案，否则级联取消/恢复分叉。

**证据**：`SessionAssociationProjection.linkSatellite` 拒绝 `SelfLink`；`tryOwnerOf` 单向回答；
→ PROOF.md `SESSION-ONTOLOGY-005`（REUSE `context/session-association.test.mjs`）。
## SESSION-ONTOLOGY-006：物理 Host parent 恒为 family root；逻辑归属只由 journal 承载

**规范**：任何 managed child（fork child、one-shot child、Companion Blogger、SyncInspector/SyncCoder、
Bookkeeper、StrengthReplica）的 Host 物理 parent 恒为 family root；儿子再建儿子时物理重挂 root，
Host 树深度恒为 2。归属关系不由物理 parentID 承载，只由 durable journal 事实
（`HandleLinked` / `CompanionBloggerLinked` / SyncDelegate 关联 / StrengthReplica attachment）
与 `SessionOwnership` 证明（HOST-015）。

**含义/动机**：UI 只渲染两层树；孙子在界面不可见等于脱管 Session。物理位置是恢复提示，不是归属证据。

**边界**：恢复时如何匹配/复用/新建（id+agent+title 精确匹配、冲突 fail closed）归
`managed-session-lifecycle`（HOST-015 restore matching）；本命题只定「物理 ≠ 逻辑」事实。

**证据**：`InjectedSessionPort.FamilyRootOf` / `childParents`（`Infrastructure/OpenCode/Host/Sessions.fs`）；
`SatelliteRuntime` 从 family root 查询 children；→ PROOF.md `SESSION-ONTOLOGY-006`。

## SESSION-ONTOLOGY-007：durable SessionAssociation 是事实，正交分类是派生视图

**规范**：durable `SessionAssociation`（FactCodec）仍以 `ManagedSessionKind`
（`WorkSession | SatelliteSession(_, Companion)`）记录；`ExecutionClass × Ownership` 是
`SessionOwnershipClassification` 派生视图，**additive only**——不改 `SessionAssociation` 字段与
FactCodec（`Execution/Session/Association.fs` 头注释）。

**含义/动机**：既有 journal 不必迁移；新视图不破坏 codec 稳定性。`SatelliteKind` 仅 `Companion`。

**边界**：`ManagedSessionKind` 案例本身是 durable codec 形状（HOW），不是长期 ontology 的替代。

**证据**：`SessionOwnershipClassification_executionClassOf/classifyLegacy/tryClassify`；
→ PROOF.md `SESSION-ONTOLOGY-007`。

## SESSION-ONTOLOGY-008：关联写不变量（link 拒绝集合）

**规范**：`linkSatellite` 拒绝：自链（`SelfLink`）、给 Companion 再挂 Companion
（`CompanionWouldRecurse`）、给已有 Y 的 Work 换 Y（`AlreadyLinkedToOther`）、Y 已被别的 Work 占用
（`CompanionClaimedByOther`）、同 child 已以不同 kind 注册（`SatelliteKindConflict`）。
同一对 (X, Y) 重链幂等（restart recovery 依赖）。

**含义/动机**：一个事实两个方向（X→Y 与 Y→X）一次写入，`isCompanion` 与 `tryBloggerOf` 都从同一
map O(1) 回答，无第二索引可分歧（PERSIST-008）。

**证据**：`SessionAssociationProjection.linkSatellite/unlink`；
→ PROOF.md `SESSION-ONTOLOGY-008`（REUSE `context/session-association.test.mjs` 17 锚点）。

## SESSION-ONTOLOGY-009：Work + Root 恰有一个 Companion；Work + Attached Sync* 的 Companion 可选

**规范**：`Work + Root`（普通主会话）恰好一个 Companion/BloggerSessionId 且 ≠ owner；
`Work + Attached Sync*` 的 Companion 可选，若有则 ≠ owner、≠ 该 Sync* 自身。

**含义/动机**：lazy-creation 规则防止第二个 Y；「无记录」只意味着 Y 尚未创建（下一次 transform
解决），绝不意味着「这是一个没听过名字的 Companion」。

**边界**：Y 的 frame/squash/投影语义归 `context-compression`/`semantic-trace`/`work-record`。

**证据**：`linkSatellite` 双向写入 + `tryBloggerOf`；→ PROOF.md `SESSION-ONTOLOGY-009`。

## SESSION-ONTOLOGY-010：runtime topology 不决定分类

**规范**：分类由 `ExecutionClass × Ownership` 决定，不由 Role / Tier / 工具面 / Logical Run /
Authority / Fallback 临时决定（HOST-008 不变量）；「有没有 Companion」不由角色资格白名单决定
（COMPANION-001）。

**含义/动机**：把拓扑绑到身份会让换执行者 = 换人、换 Peer = 换世界（历史 why/host §21）。

**证据**：`SessionAssociationProjection.isCompanion` 只读 durable kind，无任何 role 参数；
→ PROOF.md `SESSION-ONTOLOGY-010`。

## SESSION-ONTOLOGY-011：StrengthReplica 不是 SatelliteKind；owner 至多一个 active；不跨 decision 复用

**规范**：StrengthReplica 是 Universal `InternalLeaf + Attached(_, StrengthReplica)`，**不是**
`SatelliteKind` 案例、不在 FactCodec 上 durable（process-local owner→replica 索引）；
每个 owner 至多一个 active attachment；完成即 retire，不跨 Strength decision 复用 transcript。

**含义/动机**：decision-local 叶子不该进 durable association / Satellite kind，避免被当作长期成员
恢复；跨 decision 复用 transcript 会把上个猜测的历史漏进下个猜测。

**证据**：`StrengthReplicaAssociationHints`（`executionClass = InternalLeaf`、
`isStrengthReplicaAttachment`、`tryStrengthReplica`）；→ PROOF.md `SESSION-ONTOLOGY-011`。

## SESSION-ONTOLOGY-012：Bookkeeper 绑定具体 transactionId，不与 Companion / Sync* 混用

**规范**：`AttachmentKind.Bookkeeper of transactionId` 携带具体 transactionId；Bookkeeper 不得与
Companion / Sync* 身份混用（HOST-008 不变量）。

**含义/动机**：Bookkeeper 是 fetch 的 ephemeral leaf；txId 使同一 transaction 的恢复/审计可定位。

**证据**：`AttachmentKind.Bookkeeper` 构造携带 payload；→ PROOF.md `SESSION-ONTOLOGY-012`
（REUSE `kernel/sync-delegate.test.mjs` `HOST_008_AttachmentKind_bookkeeper_carries_transaction_id`）。

## SESSION-ONTOLOGY-013：canonical durable role label 稳定（不随 DU 改名漂移）

**规范**：持久化事实中的角色标签来自 `AgentRoleIdentity.roleName`（委托
`ManagedAgentCatalog.roleLabel`），不是 `Role.ToString()` 的小写化；DU case 改名不得静默改变
durable 字符串，否则旧 journal 全部解码失败。

**含义/动机**：DU 名拼写是编译器产物；把 `ToString()` 写进 durable 事实等于把编译器内部命名
固化成协议（`Session/AgentRoleIdentity.fs` 注释）。

**边界**：Role 的身份规则本体归 `participant-identity`；本命题只拥有「Host-wire 解析 + canonical
label 稳定性」这一可观察面。

**证据**：`roleName(Role.Manager) = 'manager'` 等；→ PROOF.md `SESSION-ONTOLOGY-013`。

## SESSION-ONTOLOGY-014：Student/Teacher 不存在（G3 absence）

**规范**：`Role.Student` / `Role.Teacher`、Student↔Teacher 绑定、HOST-014 canary 与
`TeacherSessionId` 投影 **absent**（G3 clean-break）；不得写成 pending / 仍存在的过渡路径，
不得以 alias / deprecated type / 隐藏 storage / SyncDelegate fallthrough 复活
（历史 shape/host HOST-008 G3；历史 what/host HOST-014 空号）。

**含义/动机**：absence ratchet 防止旧领域借兼容层复活；`SatelliteKind` 只允许 `Companion`。

**证据**：`session-ownership-ratchet` gate 拒绝 `kinds.Teacher`
（`session_ownership_matrix_rejects_unexpected_kind`）；`SatelliteKind` 单案例；
→ PROOF.md `SESSION-ONTOLOGY-014`。

## GARBAGE / 弃权（不进入 WHAT）

- `HOST-014` Student/Teacher Host 行为、QA bootstrap、`teacher` 双 await、Learn/Compile nudge
  全条 → GARBAGE（migration absence ratchet；`student-teacher-absence.mjs` 已随 CLN-Z 退役）。
- `AGENT-002` exact catalog（22 agent 名等）→ GARBAGE（machine vocabulary）。
- `AGENT-020/021/022` Student absence 细节 → GARBAGE（与 SESSION-ONTOLOGY-014 同源，收敛到 absence）。
- 历史 `SatelliteKind = { Companion, Teacher }` 中的 Teacher 案例 → GARBAGE（G3 删除）。
- 历史 change（ce-student-teacher-collapse）的 CE/registry 细节 → HOW/GARBAGE
  （实施记录 + 已删领域；其 durable WHY 已由 session-ontology / delegation 拥有）。

