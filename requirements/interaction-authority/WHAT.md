# interaction-authority — WHAT

## INTERACTION-AUTHORITY-001: 物理用户消息不等于 authority turn

物理 `role=user` 消息仅是传输层形态。物理消息标识符必须经由唯一的显式提升通道在物理接收已确立（`PhysicalAccepted`）后方可升级为 `AuthorityRoot`。不存在从传输收据直接转换为 AuthorityRoot 的通道。

## INTERACTION-AUTHORITY-002: 形态不是 authority 证据

零宽字符、空白排版、固定模板、时间戳、文本长度或合成配置中的注释/字段形态均不能作为 Authority 身份证明。消息的权威性仅能由系统内建的 typed 来源机制判定。

## INTERACTION-AUTHORITY-003: Root 独占权

`AuthorityRoot` 具有独占权限：
1. 创建新的 Logical Run；
2. 选定或变更 SelectedAgent（并推导 PeerAgent、CanonicalRole 与 SelectedTier）；
3. 成为新的 Fallback 根节点；
4. 重置 Interaction Repair 预算；
5. 成为后续默认 SelectedAgent 的延续基准。

新 Root 生效时原子清空全部 run-scoped 状态（包括待决 claims、已接受 continuation 映射与序列号），并重置 Fallback 游标。

## INTERACTION-AUTHORITY-004: Continuation 禁区

所有类型的 Continuation 仅用于延续已存在的 Logical Run，绝对禁止执行 Root 独占操作：不得新建 RunId、不得修改 SelectedAgent/CanonicalRole、不得更新底层 AuthorityProfile、不得重置 Fallback 或 repair 预算。Continuation 必须完整继承宿主 Run 与 Root 标识。

## INTERACTION-AUTHORITY-005: 四类 provenance 与两种 Root

系统严格区分四类来源形式：`AuthorityRoot`（包含 `HumanRoot` 与 `AgentOwnerRoot`）、`Continuation`、`HostInternal` 与 `UnknownOrigin`。该分类为闭集合，任何 Continuation 类型均不可被解析为 Root，反之亦然。未能匹配到合法类型的来源一律归入 `UnknownOrigin`。

## INTERACTION-AUTHORITY-006: HumanRoot 必须显式命名 managed agent

`HumanRoot` 必须显式指定合法的 managed agent 名称。省略名称、使用废弃裸名或格式错误必须 fail-closed 显式拒绝，禁止系统静默猜测或隐式补全 agent。

## INTERACTION-AUTHORITY-007: UnknownOrigin fail-closed

`UnknownOrigin` 绝对禁止更新执行 Profile、启用 Fallback 或发起任何 Continuation。无法证明来源合法性的请求必须立即阻断。

## INTERACTION-AUTHORITY-008: 来源解析优先级

消息来源按固定优先级严格判定：已确认的 Host 消息 > 已 Claim 的 PromptKey > Host 内部 Compaction/Synthetic > 已注册的 AgentOwnerRoot > 外部证明合法的 HumanRoot > UnknownOrigin。优先级顺序本身构成安全边界，避免真实业务消息被内部机制降级或冒充。

## INTERACTION-AUTHORITY-009: 纯函数永不推断 HumanRoot

来源判定中的纯计算函数绝不推断返回 `HumanRoot`。`HumanRoot` 只能在激活 Profile 缺席且携带合法显式 agent 时由 Ingress 边界授予；处于活跃 Run 中的未知消息绝不可抬升为 Root。

## INTERACTION-AUTHORITY-010: 自动 continuation 稳定 occasion identity 与有界 admission

自动合成的 repair、nudge、review 提示与重试消息绝不可借机抬升权限。普通 repair 的持久化 admission 在单次 `(SessionId, LogicalRunId, repair family)` 作用域内严格限制为一次；第二次同 family claim 必须被幂等吸收，绝不能因此推断第一次 repair 已失败。Manager 的自动闲置提示及特定会话的专项诊断必须绑定精确的 ProviderRun 实例，确保重放幂等且不会产生无界提示循环。任何自动 continuation 的 duplicate admission 都属于幂等状态而非 transport/protocol failure；Dispatch 必须以 typed outcome 暴露该状态，调用方不得把它升级成 terminal failure。

## INTERACTION-AUTHORITY-011: authority 是原子 profile 内的稳定子记录

单次执行的权威身份必须封装在不可变的 `AttemptExecutionProfile` 内原子携带，包含 SessionId、LogicalRunId、AuthorityRootId、SelectedAgent 及关联角色，在整个 Logical Run 期间保持不可变。禁止从会话缓存或分散的消息碎片中临时拼接权威状态。

## INTERACTION-AUTHORITY-012: degeneration-guard 是 continuation 而非 fallback 失败

degeneration-guard 自恢复消息（`DegenerationGuard`）等属于强类型 Continuation。它们延续当前 LogicalRun，复用既有 Root 与 Profile，不得建立新 Root、不得重置 Fallback 游标、亦不得计入模型重试失败次数。`DegenerationGuard` 不得伪装成 `ProviderRetryAttempt`。

## INTERACTION-AUTHORITY-013: 显式 continuation 绑定保持 authority continuity

同会话与同一 LogicalRun 下的强类型 continuation 推进属于权限连续演进：仅执行绑定的 EffectiveAgent 发生必要变更，其余 Root、Profile 与游标位置全部保持不变。

## INTERACTION-AUTHORITY-014: Nudge 与 JoinGuard 是 Continuation

JoinGuard、闲置 Nudge 等流转控制指令均为 Continuation，不产生新的 Authority。在存在未决后台任务时仅允许发送 JoinGuard 延续等待，禁止隐式创建新 Root。

## INTERACTION-AUTHORITY-015: external-user ingress 不授予 authority

处于运行中途的外部用户消息仅作为低权限唤醒信号打断等待，不取消当前运行时，不直接赋予 Prompt authority，亦不重置 LogicalRun 或新建生命周期。

## INTERACTION-AUTHORITY-016: Root claim 不进入 continuation 映射

接受 `AgentOwnerRoot` 的 claim 不会将消息写入 Continuation 查找映射。曾经作为 Root 的物理消息不能作为后续判定 Continuation 的依据。

## INTERACTION-AUTHORITY-017: continuation 只能接续 active run

Continuation 只能挂靠当前活跃的 `ActiveLogicalRun`，绝对禁止回退挂靠已归档或结束的历史 Profile。

## INTERACTION-AUTHORITY-018: HumanRoot Manager 的 LifeCompleted 原子释放 active run

Manager 生命周期的 `LifeCompleted` 事实原子地将对应的 `ActiveLogicalRun` 清空并释放 run-scoped 映射，同时归档历史 Profile。旧 Run 终结后，后续消息仅能通过合法的显式外部输入建立全新 Root，禁止通过 Continuation 路径复活已终结的交互。

## INTERACTION-AUTHORITY-019: repair admission、飞行态与耗尽态必须分型

`InteractionRepair` 的 claim/Submitted/PhysicalAccepted 只建立一次 repair attempt 的 admission/物理落地证据，不建立 repair failure。当前 repair attempt 仍为 `finish=None`、`tool-calls` 或其它明确 in-progress 观测时，重复 idle/reconcile 必须保持等待，禁止发送第二次 repair，禁止产生 `INTERACTION_REPAIR_EXHAUSTED`。只有当前已确认属于 `InteractionRepair` 的 attempt 自身给出不可用的稳定终结材料（例如空/XML-only `stop` 或 `length`）时，Repair owner 才能发布 `INTERACTION_REPAIR_EXHAUSTED`。普通旧 turn 在 repair 已 admitted 后再次被观察，只能幂等吸收，不能替 repair 宣判失败。
