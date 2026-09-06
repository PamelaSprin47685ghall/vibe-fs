# relay-incumbency — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：Road/Incumbency/phase/port vocabulary；`RetirementOutcome = Continue | Accepted of QualityCertificateId`、`ProjectionCut = { ProviderRunId; ToolCallId }`、`RetirementSummary = { Id; IncumbencyId; SnapshotId; AuthorityRevision; ProjectionCut; Outcome }`（快照字段类型仍为 WorkspaceSnapshotId，快照与修订是 load-bearing binding）；`AssessmentBinding` 携带 IncumbencyId；`ManagerLoopGate.gateKind` 前缀 `manager-loop:`；保留 `QualityCertificate`、`RetiredProviderRunIds` 与内部 phase 名。
- `src/Wanxiangshu/Mission/Relay/Facts.fs(.fsi)`：唯一 ID/事件合成点。`IncumbencyOpening = { RoadId; IncumbencyId; AuthorityRevision; Transaction }`；`IncumbencyOpening.initial(SessionId, PhysicalUserMessageId, WorkspaceSnapshotId)` 产出 RoadOpened + IncumbencyOpened；`IncumbencyOpening.next(RoadId, RetirementId, AuthorityRevision, WorkspaceSnapshotId)` 只产出 IncumbencyOpened；调用方不再自行合成这些 ID 与事件。按需引用 HostDigest。
- `src/Wanxiangshu/Mission/Relay/Fold.fs(.fsi)`：纯 fold，拒绝双 active、retired resurrection、第二 assessment、跨迭代重放、stale retirement 与 cut 前 opening；精确 replay 须 identity/binding/snapshot/authority/scores 全一致；CleanupBlocked 的 perfect 清障后可重试 Accepted；`Decision.openIncumbency(state, road, incumbent, snapshot, authority)`；`IncumbencyOpened of IncumbencyId * WorkspaceSnapshotId`；`RetirementCommitted` 携带闭合 Outcome。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：任期观测与退出 nudge；正常 terminal 经 manager guard gate 发送 phase 对应的退出文档，gate admission 是唯一的 durable 去重。
- `src/Wanxiangshu/Composition/Durable/Fold.fs`：Continue 退休保留 active LogicalRun 以待下一迭代，Accepted 退休关闭当前迭代的 HumanRoot Manager authority（证书失效后允许普通新迭代）。
- `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs`：Continue 退休且尚无 active 迭代时中断旧 attempt 并经正式 manager-loop gate 自动派发下一迭代；自动中断/派发仅 Continue 专属，Accepted 永不由 Narrative 自动派发（证书失效后的普通新迭代由 Change ContinueLoop 派发，同样使用 LatestRetirement cut）。
- `src/Wanxiangshu/Change/...`：`ContinueLoop : ManagerJobId -> Task<Result<IncumbencyId,string>>`；`ManagerLoopSignal`（Candidate/Continue/ExceptionalTerminal）由匹配 RetirementOutcome 派生。
- `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs`：正式 managed chat admission 决定物理接纳，继承同一 LogicalRun、AuthorityRoot 与 identity evidence。

## 依赖关系

DEPENDS ON:
- `participant-identity`
- `durable-events`
- `interaction-authority`

## 验证

| 命题 | executable proof |
|---|---|
| RELAY-001 | `requirements/relay-incumbency/tests/loop.test.mjs::WHAT[RELAY-001] one open road admits at most one active iteration` |
| RELAY-002 | `requirements/relay-incumbency/tests/loop.test.mjs::WHAT[RELAY-002] every iteration opens on the same AuditPending algebra` |
| RELAY-003 | `requirements/relay-incumbency/tests/loop.test.mjs::WHAT[RELAY-003] iteration starts empowered with no work before review` |
| RELAY-004 | `requirements/relay-incumbency/tests/loop.test.mjs::WHAT[RELAY-004] low-score assessor takes work ownership in place without a new iteration` |
| RELAY-005 | `requirements/relay-incumbency/tests/continuation.test.mjs::WHAT[RELAY-005] retired iteration never reactivates and stale runs stay absorbed` |
| RELAY-006 | `requirements/relay-incumbency/tests/continuation.test.mjs::WHAT[RELAY-006] Continue keeps the road open for a next iteration`；`requirements/relay-incumbency/tests/continuation.test.mjs::WHAT[RELAY-006] Accepted blocks reopening while valid, invalidation reopens it` |

| RELAY-008 | `requirements/relay-incumbency/tests/authority-revision.test.mjs::WHAT[RELAY-008] authority update invalidates a perfect certificate without restoring work ownership`；`requirements/relay-assessment/tests/certificate.test.mjs::WHAT[RELAY-008] certificate invalidation is explicit and never reactivates its assessor` |
| RELAY-009 | `requirements/relay-incumbency/tests/authority-revision.test.mjs::WHAT[RELAY-009] active authority update advances revision and snapshot exactly once` |
