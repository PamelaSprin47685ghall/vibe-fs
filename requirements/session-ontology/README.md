# session-ontology

> 一句话 WHY：「有一个 session」不等于「出现一个新 participant」。execution topology、ownership 与
> personhood 必须分离，否则 attached work、internal leaf、replica、companion 会制造假的角色与错误能力继承。

## WHAT 概览

Session 的长期身份由**两个正交维度**决定，而不是单一 `SatelliteKind` 轴、也不是角色/工具白名单：

```text
SessionExecutionClass（Work | InternalLeaf）
  ×
SessionOwnership（Root | Attached of ownerSessionId × AttachmentKind）
```

- `Work` = 能发普通 provider request、可有 Companion 的会话；`InternalLeaf` = 叶子（Companion /
  Bookkeeper / StrengthReplica），不递归挂叶。
- `Root` = 主会话；`Attached` = 恰好属于一个 logical owner，物理上仍挂在 family root 下（HOST-015）。
- Dedicated SyncInspector / SyncCoder = `Work + Attached`（可再挂自己的 Companion）；历史
  Student/Teacher 已 G3 clean-break 删除，不存在于当前世界。

## HOW 概览

```text
durable fact     Journal/SessionAssociation.fs   ManagedSessionKind（WorkSession | SatelliteSession(_, Companion)）
derived view     SessionOwnershipClassification  ExecutionClass × Ownership（additive，不改 codec）
hints            SyncDelegateAssociationHints     dedicated Sync* = Work + Attached(SyncInspector|SyncCoder)
                 StrengthReplicaAssociationHints  StrengthReplica = InternalLeaf + Attached(StrengthReplica)
type             Kernel/SessionOwnership.fs       SessionExecutionClass / AttachmentKind / SessionOwnership
label            Session/AgentRoleIdentity.fs     canonical durable role label（ManagedAgentCatalog，非 DU ToString）
```

## proof 概览

- `tests/session-ownership-ratchet.test.mjs`（MOVE）— 钉死 `scripts/checks/session-ownership-ratchet.mjs`：
  AttachmentKind 封闭面 + 8 类 managed session 问卷（owner/reusable/cancel/retire/…）。
- `tests/session-ontology-classification.test.mjs`（NEW）— 正交分类派生视图 + hints + canonical label。
- REUSE：`requirements/session-ontology/tests/sync-delegate.test.mjs`（HOST_008_* 4 锚点）、
  `requirements/session-ontology/tests/session-association.test.mjs`（link/unlink 不变量 17 锚点）、
  `requirements/session-ontology/tests/session-flattening.test.mjs`（HOST-015 物理扁平）、
  `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs`（HOST_014_SatelliteKind_Companion_only）。

## 阅读顺序

1. `WHY.md` — 为什么必须独立存在、什么情况下世界 RED。
2. `WHAT.md` — 唯一 normative 合同（编号命题）。
3. `HOW.md` — 实现模型 + 历史与弃权（含 GARBAGE 裁决）。
4. `PROOF.md` — 每条命题的测试落点 + SPLIT@cutover 清单。
5. `tests/` — 可执行 proof。

## DEPENDS ON

无（INDEX.md 依赖骨架：`session-ontology → 无`）。本包提供 managed-session-lifecycle、
participant-identity、provider-language、delegation、interaction-authority 共用的 session
existence/ownership ontology。
