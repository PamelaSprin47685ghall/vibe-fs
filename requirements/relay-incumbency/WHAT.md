# relay-incumbency — WHAT

## RELAY-001: 一条 open Road 至多一个 active incumbent

Road 是根用户需求的持续执行道路，Incumbency 是一位当前任 Manager 的逻辑任期。任何 durable projection 中，同一 open Road 的 active incumbency 数量必须小于等于一；并发 successor activation 使用前任 RetirementId 作为唯一键，冲突创建必须 fail closed。

## RELAY-002: 第一任与后续任使用同一状态机

所有任期均从 AuditPending 开始。第一任只在 BatonSource 为 ExistingWorld，后续任为 Retirement；不得存在 Planning Table、T1、Entrusted Road 或“第一任先实现、Reviewer 后检查”的流程特判。

## RELAY-003: 每任起步只读 audit

AuditPending 只能读取 root authority、requirements、workspace snapshot 和证据，可提交唯一 assessment 或直接 suicide。低分 assessment durable 提交后才获得工作写能力；全满分后保持只读并等待退休。

## RELAY-004: 非满分 assessor 原位取得实现责任

任一 assessment 维度低于 10 时，assessment、对应 quality obligations 与 WorkOwnershipGranted 必须在同一 durable transaction 中提交。发现问题的当前任成为实现负责人，不创建 Reviewer→旧 Manager 返工链。

## RELAY-005: retired incumbent 永不恢复

IncumbencyRetired 一旦 committed，任何 replay、provider recovery、rebase、冲突、CAS miss 或 Host crash 都不得把该 IncumbencyId 重新变为 active。每次 retirement cut 宣告的 stale provider-run identity 必须在 Road fold 中累积；这些 run 的迟到 tool/terminal 观测即使跨过一个或多个 `SuccessorActivated` 仍只能被吸收，不得进入 ordinary interaction repair 或再发 continuation。successor 只为自己的新 provider run 恢复 Manager 路由资格，绝不能把前任 stale run 重新解释成当前任。需要继续工作时只能创建新的 IncumbencyId。

## RELAY-006: successor 依赖完整退休边界

SuccessorActivated 只能发生在 predecessor retirement、机器生成 baton 与 projection cut 均 durable 之后。物理 SessionId 可以复用，但逻辑 IncumbencyId 与 provider context 必须重开。

## RELAY-007: normal stop 不是任期终态

正常 assistant terminal、空回复或自然语言完成声明均不结束任期。只要 authority 仍有效且 provider capacity 可用，系统按新的 causal frontier 去重调度退出 nudge；provider failure 走独立 failure algebra。`TurnInProgress` / `TurnNeedsContinuation` 这类 tool-call 中间观测不是 normal terminal frontier：首次 observation 不得抢在 tool body durable effect 前触发 generic interaction repair；只有后续 fresh idle 仍证明该 turn 未继续时，既有 idle repair 才可介入。

## RELAY-008: authority 或证书绑定域变化显式失效证书

AuthorityRevision、WorkspaceSnapshotId、requirement digest、target/base horizon 任一变化都使旧 QualityCertificate 显式失效。失效不会恢复 assessor，只会驱动普通 successor。

## RELAY-009: active authority update 是 durable revision，不是普通 prompt

已有 active incumbent 接纳追加要求时，必须以 expected previous `AuthorityRevision`、精确 `IncumbencyId`、新 `AuthorityRevision`、物理 accepted authority message 与 fresh `WorkspaceSnapshotId` 写入同一 Relay authority update。fold 原子推进 Road 与 active incumbent 的 revision/snapshot，并使旧有效 QualityCertificate 失效；同一精确 update 重放幂等，stale previous revision、错误 incumbent 或冲突 replay 必须 fail closed。单独发送 continuation 不构成 authority change，retired incumbent 也永远不能成为 authority update 目标。
