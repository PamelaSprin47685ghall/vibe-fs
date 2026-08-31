# participant-identity — WHAT

## PID-001: `ParticipantIdentity` 是 logical participant run 的唯一私有身份 owner

每个 durable logical participant run 恰有一个私有强类型 `ParticipantIdentity`。它原子包含 canonical `SelectedAgent`、canonical `PeerAgent`、`Role`、initial `Tier`、稳定 `Persona` 与 Persona provenance/version；字段不得被其它包分拆拥有或独立改写。该 identity 在 exact run 内不可变，不以 `SessionId` 生命周期为作用域。

## PID-002: ParticipantIdentity ≠ ExecutionBinding

`ParticipantIdentity` 表达稳定的参与者身份：`SelectedAgent` 与 `PeerAgent` 是 canonical catalog 中的 route-name identity evidence。`ExecutionBinding` 表达某次物理执行实际采用的 `EffectiveAgent`、provider/model 与租约。初始选择可作为首次 binding 的输入，但 Fallback、Strength、Peer 路由或显式 execution override 只能替换 `ExecutionBinding`，不得把当前物理选择回写为 identity。

## PID-003: Persona 由 Role × initial Tier × versioned provenance resolve-once

logical participant run 建立时，identity owner 以 canonical selected/peer relation、`Role × initial Tier × persona provenance/version` 解析一次完整 `ParticipantIdentityEvidence`。该 evidence 只有作为同一个 durable `AuthorityRootAccepted` payload 的必填字段被原子追加后才算安装；禁止先追加独立 identity-installation fact，再追加可失败的 root acceptance。任一追加失败时 identity 与 root 均未安装；相同 acceptance payload 的重放幂等，run 内不同 payload 一律拒绝。

## PID-004: 换执行者 ≠ 换人

Peer Fallback、Strength 副本运行与援助升级仅改变物理 `ExecutionBinding`。执行期间暴露的 canonical SelectedAgent、PeerAgent、Role、initial Tier、Persona 与 provenance/version 必须逐字段等于该 run 的 durable `ParticipantIdentityEvidence`；它们不得被当前 EffectiveAgent、provider/model 或租约覆盖。

## PID-005: system prompt identity 只消费 ParticipantIdentity

system prompt 的身份标识由 `ParticipantIdentity.Role` 与其稳定 Persona 产生，不读取当前 tier、EffectiveAgent、provider 或 model。机器执行切换不得改变同一 run 的身份 prompt 标识。

## PID-006: 稳定 route identity 与可变机器 binding 严格分界

canonical `fast-*` / `deep-*` selected 与 peer agent 名称可且仅可作为 `ParticipantIdentityEvidence.SelectedAgent` / `PeerAgent` 穿过边界；它们是稳定 catalog route identity，不是当前机器 binding，也不得冒充 Persona 自称。当前 `EffectiveAgent`、provider/model 与租约标识仅属于 `ExecutionBinding`，不得覆盖 selected/peer evidence 或 initial Tier。

## PID-007: Peer 是 ParticipantIdentity 内的稳定对称 route identity

identity owner 必须从 canonical catalog 解析 `SelectedAgent` 与 `PeerAgent` 并一起封入 `ParticipantIdentity`。`peer(fast-ROLE) = deep-ROLE` 且 `peer(deep-ROLE) = fast-ROLE`；catalog 中每个 PeerAgent 必须存在且双向对称。物理模型相同、EffectiveAgent 变化或 execution fallback 均不改变该 run 的 selected/peer identity facts。

## PID-008: initial Tier 属于身份；当前执行选择属于 binding authority

initial Tier 由合法 root 输入确定并封入 `ParticipantIdentity`，在 run 内不可变。当前 EffectiveAgent、provider/model 与租约由 execution owner 针对 exact `(SessionId, PhysicalUserMessageId)` 决定；Host 或用户消息中的 model 字段、synthetic prompt 与 session cache 均无权改写 identity 或自行建立物理执行绑定。

## PID-009: 内部身份仍受同一原子模型约束

Bookkeeper 等内部 logical participant run 同样拥有机器身份可见性之外的私有 `ParticipantIdentity`、稳定 Persona 与对称 Peer；其内部 Role 不进入公开 `Role` 联合类型或 Manager 的公开 fork 候选。内部身份不得拆成独立 Persona/Peer 缓存。

## PID-010: 派生 root 只能安装显式 owner-derived identity evidence

child、attached 与 InternalLeaf 的 root 必须携带 identity owner 为 exact logical participant run 签发的 typed owner-derived evidence；该 evidence 原子命名 OwnerLogicalRunId、LogicalRunId、canonical SelectedAgent/PeerAgent、Role、initial Tier、稳定 Persona 与 provenance/version。只有 owner、run 与 root acceptance 全部精确匹配时，它才可进入原子 `AuthorityRootAccepted` payload；wrong-owner、wrong-run 与字段缺失均 fail-closed。Persona 继承关系只能由该 evidence 证明，严禁根据 Session 缓存、机器档位、Host physical parent 或其它物理拓扑推断、补全或重新解析。

## PID-011: exact prior-run closure 后才可在同一 SessionId 安装 fresh identity

`SessionId` 可复用为物理容器。同一 SessionId 上存在未精确关闭的 logical participant run 时，任何不同 identity 或 fresh root 必须 fail-closed。只有 interaction-authority 为 exact `(SessionId, LogicalRunId, AuthorityRootId)` 持久化唯一 `AuthorityLogicalRunClosed`，并由同一 fold 释放该 run 的 active identity binding 后，fresh root 才可通过新的原子 `AuthorityRootAccepted` payload 安装全新的 `ParticipantIdentity`。新身份不得继承旧 run 的缓存字段；lifecycle terminal、association removal、时间、idle/timeout 或 Host 观察均不得单独推断 closure。
