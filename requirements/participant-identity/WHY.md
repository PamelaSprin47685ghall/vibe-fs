# participant-identity — WHY

## 领域动力与核心张力

`SessionId` 是可复用的物理容器，不是 personhood。参与者身份属于一个可持久恢复、可精确关闭的 logical participant run。该 run 的 canonical selected/peer agent route names、`Role`、initial `Tier`、稳定 `Persona` 与 Persona provenance/version 必须由一个私有强类型 `ParticipantIdentity` 原子拥有；当前 EffectiveAgent、provider/model、租约与 fallback 位置属于可变的 `ExecutionBinding`。root acceptance 与 identity installation 必须共享一个 durable fact，否则 crash 会留下无 authority closure 来源的孤儿 identity。

当生命周期或所有权混淆时，会产生严重退化：
- **容器冒充人**：复用 Session 时沿用缓存 Persona，使已关闭 run 的身份泄漏到 fresh root。
- **换执行者变成换人**：Fallback、Strength 或 Peer 路由改写 Role、initial Tier、Persona 或 provenance。
- **拓扑伪造身份**：child、attached 或 InternalLeaf 根据物理 parent、session cache 或自身机器档位猜出 Persona。
- **证据与身份双 owner**：authority 投影重新解析 Role/Persona，而非从原子 `AuthorityRootAccepted` 重放 identity owner 准备的版本化 evidence。

核心不变量：**换执行者不等于换人；换物理容器也不自动换人。** `ParticipantIdentity` 仅在 logical participant run 内不可变。只有 exact prior-run closure 已持久化后，同一 SessionId 上的 fresh root 才能安装一个全新的 identity。

## 破裂后果

- 已关闭 run 的 Role、Persona 或 peer 污染同一物理 Session 的后继 run。
- 模型、fallback 或 execution override 静默改写责任模型与 Persona 自称。
- 恢复时依据 Host 层级错误收养身份，或因缓存缺失生成另一份身份。
- Persona 规则升级后无法证明某次执行采用了哪个 provenance/version。

## 边界与关系

- `participant-identity`：唯一拥有 `ParticipantIdentity` 的解析、不可变性、继承证据与替换规则。
- `interaction-authority`：把 identity owner 准备的版本化 evidence 原子封入 `AuthorityRootAccepted` 并随 exact execution profile 暴露；不解析、不修改身份。
- `session-ontology`：拥有物理 Session 分类、attachment 与 durable logical ownership；不拥有 logical-run identity。
- `office-capability`：定义 Role 的权能后果；消费身份证据。
- `capability-enforcement`：保证可见能力与可执行能力同源；消费身份证据。

## DEPENDS ON

- `session-ontology`
