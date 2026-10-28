# participant-identity — WHAT

## [001] ParticipantIdentity 是 logical participant run 的唯一私有身份 owner

每个 durable logical participant run 恰有一个私有强类型 `ParticipantIdentity`。它原子包含 `Role`、稳定 `Persona` 与 Persona provenance/version；字段不被其它包分拆拥有或独立改写。该 identity 在 exact run 内不可变，不以 `SessionId` 生命周期为作用域。

Role 是本名词汇，活跃角色为 orchestrator/manager/engineer/devops/plan/advisor 六个；每个 Role 在目录里恰对应一个 Persona（Engineer→Engineer、Manager→Lead、Orchestrator→Director、DevOps→Operator、Plan→Planner、Advisor→Auditor）。Engineer 是独立身份，拥有本地调查与源码读写权能，不是其它角色的别名或 Persona 包装。活跃身份集合与调度路径只包含当前活跃角色；Bookkeeper 是内部槽位（Persona 为 Curator，role 投影为 null），只用于案例维护，不进普通调度、工具门禁与公开 fork 候选。

## [002] ParticipantIdentity ≠ ExecutionBinding

`ParticipantIdentity` 表达由 `IdentitySeed` 派生、在 logical participant run 内不可变的参与者身份：`Role`、`Persona` 与 `SelectedAgent` 是 canonical identity evidence。`ExecutionBinding` 表达某次具体物理执行的动态分配：每次 fresh physical execution 把固定的 Role 路由到 model target，并签发租约与容量栅栏（绑定严格含 `session + physical + Role + Participant + target + fence`）。重试或显式调度只能按固定 Role 申请新物理执行的 model target/lease binding；物理目标或租约不回写为 identity，角色身份不被轮换或变更。

## [003] Persona 由 Role × versioned provenance resolve-once

logical participant run 建立时，identity owner 以 `Role × persona provenance/version` 解析一次完整 `ParticipantIdentityEvidence`。该 evidence 包含 Role/Persona/SelectedAgent 且在 run 内不可变，只有作为同一个 durable `AuthorityRootAccepted` payload 的必填字段被原子追加后才算安装；不存在先追加独立 identity-installation fact、再追加可失败 root acceptance 的路径。任一追加失败时 identity 与 root 均未安装；相同 acceptance payload 的重放幂等，run 内不同 payload 一律拒绝。

## [004] 换执行者 ≠ 换人

物理执行重试与模型目标重绑只改变物理目标与租约 binding（为固定 Role 重新分配 model target/lease）。执行期间暴露的 Role、Persona、SelectedAgent 与 provenance/version 逐字段等于该 run durable 的 `ParticipantIdentityEvidence`，不被分配的 provider/model 目标或租约覆盖。旧 journal 里的 `PeerAgent`/`EffectiveAgent`/cursor 字段在解码时被忽略，不再编码。

## [005] system prompt identity 只消费 ParticipantIdentity

system prompt 的身份标识由 `ParticipantIdentity.Role` 与其稳定 Persona 产生，不读取当前 provider、model 或执行绑定。机器执行切换不改变同一 run 的身份 prompt 标识。

## [007] 内部身份仍受同一原子模型约束

Bookkeeper 等内部 logical participant run 同样拥有私有 `ParticipantIdentity` 与稳定 Persona；其内部身份不进入公开 `Role` 联合类型，也不进 Manager 的公开 fork 候选。内部身份不得拆成独立 Persona 缓存。Bookkeeper 只在案例维护机制中使用，不暴露给普通 participant 调度与公开工具门禁。

## [008] 派生 root 只安装显式 owner 签发的 identity evidence

child、attached 与 InternalLeaf 的 root 必须携带 identity owner 为 exact logical participant run 签发的 typed evidence（`IdentitySeed` 派生），原子命名 OwnerLogicalRunId、LogicalRunId、Role、SelectedAgent、稳定 Persona 与 provenance/version。只有 owner、run 与 root acceptance 全部精确匹配时，它才可进入原子 `AuthorityRootAccepted` payload；wrong-owner、wrong-run 与字段缺失均 fail-closed。后续物理执行的 model target 由调度独立裁决并绑定 target/lease。Persona 继承只由该 evidence 说明，不根据 Session 缓存、Host physical parent 或其它物理拓扑推断、补全或重新解析。已关闭的 owner run 拒绝其继承 evidence；恢复已随原子事实记录的证据时不再重复要求 owner 在场。

## [009] exact prior-run closure 后才可在同一 SessionId 安装 fresh identity

`SessionId` 可复用为物理容器。同一 SessionId 上存在未精确关闭的 logical participant run 时，任何不同 identity 或 fresh root fail-closed。只有 interaction-authority 为 exact `(SessionId, LogicalRunId, AuthorityRootId)` 持久化唯一 `AuthorityLogicalRunClosed`，并由同一 fold 释放该 run 的 active identity binding 后，fresh root 才可通过新的原子 `AuthorityRootAccepted` payload 安装全新的 `ParticipantIdentity`。新身份不继承旧 run 的缓存字段；lifecycle terminal、association removal、时间、idle/timeout 或 Host 观察均不得单独推断 closure。

## [010] 活跃身份解析与历史身份隔离解码

活跃身份解析（名字解析或新建 root）严格限定于当前活跃角色集合（`orchestrator`、`manager`、`engineer`、`devops`、`plan`、`advisor`）；任何非此集合的身份在活跃调度路径中被拒绝，内部槽位名（bookkeeper）不产出可绑定 evidence。历史事件、日志与归档中的身份走隔离的历史解码边界（十一个角色标签均可按原拼写解码，仅供 journal 重放）；读取或恢复时不把只读历史身份升级为具备源码写入权限的 `engineer`，也不把 `devops` 误解析为具备 Fission 权能的 `engineer`。
