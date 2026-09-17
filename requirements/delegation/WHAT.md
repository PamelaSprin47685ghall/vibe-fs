# delegation — WHAT

## DELEG-001: 委托 = 语义 charge + entitled office + 逻辑 owner + bounded 返回后果

一项委托必须同时明确四项要素：交接的 charge（语义任务）、允许被委托方产生的 office 后果、工作的逻辑 owner、以及返回给调用方的 bounded 后果。委托的识别依据是被委托方的权能后果，而非 persona 名字或特定工具白名单。

## DELEG-002: 同一 Office 的 authority 不变，calling 别名不具有独立路由权威

属于同一 Office 的不同 calling 别名（如历史 fast 与 deep 别名，仅作为向后兼容参数保留，不具有独立路由权威与额外权限）不改变该 Office 的权能与权限；每个 Role 对应单一确定 Persona，不以 calling 别名扩权或形成多重档位。

## DELEG-003: 独立 road 与 same-road continuation 硬区分，各占独立工具

委托区分新独立道路与既有道路续做，两者是不同的工具契约：`fork` 必填 calling（Manager 仅限 `engineer`）创建新独立道路；`resume` 必填 name 续做既有道路（包括续做既有 Engineer 道路，或调用 Manager 道路唯一绑定的固定 DevOps，后者的 name 恒为常量 `devops`），复用该 person 的完整历史与已绑定配置，传入 calling 是类型化拒绝。同一目标的后续阶段、纠正、重试均属同一道路，不因工作量大或阶段演进而另建新道路。

## DELEG-004: 不同 contract 必须不同名

语义不同的委托契约必须使用不同工具名（如 Orchestrator 的 `commission` 代表独立集成道路，Manager 的 `fork` 代表派出独立 Engineer，`resume` 代表续做既有道路或调用固定 DevOps）。同一工具名在全系统命名唯一且确定性的语义契约。

## DELEG-005: 机器拓扑永不进入委托面

委托接口的参数与返回结果严禁包含 `SessionId`、`AgentId`、`ManagerJobId`、`worktree`、`reused` 等物理拓扑标识。穿透 horizon 的只有语义后果与 bounded WorkRecord。

## DELEG-006: fork 成功仅 Byname 承接 charge；续做沿用已绑定 binding

新建 fork（Manager 仅限 Engineer）成功后果仅体现 Byname 承接 charge 的语义事实。续做时按 Byname 识别既有 participant（包括固定绑定的 DevOps），严格沿用已绑定的 execution binding 与模型档位，禁止篡改已绑定的深度或实现。

## DELEG-007: SyncDelegate DAG 有环即错，收敛为 Sphinx 程序内部同步 Engineer 调研

同步委托依赖关系必须构成严格有向无环图（DAG）。撤销活跃业务角色间的任意同步委托（如旧 Inquiry/Coder/DevOps 到 Inspector，DevOps 到 Coder 等）；保留并收敛为 Sphinx 程序工作流内部在需要语义事实时同步调用只读 Engineer 调研。被调用的只读 Engineer 仅调研现有本地事实，完成本次调研即返回程序调用点，不具备文件写入、真实执行、DevOps 差遣、Fission 或递归调用权限。

## DELEG-008: sync batch 成员与顺序由 Host tool-call 集合决定

同一 assistant 运行中指向同一 SyncDelegateRole 的所有同步调用构成单一批次；其成员构成与执行顺序完全由 Host tool-call 列表决定。不得依赖到达时序或微任务调度猜测批次边界；拼接后的 charges 与 prompts 仅触发一次批次发送。

## DELEG-009: serialization key = immediate caller ReuseScope；同 key 至多一个 active batch

同步委托的串行化作用域为直接调用方的 ReuseScope。同一 key 下同时至多存在一个活跃批次；在前一批次完成前到达的新请求直接拒绝。不同层级的嵌套委托各占本层 scope，互不阻塞。

## DELEG-010: delegate 模型绑定由系统调度与有效配置决定，禁止自选 target

委派绑定的模型与执行配置由系统调度统一映射（历史 tier 及 fast/deep 仅作为向下兼容参数，不赋予额外权限与模型选择权），模型不可自选目标 target。复用既有 child 时严格沿用其已绑定 managed agent。

## DELEG-011: 无 return 通道；ordinary completion 结束 batch

同步委托不设独立 return 工具通道或双重 await。被委托方的普通 Assistant completion 即宣告批次结束，由宿主将其物化为 `includeOpening=false` 的 bounded WorkRecord 返回给调用方。

## DELEG-012: 同步返回 = canonical 得 WorkRecord，siblings 只引用

同步批次内仅首个 canonical 调用方接收完整的 bounded WorkRecord 正文；其余 sibling 调用方仅接收指向 canonical 结果的简要引用，避免重复复制。

## DELEG-013: Join 消费 owner 可用 completion，有界批次、稳定排序、逐项 CAS

Join 仅消费当前 owner 的可用 completion。批次受全局上限约束，成员保持稳定排序并逐项 CAS 消费。子到父交付的完成项必须以 entry-local 的 WorkRecord 形式呈现，严禁以字段式 DTO 封装。

## DELEG-014: commission 批量 join 具备相同有界性

Orchestrator 针对 commission 道路的批量 join 遵循严格的 FIFO 排空与与标准 Join 相同的批次上限约束。

## DELEG-015: join 中断是 Interrupted，不是 ForkError

Join 等待遇外部用户输入、操作员取消或超时终止时，产生 `Interrupted` 状态而非业务失败错误。外部输入仅打断当前等待，不取消 child 执行亦不剥夺权限。

## DELEG-016: horizon 是 pull-only snapshot

`horizon()` 是按需拉取的瞬时快照，严禁建立后台轮询、订阅或自动推送。快照仅反映当前在场名册与各 child 最新的 durable 工作记录。

## DELEG-017: 返回结果只改变 caller 认识，不自动转移 authority

委托返回的 WorkRecord 或建议仅作为调用方决策的证据输入，不自动改变全局请求的推进方向，不授予调用方额外权能，亦不免除其既定义务。

## DELEG-019: fork child 首 prompt 是 typed 语义载荷，不是自由文本

fork child 的初始提示词是类型化渲染的结构载荷，严格区分 Assignment（指令任务）与 CommissionerRecord/Attachment（只读上下文数据）。父到子方向的上下文必须作为 TOML 数据字段包裹，子到父方向的完成项必须作为注释式 WorkRecord，严禁方向混淆。

## DELEG-020: 委托语义不依赖当前工具名

委托机制绑定的是规范的语义合同，而非特定物理工具名称。工具名称的演进与替换不影响本合同定义的权能、所有权与生命周期规则。

## DELEG-021: fork attachment 只附背景，不转移 charge / authority

fork 携带的 attachment 仅将指定同伴的历史工作记录作为只读数据字段注入新任务的首 prompt，严禁将附件中的未竟工作转化为被委托方的任务义务，亦不克隆其 authority。

## DELEG-022: delegator 可给 callee 一个 advisory expected_tool_calls 估算

委托调用方可提供可选的建议性 `expected_tool_calls` 估算值。该数值仅用于校准被委托方的认知与规划，真实工具调用逐次递减计数至零饱和。该估算绝非硬性预算，计数归零不得阻断执行、改变权限或触发异常流。

## DELEG-023: 委托失败仅在所有恢复路径耗尽后向调用方报告

被委托方在执行过程中遇到单次尝试失败时，属于局部瞬态故障，不得立即向父调用方报告失败；必须等待子会话内的所有恢复重试路径完全耗尽或会话确定性终结后，方可向调用方交付最终失败。

## DELEG-024: reusable delegation 由 logical route 上的一次 direct-CE invocation 定义

复用既有 participant 发起新工作时，每次调用都是宿主 F# CE 中的一次独立 invocation，不建立 durable `Stage/Phase/ActiveWorkUnit` 或第二状态机。invocation 属于稳定 logical route：resume continuation 由 Byname 标识，SyncDelegate 由 caller scope + dedicated role 标识；物理 `SessionId` 只是本次执行目标，不拥有 handoff 连续性。

work unit 的输入窗口为该 route 上“上一已完成 work unit 的 parent frontier”到本次 admission 时 parent XTrace head 的 delta LifecycleWorkRecord；route 首次 work unit 使用当前 parent LifecycleWorkRecord（含 Opening）作为初始背景。prompt = 本次新 charge + 该 parent record。**同步**委托必须等待本 work unit 自己的完成，并只返回 callee 在本 work unit XTrace 范围内的 bounded delta LifecycleWorkRecord；**异步** `fork` 与 `resume`（后者即 same-road continuation 与固定 DevOps 调用）只负责原子 admission + dispatch，成功即返回“该 Byname 已承接本次 charge”的放置后果，绝不等待 callee completion、绝不直接返回 WorkRecord。fork 与 resume completion 的唯一 pull 边界是 `join` / `horizon`。

## DELEG-025: work unit completion 由 causal identity 决定，不由订阅时刻决定

terminal 能完成或失败 work unit，当且仅当它属于该 work unit 实际接受的 Authority Root / provider execution。每个 admitted work unit 的 completion cell 是单次赋值：第一个因果有效的 proven terminal 唯一获准 claim，恢复 continuation 不得 claim，任何晚到的成功或失败 terminal 均为幂等 no-op。`Completed`、`Failed`、`Aborted` 的 run-scoped 终结都必须保留这一 causal identity；只有真正的 session-wide 物理故障才允许没有 Authority Root。历史 sticky terminal、晚到的上一 provider run、旧 completion cache 即使发生在新订阅之后也不得完成或失败新 work unit。时间先后可以作为 transport 优化，绝不是 completion identity。

## DELEG-026: effect truth 只能沿 direct CE 单向前进

新 assignment 的业务流程必须直接写成 F# CE，且 effect truth 单向前进。同步委托为 `prepare → dispatch → await own completion → checkpoint completed handoff`；异步 `fork` 与 `resume` 必须同步确认交接（等待接收方确已完成身份校验与接收持久化），异步执行工作，completion 与 WorkRecord 由后续 `join` / `horizon` 独立消费，fork/resume invocation 本身严禁跨过 dispatch 去等待 child provider/run 完成。物理 dispatch 之前的 durable claim 复用 `PromptAuthority` 已有事实，不另造 delegation program-state。确定未发送或已证实被拒的 dispatch failure（如 Host 拒绝、Hook 拒绝）必须作为本次调用失败并释放预留，不留下运行中任务；仅有 transport receipt（Submitted）未获接收方确立接收时，不得向调用方宣告已承接；仅当接收方签发 exact 接收证据（AcceptedAssignment）后，fork/resume 方可返回成功，且仅有此类已接收 assignment 允许发布为 join 可等待的运行任务。Host 对 prompt acceptance 给出 unknown 或确认超时时，durable Pending claim 继续拥有恢复权：保留原 PromptKey 防止重复投递、不得伪造 terminal failure、不得自动重发；调用方必须得到明确的“可能已接受、不要另起重复委托”未决后果（DispatchUncertain），且严禁将未确认投递作为运行中任务交给 join 死等。route 的 parent frontier 只能由“本次 interaction 已完成”的 durable fact推进。调用方因此永远不会同时得到“无法放置”与“后台其实已经启动”两种互斥现实。

## DELEG-027: 新 assignment 不得伪装成 busy nudge

同一 logical route 同时至多一个 active work unit。已有 work unit 未终结时到来的新 assignment 必须明确拒绝；`BusyAgentNudge` 只允许作为既有 LogicalRun 的内部 continuation，不得承载新的 fork/resume/synchronous charge。前一 work unit 已完成后，同一 participant 必须立即可承接下一 work unit，无需依赖 join 消费或重新创建 participant。

## DELEG-028: Delegation contract/runtime 编译闭包必须按 effect 边界分层

`Delegation.Contract` 只拥有 command/result、fact、typed payload、logical route、completion evidence 与 injected capability；`Delegation.Fold` 只实现纯投影；`Delegation.Ledger` 仅在 composition 边界把 fold 连接到 canonical `AgentJournal`；`Delegation.Sync.Runtime`、`Delegation.Fork.Runtime` 与 `Delegation.Recovery.Runtime` 消费 contract/fold 并实现各自 CE。Host callback/dispatch 与 PTY 分别位于 `Delegation.Host.Adapter`、`Delegation.Pty.Adapter`，只能由 composition root 绑定。Contract 的 direct/transitive ProjectReference closure 不得包含 Host、OpenCode tool、PTY、process、EventStore runtime、sync/fork workflow 或 recovery runtime。

`Delegation.Contract` 的 transitive production `.fs` 不得超过 100。fold/runtime/sync/fork localities 的 transitive production `.fs` 以 185 为 target；adapter locality（`Delegation.Host.Adapter`、`Delegation.Pty.Adapter`）按其 charter 必须组装 durable spine、wait、host signal 与 managed-agent 物理 vocabularies，closure 由此被上游共享 spine 主导，其闭包以全仓 production `.fs` 的 60% 为 hard ceiling（超过即 full fallback，不伪装为 focused compile），实测规模作为回归 ratchet——任何增长必须伴随本条修订；fold/runtime localities 的闭包由 delegation 自有 contract 分片与少量 foundation/identity vocabulary 构成。owner compile 必须把选定 locality 的 transitive ProjectReference 扁平化为唯一编译计划，单次调用的 Compile Include 必须保持拓扑偏序；`build.mjs` 严禁退回目录通配。

## DELEG-029: Delegation runtime、PTY 与 durable outer routing 必须单向注入

Delegation 领域 constructor 只产生 `DelegationFactCases`、`ExecutionFactCases` 或等价 typed intent；linkage、estimate、handoff frontier、child handle index 与 closed rejection 由 delegation-owned pure fold/decision 拥有。durable composition 是把这些 case 包入 `AgentFact` 并组合 projection 的唯一位置。Host runtime、recovery、fork、fold 与 sync 只消费 delegation-owned append/query/wait/clock/PTY capability types；PTY adapter 不得读取 `HostForkRuntime`、Fork runtime、Process implementation、Gate、Dictionary、registry 或 TCS。Temporal、CausalWait 及 Process physical implementations 只由 composition 构造最窄 capability 并注入。

## DELEG-030: delegation fatal escalation 只消费 mandatory injected fuse

handoff checkpoint 或 sync invariant 失败由 delegation owner 构造 typed incident；caller 必须先保留已发生 effect 与 durable settlement，再调用构造时必填的 fatal capability。fork/sync runtime 不得直接引用 fatal physical adapter，不得用 optional/default/global fallback；同一 incident 只允许一次 report 与一次 kill。

## DELEG-031: reusable completion checkpoint 以 closed settlement 收口，不以 string 失败

`CheckpointCompleted`/`CheckpointCompletedHandoff` 返回 `HandoffCheckpointSettlement`（`HandoffCheckpointCommitment`：`Committed`、`NotCommitted`、`Unknown`、`PhaseConflict`），恒带 exact parent + route identity。WriterUnavailable → `NotCommitted`（已知未写）；WriteUnknown → `Unknown`（pending-evidence，不自动重试、不重发）；frontier fold cut（retreat/negative）→ `PhaseConflict`（确切 invariant 违例）。`NotCommitted`/`Unknown` 不改写已证成的 child 完成：SyncDelegate 照常交付已赚得的 WorkRecord，fork 照常交付 proven completion，绝不重跑已完成 child；`Unknown` 由下一次 invocation 重读 durable frontier 收敛。仅 `PhaseConflict` 经注入 fuse 熔断。Handoff capability 缺失是构造期事实（`PrepareHandoff` 未产出 prepared 即无 checkpoint 可写），不在 completion 路径上再 fatal。

## DELEG-032: Engineer 完成即返回，禁止跨角色与向后委托差遣

Engineer 负责本地事实调查与源码读写实现，本次工作完成或到达需要 Manager 决策的边界时立即向 Manager 返回，不得自行组织验证链，严禁直接调用、包装或转发任务给 DevOps。DevOps 同样不得创建或差遣其他工程子代理。需要运行验证或进一步组织工作时，由 Manager 统一调度。
