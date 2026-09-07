# relay-incumbency — WHAT

## RELAY-001: 一条 open Road 至多一个 active 迭代

Road 是根用户需求的持续执行道路，Incumbency 是一次 Manager 迭代的逻辑任期。任何 durable projection 中，同一 open Road 的 active incumbency 数量必须小于等于一；并发 opening 使用前任 RetirementId 派生同一 deterministic IncumbencyId。相同 exact fact 的重复提交按 event-store 至少一次语义幂等折叠，只能产生一个 active incumbency 与一个 manager-loop physical prompt；不同 identity 或 payload 的冲突创建必须 fail closed。不得用 journal envelope 数冒充逻辑迭代数。

## RELAY-002: 每一次迭代使用同一 opening 代数与同一状态机

所有迭代均从初始任期事实（AuditPending）开始。首次 opening 与每次后续 opening 是同一代数：相同的 `IncumbencyOpened of IncumbencyId * WorkspaceSnapshotId` 事件形状，相同的 AuditPending 起始事实（尚未记录评审事实），相同的 authority 绑定检查。Opening 代数不按 Planning Table、T1、Entrusted Road 或迭代序号分支：这些是 Role Law 下有效的当前任务纪律，不是 opening 的分支条件；亦不存在“第一任先实现、后检查”的流程特判。`Facts.IncumbencyOpening.initial` 与 `Facts.IncumbencyOpening.next` 只是同一 opening 在不同时刻的调用入口：前者同时产出 RoadOpened 与 IncumbencyOpened，后者只产出 IncumbencyOpened；两者产出的 IncumbencyOpened 在事件结构与 fold 语义上完全一致。跨包裁定依据 STRUCTURED-WORKFLOW-003/004：Relay state 与 RoadView 仅表达不可变领域事实（任期身份、评审记录、质量证书、退休纪要），`ActivePhase` 仅保留为只读诊断投影，严禁作为跨模块执行位置（PC）或效果选择 API；业务执行时序与决策权能由 Structured Workflow 与 Manager owner CE 独占。

## RELAY-003: 每任起步全权就绪，评审前有能力无任务

所有迭代自积极建立起即具备 Manager 角色的全部管理权能。当前迭代在提交评审前已经具备完整权限（包括维护任务账本、规划与委派），只是尚未承接具体执行任务（有能力无任务完全合法）。迭代开始时不预知评审后路由；评审前可见的 Manager 内容不得泄露低分承接修复、满分结束工作或还存在下一次迭代。

## RELAY-004: 非满分 assessor 原位取得实现责任

任一 assessment 维度低于 10 时，单次 AssessmentCommitted 在同一 fold transition 中同时记录 ScoreVector、从其派生对应 quality obligations 并确立工作归属事实（WorkOwned），只派生一次。发现问题的当前迭代成为实现负责人，不创建返工链，不开启新迭代。该状态由评审事实与未决义务的客观存在所约束（无有效满分质量证书），而非由外部调度器读取阶段枚举推动下一步执行。

## RELAY-005: retired 迭代永不恢复

IncumbencyRetired 一旦 committed，任何 replay、provider recovery、rebase、冲突、CAS miss 或 Host crash 都不得把该 IncumbencyId 重新变为 active。每次 retirement cut 宣告的 stale provider-run identity 必须在 Road fold 中累积；这些 run 的迟到 tool/terminal 观测即使跨过一次或多次后续迭代仍只能被吸收，不得进入 ordinary interaction repair 或再发 continuation。后续迭代只为自己的新 provider run 恢复 Manager 路由资格，绝不能把前任 stale run 重新解释成当前迭代。需要继续工作时只能创建新的 IncumbencyId。

## RELAY-006: 退休产出闭合 outcome，Continue 与 Accepted 决定 Road 去向

每次成功 retirement 在同一 durable transaction 中提交闭合的 `RetirementSummary = { Id; IncumbencyId; SnapshotId; AuthorityRevision; ProjectionCut = { ProviderRunId; ToolCallId }; Outcome }`，其中快照与 authority 修订是 load-bearing retirement binding，`Outcome = Continue | Accepted of QualityCertificateId`。`Continue` 表示工作尚未完成：承载 Road 的 ActiveLogicalRun 保持开放，projection 在下一次 active 迭代就位后只保留 typed authority 消息与当前迭代消息；`Accepted` 表示质量证书已被接受：当前迭代关闭，证书有效期间不得开启新的迭代。后续显式 `QualityCertificateInvalidated`（snapshot、rebase 或 CAS admission 驱动）使该证书失效后，允许以普通 opening 开启下一个 AuditPending 迭代；该新迭代同样以上一次 LatestRetirement cut 为 projection 下界，携带的快照即退休时当前快照。只有自动中断/激活是 Continue 专属，失效后重开走 Change ContinueLoop 普通派发。Continue 退休的快照允许与 assessment 时不同，Accepted 退休的快照必须等于 assessment 快照，两者 authority 都必须等于当前。ManagerLoopSignal 由对 Outcome 的匹配派生（Accepted 证书→Candidate，Continue→Continue），不得在 Outcome 之外另立请求或接受布尔。物理 SessionId 可以复用，但逻辑 IncumbencyId 与 provider context 必须重开。跨包约束：退休与续发循环属于领域事实流转，composition root（如 `PluginTransforms`）严禁拥有或内联循环决策与提示词派发，自动评审/工作/收尾时序由 Manager owner CE 自主驱动。

## RELAY-007: 已删除——normal-stop nudge 归属 interaction-authority

此编号永久空缺。manager guard gate、terminal occasion 与飞行态只由 INTERACTION-AUTHORITY-019 定义，Relay 不再复制 nudge 状态机。

## RELAY-008: authority 或证书绑定域变化显式失效证书

AuthorityRevision、WorkspaceSnapshotId、requirement digest、target/base horizon 任一变化都使旧 QualityCertificate 显式失效。失效不会恢复 assessor，只会驱动普通下一迭代。

## RELAY-009: active authority update 是 durable revision，不是普通 prompt

已有 active 迭代接纳追加要求时，必须以 expected previous `AuthorityRevision`、精确 `IncumbencyId`、新 `AuthorityRevision`、物理 accepted authority message 与 fresh `WorkspaceSnapshotId` 写入同一 Relay authority update。fold 原子推进 Road 与 active 迭代的 revision/snapshot，并使旧有效 QualityCertificate 失效；同一精确 update 重放幂等，stale previous revision、错误 incumbent 或冲突 replay 必须 fail closed。单独发送 continuation 不构成 authority change，retired 迭代也永远不能成为 authority update 目标。
