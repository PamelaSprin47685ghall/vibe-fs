# delegation — WHAT

## DELEG-001: 委托 = 语义 charge + entitled office + 逻辑 owner + bounded 返回后果

一项委托必须同时明确四项要素：交接的 charge（语义任务）、允许被委托方产生的 office 后果、工作的逻辑 owner、以及返回给调用方的 bounded 后果。委托的识别依据是被委托方的权能后果，而非 persona 名字或特定工具白名单。

## DELEG-002: 同一 Office 的 calling 名只差 persona/depth，不差 authority

属于同一 Office 的不同 calling 别名（如 fast 与 deep 档位）仅在 persona 风格与推理深度上存在差异，不改变该 Office 的权能与权限。

## DELEG-003: 独立 road 与 same-road continuation 硬区分

委托区分新独立道路与既有道路续做。明确指定 calling 时创建新独立道路；缺省 calling 且指定已有 Byname 时，沿用既有道路进行续做。同一目标的后续阶段、纠正、重试均属同一道路，不因工作量大或阶段演进而另建新道路。

## DELEG-004: 不同 contract 必须不同名

语义不同的委托契约必须使用不同工具名（如 Orchestrator 的 `commission` 代表独立集成道路，Manager 的 `fork` 代表使命内 witness）。同一工具名在全系统命名唯一且确定性的语义契约。

## DELEG-005: 机器拓扑永不进入委托面

委托接口的参数与返回结果严禁包含 `SessionId`、`AgentId`、`ManagerJobId`、`worktree`、`reused` 等物理拓扑标识。穿透 horizon 的只有语义后果与 bounded WorkRecord。

## DELEG-006: fork 成功仅 Byname 承接 charge；续做沿用已绑定 binding

新建 fork 成功后果仅体现 Byname 承接 charge 的语义事实。续做时按 Byname 识别既有 participant，并严格沿用已绑定的 execution binding 与模型档位，禁止篡改已绑定的深度或实现。

## DELEG-007: SyncDelegate DAG 有环即错

同步委托依赖关系必须构成严格有向无环图（DAG）。允许预定义的单向委托边（如 Inquiry/Coder/DevOps 到 Inspector，DevOps 到 Coder），严禁任何反向或成环委托。

## DELEG-008: sync batch 成员与顺序由 Host tool-call 集合决定

同一 assistant 运行中指向同一 SyncDelegateRole 的所有同步调用构成单一批次；其成员构成与执行顺序完全由 Host tool-call 列表决定。不得依赖到达时序或微任务调度猜测批次边界；拼接后的 charges 与 prompts 仅触发一次批次发送。

## DELEG-009: serialization key = immediate caller ReuseScope；同 key 至多一个 active batch

同步委托的串行化作用域为直接调用方的 ReuseScope。同一 key 下同时至多存在一个活跃批次；在前一批次完成前到达的新请求直接拒绝。不同层级的嵌套委托各占本层 scope，互不阻塞。

## DELEG-010: owner effective tier 决定 delegate tier

委派绑定的档位由调用方有效 tier 确定性映射（fast 对应 fast，deep 对应 deep），模型不可自选目标 target。复用既有 child 时严格沿用其已绑定 managed agent。

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

## DELEG-017: 返回结果只改变 caller 认识，不自动转移 authority`

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

复用既有 participant 发起新工作时，每次调用都是宿主 F# CE 中的一次独立 invocation，不建立 durable `Stage/Phase/ActiveWorkUnit` 或第二状态机。invocation 属于稳定 logical route：fork continuation 由 Byname 标识，SyncDelegate 由 caller scope + dedicated role 标识；物理 `SessionId` 只是本次执行目标，不拥有 handoff 连续性。

work unit 的输入窗口为该 route 上“上一已完成 work unit 的 parent frontier”到本次 admission 时 parent XTrace head 的 delta LifecycleWorkRecord；route 首次 work unit 使用当前 parent LifecycleWorkRecord（含 Opening）作为初始背景。prompt = 本次新 charge + 该 parent record。**同步**委托（`inspect`、`establish-behavior`、`repair-behavior`）必须等待本 work unit 自己的完成，并只返回 callee 在本 work unit XTrace 范围内的 bounded delta LifecycleWorkRecord；**异步** `fork`（包括 same-road continuation）只负责原子 admission + dispatch，成功即返回“该 Byname 已承接本次 charge”的放置后果，绝不等待 callee completion、绝不直接返回 WorkRecord。fork completion 的唯一 pull 边界是 `join` / `horizon`。

## DELEG-025: work unit completion 由 causal identity 决定，不由订阅时刻决定

terminal 能完成或失败 work unit，当且仅当它属于该 work unit 实际接受的 Authority Root / provider execution。`Completed`、`Failed`、`Aborted` 的 run-scoped 终结都必须保留这一 causal identity；只有真正的 session-wide 物理故障才允许没有 Authority Root。历史 sticky terminal、晚到的上一 provider run、旧 completion cache 即使发生在新订阅之后也不得完成或失败新 work unit。时间先后可以作为 transport 优化，绝不是 completion identity。

## DELEG-026: effect truth 只能沿 direct CE 单向前进

新 assignment 的业务流程必须直接写成 F# CE，且 effect truth 单向前进。同步委托为 `prepare → dispatch → await own completion → checkpoint completed handoff`；异步 `fork` 为 `prepare → dispatch → return placement`，completion 与 WorkRecord 由后续 `join` / `horizon` 独立消费，fork invocation 本身严禁跨过 dispatch 去等待 child。物理 dispatch 之前的 durable claim 复用 `PromptAuthority` 已有事实，不另造 delegation program-state。确定未发送的 dispatch failure 可以作为本次调用失败；一旦 dispatch 已发生或 outcome unknown，任何后置 handoff/frontier/affinity bookkeeping 都不得把调用结果改写成“未放置”。Host 对 prompt acceptance 给出 unknown 时，durable Pending claim 继续拥有恢复权：不得伪造 terminal failure、不得释放本次 run 的 terminal observer、不得自动重发；调用方必须得到明确的“可能已接受、不要另起重复委托”后果。route 的 parent frontier 只能由“本次 interaction 已完成”的 durable fact推进。调用方因此永远不会同时得到“无法放置”与“后台其实已经启动”两种互斥现实。

## DELEG-027: 新 assignment 不得伪装成 busy nudge

同一 logical route 同时至多一个 active work unit。已有 work unit 未终结时到来的新 assignment 必须明确拒绝；`BusyAgentNudge` 只允许作为既有 LogicalRun 的内部 continuation，不得承载新的 fork/synchronous charge。前一 work unit 已完成后，同一 participant 必须立即可承接下一 work unit，无需依赖 join 消费或重新创建 participant。

## DELEG-028: Delegation contract/runtime 编译闭包必须按 effect 边界分层

`Delegation.Contract` 只拥有 command/result、fact、typed payload、logical route、completion evidence 与 injected capability；`Delegation.Fold` 只实现纯投影；`Delegation.Ledger` 仅在 composition 边界把 fold 连接到 canonical `AgentJournal`；`Delegation.Sync.Runtime`、`Delegation.Fork.Runtime` 与 `Delegation.Recovery.Runtime` 消费 contract/fold 并实现各自 CE。Host callback/dispatch 与 PTY 分别位于 `Delegation.Host.Adapter`、`Delegation.Pty.Adapter`，只能由 composition root 绑定。Contract 的 direct/transitive ProjectReference closure 不得包含 Host、OpenCode tool、PTY、process、EventStore runtime、sync/fork workflow 或 recovery runtime。

`Delegation.Contract` 的 transitive production `.fs` 不得超过 100。fold/runtime/sync/fork localities 的 transitive production `.fs` 以 185 为 target；adapter locality（`Delegation.Host.Adapter`、`Delegation.Pty.Adapter`）按其 charter 必须组装 durable spine、wait、host signal 与 managed-agent 物理 vocabularies，closure 由此被上游共享 spine 主导，其闭包以全仓 production `.fs` 的 60% 为 hard ceiling（超过即 full fallback，不伪装为 focused compile），实测规模（2026-09-02：host 315、pty 316、recovery 193）作为回归 ratchet——任何增长必须伴随本条修订；recovery 的闭包余量由 session-contract 快照 vocabulary 与 durable spine 的真实消费构成，185 为其持续收缩目标。composition locality 豁免规模预算。owner compile 必须把选定 locality 的 ProjectReference closure 合并为 aggregate-order、零 ProjectReference 的单一 flat project，并只启动一次 Fable。
