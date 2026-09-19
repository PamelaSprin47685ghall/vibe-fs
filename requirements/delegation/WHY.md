# delegation — WHY

## 领域价值与核心矛盾

在多角色协作系统中，工作必须能够从一个 participant 转交至另一个 participant（例如 Orchestrator 委托独立 Manager 工作道路、Manager 派出 Engineer 进行调查与实现、Manager 续做调用固定 DevOps 运行与局部修复、以及 Sphinx 程序内部进行语义事实探究）。

转交的核心矛盾在于：**业务意图上的职责交接必须与底层的机器执行拓扑正交**。若将执行拓扑（SessionId、AgentId、worktree 路径、复用标志等）混入业务协议，模型将被迫充当物理拓扑的解码器，模糊任务所有权与权能边界。

## 核心不变量

1. **按后果委任（Entrust by Consequence）**：委任必须明确 charge（语义任务）、office（权能后果）、logical owner（任务归属）以及 bounded 返回后果。
2. **普通工作链与委托边界**：
   - Orchestrator 负责战役战略统筹，委托 Manager 独立道路（`commission`）；
   - Manager 管理任意数量的 Engineer（通过 `fork` 派出）与唯一绑定的固定 DevOps（通过 `resume` 续做）；
   - Engineer 负责本地事实调查与源码读写实现/重构/测试编写，完成本次工作即返回 Manager，不组织验证链，严禁 Engineer→DevOps 任何直接、包装或转发差遣；
   - DevOps 拥有直接工程能力与真实命令执行，具备角色固有非架构级修复授权，执行验证后向 Manager 返回事实与边界；禁止 DevOps 创建或差遣其他工程子代理；
   - Sphinx 作为程控探究工作流，可在程序需要时同步调用只读 Engineer 调研本地事实，Engineer 完成后返回程序调用点，不借用 DevOps、不递归、不 Fission。
3. **独立道路与续做严格区分**：
   - `fork`：必填 calling（Manager 仅限 `engineer`）创建新独立道路；
   - `resume`：必填 name 续做既有道路（包括续做既有 Engineer 道路，或调用 Manager 道路唯一绑定的固定 DevOps），复用该 participant 的历史与已绑定环境；
   - 保持 delegation-024、delegation-026、delegation-027 的契约：resume 同步等待 exact assignment 接收确认（AcceptedAssignment）后返回承接后果，异步执行工作，结果由 join/horizon 独立消费；同一道路同时至多一个 active work unit，忙碌时明确拒绝新任务，严禁将新任务伪装成 BusyAgentNudge。
4. **拓扑隔离**：机器身份与会话拓扑严禁穿透 horizon 污染业务协议面。
5. **返回是证据而非权力转移**：委托返回的 WorkRecord 仅更新调用方的认知状态，绝不自动赋予调用方额外权能或解除其既定义务。
6. **单向载荷与信封隔离**：父向子传递背景必须作为只读数据字段，子向父交付结果必须作为 entry-local 证据，严禁逆向污染。
7. **复用的是 participant，不是物理 session 或上一轮执行态**：同一 Byname / dedicated role 可以连续承接多个 work unit；每个新 charge 都拥有独立的输入窗口、执行身份与完成证据。
8. **已发生的 effect 不得被后置 bookkeeping 否认**：一旦某个 work unit 已经 durable admission 并进入物理 dispatch，后续 projection/affinity/frontier 写入失败只能进入明确的失败或 reconciliation 语义，绝不能把调用结果降格成“没有放置”。
9. **Contract 不携带执行拓扑**：业务 consumer 只编译 delegation command/result、typed payload 与 capability；fold、AgentJournal ledger、sync/fork workflow、Host callback、PTY、recovery 与 OpenCode adapter 分居单向依赖的 Runtime/Composition/Adapter locality。

## 破坏后果

- **拓扑冒充业务**：调用方依赖物理 `agent_id` 或 `worktree` 识别委托，导致运行时调度调整时业务逻辑全面断裂。
- **权能越界与篡夺**：委托被误解为所有权或 Persona 的隐式置换，咨询建议被当成任务重新分配。
- **越权差遣与死循环**：Engineer 自行调用 DevOps 组织验证链，或 DevOps 差遣 Coder/Inspector 创建子代理，导致调度职责混乱。
- **认知发散与幻觉**：背景材料被当作新 assignment，导致子会话背离自身使命或重复执行父会话义务。
- **旧结果冒充新完成**：上一轮 sticky terminal 或全生命周期 WorkRecord 被下一次复用直接消费，调用方看见“新工作已完成”，实际 child 根本没有处理新需求。
- **命令/现实分叉**：Host 已经启动 child、模型已经在工作，tool 却因发送后的第二次 journal append 失败返回“无法放置”；调用方随后重试，只会撞上一个自己刚刚被告知“不存在”的 busy participant。
- **入队与执行混淆**：本机派发回执已持久化，却因异步消息身份尚未到达而报告“不确定”，把正常调度间隔冒充发送异常。放置确认不等于消息身份确认，更不等于工作完成。
- **物理拓扑偷走连续性**：把 parent delta frontier 挂在 `SessionId` 上，session replacement/recovery 后同一 logical participant 会被误当成第一次 handoff，重复或丢失背景。

## DEPENDS ON

- `session-ontology`
- `participant-identity`
- `participant-horizon`
