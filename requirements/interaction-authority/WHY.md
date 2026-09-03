# interaction-authority — WHY

## 1. 领域动机与核心矛盾

物理传输层上的 `role=user` 文本极其廉价且极易伪造。如果将物理用户消息形态直接等同于权威交互（Authority Turn），系统会出现以下破坏：
1. **权限自我抬升**：内部合成的 continuation、repair 提示或诊断重试在经过传输层后被重新误判为真人发起的根指令（HumanRoot）。
2. **状态与预算被异常重置**：每次伪造的 Root 都会错误地创建新的 Logical Run、重置 Fallback/Repair 预算或修改已锁定的 Agent 绑定。
3. **中断与自恢复破坏恢复流程**：将 degeneration-guard 等自恢复介入误判为主流程崩溃，导致不当推进重试游标。

`interaction-authority` 确立唯一权威来源边界：
- 严格区分 **Root**（创建 Logical Run 并确立权威）与 **Continuation**（仅延续既有 Run，禁止抬权）；
- 物理消息形态绝不是 authority 证据，仅有 typed provenance 能建立权限；
- 未知来源（UnknownOrigin）一律 fail-closed。

## 2. 核心不变量与破坏后果

- **PhysicalUserMessage ≠ AuthorityTurn**：物理层消息必须经由显式、受控的提升函数在物理落地证明后才能升级为 AuthorityRoot；若破坏，任意中间件即可劫持会话权限。
- **Continuation 严格受限**：Continuation 必须继承既有 Root 权威，禁止新建 RunId、修改 SelectedAgent 或重置 Fallback 预算；若破坏，自动修复会无限死循环。
- **Admission ≠ Outcome**：repair claim 只证明一次自动修复已获准进入物理执行，不证明该修复已经返回、更不证明它失败。若把“已 claim”直接解释成“已耗尽”，同一 repair 在飞行期间的合法 idle/reconcile 竞态会把仍在生成的有效结果提前判死。
- **Root acceptance 与 identity installation 是同一 durable fact**：`AuthorityRootAccepted` 必须携带 participant-identity owner 准备的完整 evidence；拆成两个 append 会在 crash cut 留下无 root、无 closure source 的孤儿 identity。
- **原子 Profile 不可拼装**：执行 profile 必须携带 accepted root 内的版本化 identity evidence，禁止从历史消息、Session cache、物理 parent 或 agent 名称动态拼凑。
- **证据保管不等于身份所有权**：本包持久化并精确暴露 identity evidence，以便重放与审计；stable SelectedAgent、Role、Persona 与 provenance/version 的解析、不可变性及替换规则仍只属于 `participant-identity`。当前 EffectiveAgent/provider/model/lease 只属于 execution binding。
- **物理 Session 不等于 logical run**：authority profile 与 identity evidence 都以 exact logical run 为作用域；每种 HumanRoot/AgentOwnerRoot lifecycle 必须先收敛为 exact durable `AuthorityLogicalRunClosed`，SessionId 才可复用。idle/timeout 与物理 topology 不是 closure。

## DEPENDS ON

- `participant-identity`
- `session-ontology`

## Physical fatal boundary

InteractionRepair owns gate/claim/fresh-terminal policy；process fuse是foreign physical effect。若repair runtime直接fatal，claim与physical acceptance可能未settle，重复idle/reconcile也可能对同一incident重复kill。
