# execution-model-routing — WHAT

## [001] 模型选择与官方机制一致

插件不加载 `~/.config/opencode/wanxiangshu.mjs`，仓库不再提供该模板文件，也没有自定义调度、拉黑或协议加载机制。每个 agent 执行走什么模型完全由 OpenCode 官方机制决定：优先用户显式选择，其次 `opencode.json` / `opencode.jsonc` 中 `agent.<name>.model` 的配置。路由运行时在生产以无调度器模式启动，直接采用宿主解析出的目标；`wanxiangli/routing` 保留的调度器 ABI 只作为测试注入口，不覆盖宿主模型选择。

【程序待改】`wanxiangli/src/routing/RoutingProtocol.fs` 的协议错误提示仍让用户「参考 `resources/wanxiangshu.mjs` 推荐模板手工迁移配置」，而该模板文件已不在仓库里；这段提示要按现状改写或删除。

## [002] 无 fallback 策略

系统不维护候选模型池，也不做备用 provider 切换。发生错误、中断或需要重试催促时，一律沿用该 agent 原本由用户或原生配置确定的同一模型重新尝试，不切换到备选模型，不拉黑主模型。

## [003] `running` 是真实 provider capacity token multiset

`running` 准确反映系统内部实际持有的 provider capacity token 集合，基础 token 总数即 `running.length`。同一进程内所有 plugin 实例与 worktree 共享这份 module-level multiset；显式 lender 的 token 被借用方使用时只计数一次，不重复计数。进程重启丢弃进程内容量，只从显式的物理观察重建。

## [004] 普通 fresh execution demand 在 `chat.message` 准入产生；`null` = 等待，不是失败

发送或排队阶段不抢占 model slot，`SendPrompt` 保持 `Model=None`。新 execution 的需求准入点是 Host 接收物理 user message 后的 `chat.message` 边界。同一活跃 run 的 `HumanMessage` / `BusyAgentNudge` 只接纳追加材料，不产生第二份调度需求；其 exact lease 交接遵循 [006] 的可见材料边界。
调度返回 `null` 时不调用 provider、不消耗失败预算，demand 进入 pending 队列，并在 occupancy 变更时由事件驱动重算。新到达的物理 user message 或会话销毁会取消并取代被 supersede 的旧 pending demand。

## [005] 模型选择权归宿主；Runtime 不保留自定义调度策略

模型选择权完全在宿主（OpenCode）。Runtime 只维护真实执行租约的生命周期绑定与并发仲裁，不加载外部 MJS 调度策略层，也不做多候选切换。

## [006] exact lease 与 fresh execution 路由；活跃 run 的追加材料延后交接

物理执行租约与 `(SessionId, PhysicalUserMessageId)` 绑定。同一 PhysicalUserMessageId 的执行与重试严格复用已有 target 与 capacity fence，不重新触发调度，也不在同一 physical 内改变 Role、participant 或切换 agent。同一 SessionId 出现新 PhysicalUserMessageId（fresh physical execution）时原子替代旧租约，并把该 run 不可变的 canonical Role 重新路由至 target：生产环境沿用宿主目标；测试注入的调度器可以选择新 target，且仅当旧执行仍是当前活跃执行时才把其 target 作为 `previous` 传入供优先续用。角色与 participant identity 绝不改变。provider step 结束时把实际 lease target 与 exact `ProviderRunIdentity` 绑定；失败结算只可原子消费这条绑定记录，禁止从 session 当前 target 猜测失败 provider。每个 session 只保留最近一次 provider run 的目标记录，新 run 自动覆盖旧记录。租约不以 SessionId 为单位跨物理执行永久绑定；exact terminal 释放后不存在 session 级 previous 缓存。

成功建立新物理执行的 `Admitted` lease 或 `Queued` demand 才构成原子替代。调度返回 `null` 且 pending queue 已满时，新输入必须以 `CapacityQueueFull` 拒绝，保留旧 exact lease、token 和 pending ownership；不得先退休旧执行再发现无处排队。队列已满但能立即发放 target 时，仍允许直接入场，不得仅凭队列长度拒绝。

同一 LogicalRun、authority root 与 IdentitySeed 的 `HumanMessage` / `BusyAgentNudge` 若遇到旧活跃 committed lease，属于追加材料而非 fresh 调度。`chat.message` 保留旧 lease 并投影其 target；实际 provider transform 选择可见新输入后，才按旧 opaque lease 验证所有权、转交同一 token/借用 credit 并建立新 physical key 的 fresh fence。保持原 role、participant、target 与 lender，不再调用调度，不另占容量，不提前取消旧请求的 step。旧 exact 结算不能释放新 fence。

durable Accepted 之后、Host projection 之前，容量 owner 以 exact 新输入 key 保留旧 opaque lease 的 credit。旧输出自然终结时若仍有未选入请求的已接纳材料，返回 typed `HeldForInput`，不丢失已投影的模型与借用关系；下一次可见输入准入才交接并撤销本次保留。较晚的未选入材料随同一 credit 更新保留的 opaque handle，不能被较早请求结算掉。投影失败、材料精确终结或作用域关闭均须清理自己的保留；最后一份材料撤销后完成延后的旧资源归还，borrowed credit 不退休 lender。

## [007] 物理 execution identity 与终结信号释放 occupancy；session/业务 lifecycle 不拥有槽

租约释放只认确切的物理执行终结信号：无 error、completed assistant，且 `finish` 明确属于 `stop | length | content-filter`，其 parentID 匹配该 PhysicalUserMessageId。
`finish="tool-calls"` 只终结单步并归还 step token，不解除 physical execution binding；assistant error 以及 Host 将流错误归一化后的 `finish="unknown" | "error"` 也只作为单步终结信号，不直接删除 execution binding，以便 Host 继续同 material retry。业务层的 handle 完成、join 或 finality 不直接操作租约。

## [008] 官方模型配置为权威来源

宿主原生解析出的模型配置（用户会话选择优先，其次 `opencode.json` / `opencode.jsonc` 中 `agent.<name>.model`）是唯一模型真相源。系统尊重宿主选择并原样保持，不强加角色互异性校验，也不修改宿主 message 对象的 model 字段。

## [009] managed model 准入与读取分离；dispatch message 保持 model-free

所有内部 synthetic prompt 分派保持 `Model=None`。Host 接收物理 user message 后的 `chat.message` hook 获取租约，并把 `{providerID, modelID, variant}` 投影至 mutable message。后续 hook（`chat.params`、transform、tool）通过容量所有者的只读查询读取该 exact execution 已提交的租约；该查询不调用调度、不发放 fence、不 commit/release，也不因读取增加 duplicate/stale/conflict 计数。观测通过后，managed 请求只投影 `temperature = 1.0`，不改写宿主 model 对象。运行时观测不再校验 agent 或 model/reasoning 与租约是否一致：漂移真实发生时静默通过、不阻断，不再有 PROMPT-006 漂移拒绝。

[006] 的追加材料在 `chat.message` 只投影旧目标；provider transform 先调用独立的可见输入准入操作，完成 exact lease 交接，再执行上述只读查询。这不是缺失租约的通用回填：没有 exact Accepted 记录、同 run 材料或旧 committed lease 时仍须 fail closed。

## [010] provider capacity 独立成可抢占 token；只凭显式 lender 借用

ModelTarget 物理绑定与 provider capacity token 严格解耦。provider 请求发出前，由 `experimental.chat.messages.transform` 获取对应 provider 的 capacity token。
借用只能使用 acquire 输入中 `lenderSessionId` 显式指定的 lender 的 credit；无 lender 输入的 demand 只走普通容量，绝不因派生关系、ambient 拓扑或同名 session 获得信用。token 只在 provider-step 边界转移。显式召回（release/retire）须等待借用方 step 结束；但当 lender 自己的 `experimental.chat.messages.transform` 为同一 owned credit 再次进入后续 provider step 时，容量所有者必须在仲裁前回收该 credit 上任何外来 InFlight/Retiring step（transform 入口抢占召回）：回合内后代借用可阻塞属主，属主 transform 一旦触发即回收。等待中的 borrower 仍按单调序号优先于较晚的 lender owned step，不被该回收饿死。
同一 token 同时面对多个可执行 provider-step demand 时，按 demand 的单调序号选择最早者；owned、borrowed、ordinary 只决定该 demand 可使用哪枚 token，不构成调度优先级。较晚到达的 lender owned step 不得越过已等待且可借用该 token 的 child step。
Host 开始执行某个 managed tool 时，tool context 的 exact `ProviderRunIdentity` 构成 provider→tool 的因果 step 边界：在任何 capability/role 准入与 tool body 运行前，必须用当前冻结的 `PhysicalUserMessageId` 结束该 provider step，使 token 进入可借用的 idle 状态。工具体可以同步等待 descendant provider work，因此不得把 provider capacity 持有到 tool body 返回，不得以 wall-clock timeout 猜测何时释放，也不得通过借用真实仍在执行的 token 伪造并发容量。

同一物理执行通知重复进入相同 fence 的 provider step 时，opt-in 携带 `requestKey`（由容量所有者从 `(sessionId, physicalUserMessageId, fence)` 派生）实现精确幂等：若当前持有 token 的 step 匹配相同三元组且 fence 不超前，立即返回已完成 Task 复用已持有 step；若 waiters 队列中已存在相同 `(SessionId, PhysicalUserMessageId, RequestKey=Some k)` 的 demand，直接返回该 demand 既有的 Task（同一 Task 对象且 waiters 不增长）。不同 requestKey 或无 requestKey 的调用保持正常的 FIFO 排队与因果辈分。这实现 `admit(exactRequestKey)` 式精确幂等，杜绝无幂等重入 transform 造成的容量死锁。

## [011] 物理 admission 顺序固定为 accept → acquire → project

Managed chat execution 必须先为 exact `(SessionId, PhysicalUserMessageId)` durable 写入 `Accepted`（携带 IdentitySeed 确立的不可变 canonical participant 事实），随后才可排队或获取容量。一次物理消息执行的调度/acquire 严格在 durable `Accepted` 之后，顺序为：

1. durable accept：写入包含 exact session、physical message 与 canonical participant 的 durable `Accepted` 事实；
2. exact capacity acquire：将 IdentitySeed 确立的 fixed canonical Role 解析为 ModelTarget（生产环境即宿主目标；同一 physical execution 复用原 target；acquire 输入同时携带 IdentitySeed 派生的 participant 与可选的 `lenderSessionId`），并以包含 SessionId + PhysicalUserMessageId + fixed Role + Participant + target + fence 的 exact capacity identity 获取 capacity fence；
3. Host projection：将 target 投影至 Host 消息。

任一步失败只能交给 `execution-failure-policy` 结算已拥有的事实与资源；严禁 acquire-before-accept、先改 Host message 后补 capacity acquire，或让未接受的发送意图预占容量。此顺序只约束物理消息执行的普通准入，不存在只读副本的非等待预约。

[006] 的活跃 run 追加材料不预占新的容量：durable accept 后可投影已经取得的旧 target；新 exact lease 只能在可见材料进入下一次 provider 请求时交接，provider effect 前仍须完成 exact commit 与 durable ProviderStarted。

## [012] Capacity 是 exact opaque fenced capability

每次成功 acquire 返回不可伪造、单次结算的 capacity fence。
Capacity exact identity 严格为 `(SessionId, PhysicalUserMessageId, Role, Participant, ModelTarget, CapacityFence)`，严格区分本地 participant（Role/Persona/SelectedAgent 事实）与远端模型目标（ModelTarget）。
同一 physical execution 的重试严格复用已有 target 与 fence；fresh physical execution 获取对应新 execution 的 fresh fence。
capacity fence 至少因果绑定 exact session、physical message、fixed Role + Participant、target、owner lineage 与 fence epoch。borrow/recall 只转移同一 fence 的合法 custody，不复制 token。release、retain 与 transfer 必须携带 exact fence 并验证包含 fixed Role+Participant 与 exact target 的完整 capacity identity 及当前 custody；旧 epoch、重复 settlement、错误 physical id 或按计数/session 猜测释放均 fail closed。capacity settlement 的选择由 `execution-failure-policy` 输出，路由器只验证并原子执行。

## [013] Pending demand 是 bounded typed queue

`null` 或暂不可 acquire 的已接受 demand 只能进入有明确上限的 typed queue；entry 必须携带 exact `(SessionId, PhysicalUserMessageId)`、resolved target、role/participant/lender 输入与 supersession identity。队列满返回 typed `CapacityQueueFull` 并交给 failure policy，不得丢弃、无限扩容、解析错误文本后重试，或借 wall-clock timeout 清退。capacity release、exact supersede、session deletion 与 shutdown 是队列重算/移除事件；队列正确性不依赖 polling、sleep 或 elapsed time。

已确认的 provider 驱逐同样触发队列重算。每个既有 demand 按自己的 role 查询理论容量：仍有候选时继续正常调度或排队；已无候选时以与新 admission 相同的 typed `CapacityQueueFull` 结算，不等已不存在的容量生产者。其它池的 demand 与已提交 exact lease 不因此被取消或释放。普通 route 返回 `null` 不等于理论容量为 false。

## [014] Capacity snapshot 与对账只观察、绝不修复

Capacity owner 发布不可变 snapshot：ledger entry、token state、exact execution owner、pending waiter、role/participant 身份与单调 duplicate/stale/conflict transition counter。始终满足 `0 <= active <= ledger entries`，且每个 token、waiter 与 map owner 均可追溯到 snapshot 内同一 exact identity。对账是纯函数：合法状态返回 `NoOp`；任何 map/ledger divergence、无 owner token/waiter 或不可能计数返回 typed `FailClosed`，不自动补删 ledger/map、不清零 counter/config、不借时间推断。release、commit、cancel 只返回封闭 outcome `Applied | AlreadyApplied | StaleFence | Conflict`；duplicate 不二次递减，旧 fence 不触碰新 execution。

## [015] Reliability query 复用 capacity owner snapshot

Reliability query 的 queue depth、active lease 与 duplicate/stale/conflict fence 数逐字段投影 [014] 的 immutable snapshot；diagnostic 模块不维护第二份 capacity/release counter，不重算不同公式，不 reset/repair owner state，也不把 query result 反馈给 routing。`CapacityQueueFull` 观测只递增 process-local 单调计数器，只用于观测，不授权重试。

## [016] routing fatal 绑定 exact fence 结算并经注入的进程保险丝执行

fatal incident 必须携带 exact execution key、capacity fence 与 `Committed | Unknown` 结算记录；未结算、stale fence 与粗粒度 session identity 无权 fatal。routing/Host port 只接受组合层注入的 mandatory fatal capability，不直接引用进程物理适配层。同一 incident 只允许一次 report 与 kill，fatal 不修复、不清零、不释放 capacity state。

【程序待改】`Execution/Routing` 目前没有任何 fatal 接线：路由内部只有 `poison`（记录异常并 raise），`Diagnostic.fatal` 与 `FatalProcess`（进程保险丝）在 routing 中没有调用方；本条描述的注入式 fatal capability 尚未落地。

## [017] provider 恢复重投的原目标绑定与失败驱逐

provider 恢复的一次已确认失败由 `ModelRouting` 结算失败 attempt 的 exact 目标记录（见 [006]），只有两种结局：

- 保留目标：为同一 `SessionId` 写入一次单次消费的 recovery retry 绑定；该 session 的下一次 fresh admission 以该 target 作为调度偏好（优先于被原子取代执行提供的 `previous`），绑定随之被消费，无论该 admission 成功、排队还是被更新物理消息取代。
- 驱逐目标：其 provider 在进程生命周期内被 poison，后续 fresh admission 回到普通调度。

两种结局都只属于该 exact 记录：记录单次消费，重复或过期输入不产生第二结局；无记录（例如进程重启后）不产生任何结局。recovery retry 绑定不是 session 级 previous 缓存：它只由失败结算从 exact 记录显式写入、只服务该 session 的下一次 fresh admission，并在 force cleanup（`ReleaseExecution`）时清除。

provider 健康变化只参与 fresh demand 的准入与既有队列的重算。同一 exact physical execution 重放时，先核对既有 role、participant 并复用原 fence；不得把后来的理论容量不足误作该已提交准入的拒绝，也不得为此替换或释放旧 lease。

## [018] 角色集合模型路由与旧角色槽位解耦

模型调度权威以当前合法角色集合（Engineer、DevOps、Manager、Orchestrator、Plan）为唯一合法输入。调度输入若为已废除角色，系统必须 fail closed，严禁为其分配模型租约或发起 provider 物理准入。

## [019] 固定 DevOps 模型绑定持久性与禁止通过 resume 换模型

同一道路内固定绑定的 DevOps 的 ModelTarget 由道路初始化阶段确定并持久化记录（进程内缓存加 durable resolver）。
后续该道路内所有针对 DevOps 的 resume、续行或崩溃恢复，必须严格继承并复用该既有 ModelTarget，严禁通过 resume 参数或运行时策略重新分配、篡改或覆盖 DevOps 的物理模型。固定 DevOps 目标绑定只约束原 owner execution。不存在只读委托用途，也不得另立副本池覆盖该绑定。

## [020] 不存在只读委托池

只读委托用途与副本调度不是当前模型合同。加载器不要求单独的配置存在性查询，也不因任何查询存在或缺失而打开第二条调度轴。理论容量只按 Role 回答。容量暂满、provider 不可用或一次 route 返回 `null`，按既有 pending、取消与失败规则处理，不另造启用状态。
