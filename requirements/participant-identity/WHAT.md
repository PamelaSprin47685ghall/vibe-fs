# participant-identity — WHAT

## PID-001: Role 是 office 身份；Tier 只改 ExecutionBinding

`Role` 是系统预定义的固定 office 身份枚举（如 Manager、Orchestrator、Coder、Inspector、DevOps、Browser、Inquiry、Reviewer、Distiller、Blogger）。`AgentTier` 仅包含 Fast 与 Deep 两档。Tier 只决定物理 ExecutionBinding 的机器档位（EffectiveAgent），不产生新 Role，不改变 Role Law，亦不改变工具权限集合。`ToolPermission` 与权限矩阵归 `office-capability`；身份 owner 不得复制 capability law。

## PID-002: Role ≠ Persona ≠ ExecutionBinding 三轴分离

Role、Persona 与 ExecutionBinding 保持绝对独立正交：
- Role：职责 office，在 session 生命周期内严格不变。
- Persona：封闭的 22 case 类型；自我模型在 session 创建时单次绑定且不可变。只有 owner 的穷尽 `render/tryParse` 可穿越字符串边界；显示文案不得参与 authority 判断。
- ExecutionBinding：由 EffectiveAgent/tier 及其物理执行租约组成，可在 Fallback 或 Strength 机制下发生切换。

全仓只有这一组 `Role`/`AgentTier`/`Persona` identity vocabulary。旧 `RoleIdentity` 字符串桥与任何 forwarding alias 均不存在。

## PID-003: Persona 一次冻结，创建时 resolve-once，之后不可变

`SessionPersona` 以 branded `SessionId` 为键、以 `Persona` 为值，在 session 创建时根据 `Role × initial tier` 完成单次绑定（resolve-once）。同值重绑幂等成功；异值重绑返回携带 `SessionId × existing × attempted` 的 typed `PersonaRejection`，原值不变。Host adapter 必须显式传播或在物理边界 fail closed，不得吞掉冲突后继续。在后续的 Fallback、Strength、Peer 切换或 mid-life 状态中，严禁改写或重绑 Persona。

## PID-004: 换执行者 ≠ 换人；Fallback/Strength/Peer 只改 ExecutionBinding

Peer Fallback、Strength 副本运行以及援助升级（fast→deep）均仅变更物理执行绑定（ExecutionBinding）。参与者的 Persona 保持恒定，system prompt 身份标识与自我模型字节保持不变。

## PID-005: system prompt identity 是 CanonicalRole 的函数，tier/EffectiveAgent 不参与

system prompt 中的身份标识由 `CanonicalRole` 纯函数决定，其输出值严禁包含 `fast` 或 `deep` 标记。同一 Role 的 fast 档与 deep 档共享完全相同的身份 prompt 标识。

## PID-006: `fast-*`/`deep-*` 是机器路由身份，不冒充 Persona 自称、不穿过 horizon

`fast-*` 与 `deep-*` 仅作为 ExecutionBinding 的底层机器路由名称（wire 名称），严禁作为模型可见的 Persona 自称，亦不得穿透 participant horizon 暴露给模型视野。

## PID-007: Peer 配对本体：peer(fast-ROLE)=deep-ROLE，对称且启动可证明

系统为每个 canonical role 维护严格对称的 Peer 配对：`peer(fast-ROLE) = deep-ROLE` 且 `peer(deep-ROLE) = fast-ROLE`。Peer 名称必须在 catalog 中存在且双向对称。Bookkeeper 内部角色同样遵循对称配对。物理模型是否相同不影响 Peer 关系的成立。

## PID-008: managed session 冻结 agent 档位；user-facing 由最近真实用户请求决定 EffectiveAgent；model 不属于用户 binding authority

- 有父级的 managed session：创建时冻结 base EffectiveAgent；除显式类型化覆盖（ExplicitExecutionOverride）外，任何请求字段均不得重绑。
- 无父级的 user-facing session：base EffectiveAgent 由最近一次外部真实用户请求决定；插件自产 prompt 沿用最近观测到的 agent。
- 物理 model 不受 Host 或用户 message 的 model 字段控制。synthetic `SendPrompt` 仅冻结 EffectiveAgent 并保持 `Model=None`；物理执行准入时由模型调度层以 `(SessionId, PhysicalUserMessageId)` 取得租约。

## PID-009: 内部身份有机器身份 + Persona + peer，但不进 public Role DU

Bookkeeper 等内部运行时身份拥有机器身份、内部 Persona 以及对称 Peer，但不进入公开的 `Role` 联合类型枚举，不出现在 Manager 的公开 fork 候选列表中。

## PID-010: personhood 连续性：child/attached/InternalLeaf persona 继承 owner persona

派生执行上下文（child session、attached session 以及 InternalLeaf session）的 Persona 严格继承其 owner 的 Persona，严禁按派生上下文自身的 fast/deep 档位重新解析。
