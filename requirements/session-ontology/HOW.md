# HOW — session-ontology（实现模型与约束；非 normative）

本文件解释实现怎么表达 WHAT，不新增规范。历史与弃权见文末。

## 实现模型

### 类型（`src/Wanxiangshu/Kernel/SessionOwnership.fs`）

```fsharp
type SessionExecutionClass = Work | InternalLeaf

type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId: string
    | StrengthReplica          // Universal ownership only；绝不是 SatelliteKind case

type SessionOwnership =
    | Root
    | Attached of ownerSessionId: SessionId * attachment: AttachmentKind
```

辅助谓词：`SessionExecutionClass.isWork/isInternalLeaf`、
`SessionOwnership.tryOwner/attachmentKind`（Root → None）。

### durable 事实（`src/Wanxiangshu/Execution/Session/Association.fs`）

```fsharp
type SatelliteKind = | Companion          // 唯一案例；Teacher 已 G3 删除

type ManagedSessionKind =
    | WorkSession
    | SatelliteSession of ownerSessionId: SessionId * kind: SatelliteKind

type SessionAssociation =
    { SessionId: SessionId
      Kind: ManagedSessionKind
      BloggerSessionId: SessionId option   // Work 有 Y；Companion 恒 None
      ParentSessionId: SessionId option }
```

`SessionAssociationProjection` 提供双向 O(1) 查询（PERSIST-008）：

- `isCompanion / isSatellite / isWorkSession / tryMainSessionOf / tryOwnerOf / tryBloggerOf`
- `linkSatellite`：一个事实两个方向一次写入；拒绝集合见 WHAT SESSION-ONTOLOGY-008。
- `unlink`：Work 保留记录、失去 Y（Y 的 entry 被 REMOVE 而非 tombstone——Y 不会被模型重提、
  不会被 join，残留会使 `isCompanion` 对已不存在的 session 返回 true）。

### 派生视图（additive，不改 codec）

`SessionOwnershipClassification`：

- `executionClassOf : ManagedSessionKind -> SessionExecutionClass`（WorkSession → Work；
  SatelliteSession _ → InternalLeaf）。
- `classifyLegacy : SessionAssociation -> SessionExecutionClass * SessionOwnership option`
  （WorkSession → (Work, Root)；SatelliteSession(owner, Companion) → (InternalLeaf,
  Attached(owner, Companion))）。
- `tryClassify : SessionId -> Map<SessionId, SessionAssociation> ->
  (SessionExecutionClass * SessionOwnership option) option`。

hints（分类已知但不进 durable record 的路径）：

- `SyncDelegateAssociationHints.dedicatedExecutionClass = Work`；
  `dedicatedOwnership owner role = Attached(owner, delegateRoleToAttachment role)`。
- `StrengthReplicaAssociationHints.executionClass = InternalLeaf`；
  `ownership owner = Attached(owner, StrengthReplica)`；`tryStrengthReplica` 查 process-local
  owner→replica map（非 durable fold）。

### canonical role label（`src/Wanxiangshu/Session/AgentRoleIdentity.fs`）

`roleOfString` 解析 Host wire 名（`fast-coder` → Coder；alias 拒绝）；`roleName` 返回
`ManagedAgentCatalog.roleLabel` 的拼写，DU case 改名不改 durable 字符串。

### 物理扁平（`src/Wanxiangshu/Infrastructure/OpenCode/Host/Sessions.fs`）

`InjectedSessionPort` 持有 `parentChildMap` / `childParents`；`FamilyRootOf` 沿恢复的 journal
parent 找根；`CreateChildSession` 一律把物理 parent 置为 family root（HOST-015）。
`SatelliteRuntime`（`Session/SatelliteRuntime.fs`）从 root + owner 两侧查询 children，
journal 关联优先做 durable keyed lookup。

## 历史与弃权

- **为什么不是 `SatelliteKind` 单轴**：universal.md §13.5——Dedicated Sync* 与 Bookkeeper/
  StrengthReplica 不共享执行能力边界；继续塞单轴会让「长期 hot-knowledge Work」与「短命 leaf」
  共用分类。GARBAGE：旧 `SatelliteKind = { Companion, Teacher }` 的 Teacher 案例。
- **复用 Teacher 调用代数，不复用 Teacher 分类**（universal.md §13.6）：SyncDelegate 的
  `send → await → completion` 协议被保留并归 `delegation`；Teacher 的 leaf/no-Companion 拓扑删除。
- **G3 clean-break**：Student/Teacher（`ce-student-teacher-collapse.md`、`universal.md`）——
  已删领域；absence ratchet（`student-teacher-absence.mjs`，migration-only）已随 CLN-Z 退役，
  `unexpected-kind` 拒绝与角色枚举共同证明（SESSION-ONTOLOGY-014）。
- **Companion 不是永久 ontology**（HANDOFF §11.1）：`companion.md` 的 15 条 COMPANION 中，
  topology（001/002）归本包；frame/squash 归 context-compression；XTrace 归 semantic-trace；
  WorkRecord 归 work-record；prefix 归 prefix-stability。未来 deterministic in-process summarizer
  取代 physical Blogger leaf 时这些 WHAT 均不变。
- **AgentRoleIdentity 双归属**：EVIDENCE.md 同时列于 session-ontology 与 participant-identity。
  本包只取「Host-wire 解析 + canonical label 稳定」（SESSION-ONTOLOGY-013）；Role 身份规则本体
  归 participant-identity（DOES NOT OWN）。
- **host-session-context.test.mjs 的 roleOf 断言**：MOVE 至 host-boundary 包，因其测试对象是
  HostSessionContext（Host 观察适配器）；若 participant-identity 后续要求独占 role 解析断言，
  cutover 时按 SPLIT 计划拆出。
