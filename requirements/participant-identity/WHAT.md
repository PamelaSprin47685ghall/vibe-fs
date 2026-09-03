# participant-identity — WHAT

## PID-001: `ParticipantIdentity` 是 logical participant run 的唯一私有身份 owner

每个 durable logical participant run 恰有一个私有强类型 `ParticipantIdentity`。它原子包含 `Role`、稳定 `Persona` 与 Persona provenance/version；字段不得被其它包分拆拥有或独立改写。该 identity 在 exact run 内不可变，不以 `SessionId` 生命周期为作用域。Role 是本名词汇（Manager/Orchestrator/Coder/Inspector/Browser/Inquiry/Reviewer/DevOps/Distiller/Blogger），每个 Role 在运行时恰对应一个 Persona。Predictor 是内部机制专用角色，仅为 Strength 降级指定廉价 provider/model，不参与普通调度、工具门禁与用户可见 fork 候选。

## PID-002: ParticipantIdentity ≠ ExecutionBinding

`ParticipantIdentity` 表达稳定的参与者身份：`Role` 与 `Persona` 是 canonical identity evidence。`ExecutionBinding` 表达某次物理执行实际采用的 `EffectiveAgent`、provider/model 与租约。初始选择可作为首次 binding 的输入，但 Fallback、Strength 或显式 execution override 只能替换 `ExecutionBinding`，不得把当前物理选择回写为 identity。

## PID-003: Persona 由 Role × versioned provenance resolve-once

logical participant run 建立时，identity owner 以 `Role × persona provenance/version` 解析一次完整 `ParticipantIdentityEvidence`。该 evidence 只有作为同一个 durable `AuthorityRootAccepted` payload 的必填字段被原子追加后才算安装；禁止先追加独立 identity-installation fact，再追加可失败的 root acceptance。任一追加失败时 identity 与 root 均未安装；相同 acceptance payload 的重放幂等，run 内不同 payload 一律拒绝。

## PID-004: 换执行者 ≠ 换人

Fallback、Strength 副本运行与援助升级仅改变物理 `ExecutionBinding`。执行期间暴露的 Role、Persona 与 provenance/version 必须逐字段等于该 run 的 durable `ParticipantIdentityEvidence`；它们不得被当前 EffectiveAgent、provider/model 或租约覆盖。

## PID-005: system prompt identity 只消费 ParticipantIdentity

system prompt 的身份标识由 `ParticipantIdentity.Role` 与其稳定 Persona 产生，不读取当前 EffectiveAgent、provider 或 model。机器执行切换不得改变同一 run 的身份 prompt 标识。

## PID-006: 稳定 Role identity 与可变机器 binding 严格分界

`Role` 与 `Persona` 是稳定 identity evidence，不是当前机器 binding。当前 `EffectiveAgent`、provider/model 与租约标识仅属于 `ExecutionBinding`，不得覆盖 Role/Persona evidence。

## PID-007: 内部身份仍受同一原子模型约束

Bookkeeper 等内部 logical participant run 同样拥有机器身份可见性之外的私有 `ParticipantIdentity` 与稳定 Persona；其内部 Role 不进入公开 `Role` 联合类型或 Manager 的公开 fork 候选。内部身份不得拆成独立 Persona 缓存。Predictor 仅在 Strength 内部机制中使用，不暴露给普通 participant 调度与工具门禁。

## PID-008: 派生 root 只能安装显式 owner-derived identity evidence

child、attached 与 InternalLeaf 的 root 必须携带 identity owner 为 exact logical participant run 签发的 typed owner-derived evidence；该 evidence 原子命名 OwnerLogicalRunId、LogicalRunId、Role、稳定 Persona 与 provenance/version。只有 owner、run 与 root acceptance 全部精确匹配时，它才可进入原子 `AuthorityRootAccepted` payload；wrong-owner、wrong-run 与字段缺失均 fail-closed。Persona 继承关系只能由该 evidence 证明，严禁根据 Session 缓存、Host physical parent 或其它物理拓扑推断、补全或重新解析。

## PID-009: exact prior-run closure 后才可在同一 SessionId 安装 fresh identity

`SessionId` 可复用为物理容器。同一 SessionId 上存在未精确关闭的 logical participant run 时，任何不同 identity 或 fresh root 必须 fail-closed。只有 interaction-authority 为 exact `(SessionId, LogicalRunId, AuthorityRootId)` 持久化唯一 `AuthorityLogicalRunClosed`，并由同一 fold 释放该 run 的 active identity binding 后，fresh root 才可通过新的原子 `AuthorityRootAccepted` payload 安装全新的 `ParticipantIdentity`。新身份不得继承旧 run 的缓存字段；lifecycle terminal、association removal、时间、idle/timeout 或 Host 观察均不得单独推断 closure。
