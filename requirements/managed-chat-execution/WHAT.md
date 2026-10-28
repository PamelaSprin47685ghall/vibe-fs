# managed-chat-execution — WHAT

## [001] 唯一 owner 与 exact execution key

`managed-chat-execution` 是持久化 managed chat execution 的唯一 owner。每个执行仅由 exact `(SessionId, PhysicalUserMessageId)` 唯一标识；单个 `SessionId` 可按顺序承载多次执行，但 session 本身不持有 current execution 终态、租约或替代身份。执行身份以已落盘的 `Accepted` 事实与模型路由（ModelRouting）持有的 exact lease 为准；不要求宿主侧二次登记身份或另立执行绑定表。

## [002] 版本化持久化事实词汇

`Accepted` 携带 provider 启动前的 `AcceptedChatExecutionEvidence`：包含 exact key、由 IdentitySeed 派生且在整个 logical run 内不可变的固定 participant 与 role（含 persona、personaCatalogVersion 与来源信息）、PromptOrigin 以及 authority 事实，禁止包含尚未发生的 ProviderRun。执行身份使用确定性的 participant 与 role，禁止引入易变或非受控的代理身份字段；provider 重试由单一连续失败预算管辖，managed-chat 恢复自身不重试。`ProviderStarted` 携带 `ProviderStartedEvidence`：包含完整 accepted evidence 以及宿主实际观察到的 ProviderRunIdentity 与 ProviderRequestKind。终态事实（Terminal）使用封闭联合体：`PreProvider` 携带 accepted evidence，`AfterProviderStart` 携带 started evidence，禁止使用布尔或可选字段拼接阶段。旧版本事实必须经纯函数逐级升级后再折叠；内存进程状态、日志文本与宿主侧易变投影均不得冒充持久化事实。

## [003] 固定准入次序与追加材料处理

普通 managed chat 准入必须严格遵循固定次序：`解析启动前身份 → 提交持久化 Accepted → 获取 exact 容量租约 → 投影到宿主 → 提交租约 → 触发 provider 动作`。成功路径上不设独立的 bind/unbind 步骤：容量所有者持有的 exact lease 一旦取得即代表该执行完成绑定。宿主实际建立 ProviderRun 后，才可生成 started evidence 并持久化 `ProviderStarted`。在 `Accepted` 落盘确认前，严禁获取容量、修改宿主消息或调用 provider；任一步骤失败均不得越过后续边界，任何环节均不得预测或伪造 ProviderRunIdentity。

同一 LogicalRun、authority root 与 IdentitySeed 的 `HumanMessage` / `BusyAgentNudge` 属于追加材料，不是新的执行任务。若已存在已提交的旧租约，先持久化 `Accepted`，再向宿主投影旧 target，此时不替代旧租约。当宿主实际选择已保存的新材料进入 provider 请求时，准入模块基于已落盘的 exact Accepted 事实，转交原容量信用、建立新 exact lease 并结算旧执行，然后方可越过 provider 边界。已经准备中的旧请求仍使用旧租约；同一请求中包含的较早且未启动的追加材料做精确结算，不另行恢复重发。

追加材料接纳后，由容量模块保留旧租约对应的信用，覆盖旧输出自然终结而新材料尚未进入 provider 的过渡窗口；具体资源交接与清理遵循 execution-model-routing-006。

## [004] Accepted 单次建立且重放幂等

针对同一 key 的等值 `Accepted` 事实重放属于幂等无操作（no-op）；若提交内容与既有 identity 或 acceptance 冲突，必须安全封闭（fail closed）拒绝。当传入状态的 key 不匹配时必须直接拒绝，不得借由终态绕过 exact key 校验；exact 终态重放不得因新提交的畸变载荷改写既有结论。新物理消息即使复用同一 `SessionId`，也必须建立全新的 exact key，不得继承前一执行的事实。`ProviderStarted` 只能在 accepted evidence 完全一致的前提下追加宿主观察到的 run evidence。

## [005] ProviderStarted 是 provider effect 的持久化前置

首次发送 provider 请求体，只能在满足以下条件后发生：exact execution 已完成 `Accepted` 落盘、已取得并绑定 exact capacity、宿主已建立 exact ProviderRunIdentity，且对应的 `ProviderStarted` 事实已落盘确认。每次观察到 provider 启动时，必须在任何故障或空闲唤醒前将 reconciler 物理游标推进到对应的 `PhysicalUserMessageId`，但不得因此额外伪造权限。`ProviderStarted` 事实将整个物理消息执行推入 provider 阶段；同一物理执行内由工具调用结果触发的后续宿主 assistant ProviderRun 直接复用该阶段，不得要求第二份准入计划，也不得追加第二个 `ProviderStarted`。等值的宿主消息更新通知重放保持幂等；终态之后到达的首次启动事件必须直接拒绝。

## [006] Terminal 单赋值且只接受类型化结果

每个 exact execution 最多只能有一个终态（terminal disposition）。启动前的 `PreProvider` 阶段只允许 `Cancelled | Rejected | Failed`，严禁 `Completed`；启动后的 `AfterProviderStart` 阶段绑定 exact started evidence，允许四种终态。相同 evidence 与 terminal 的重放保持幂等；任何与既有事实冲突的 evidence 或 terminal 竞争均直接拒绝（fail closed），不得覆盖首个落盘事实。终态决策只接受 `execution-failure-policy` 发布的封闭类型化决议，或宿主明确给出的成功/取消/删除事件；自由文本仅作排查诊断，不得承载重试、回退、熔断、容量、消息或致命性判定。

## [007] 启动前故障的精确结算

在 `Accepted` 之后、`ProviderStarted` 之前发生的拒绝、取消、删除、绑定失败或宿主投影失败，必须针对 exact key 写入类型化终态（terminal disposition）；若此前已取得容量，则必须在终态持久化确认后精确归还。该路径严禁调用 provider，严禁释放或终结同一 session 内的其他执行。

追加材料失败或取消时，仅撤销其自身保留的延续信用；若旧执行仍在运行，不得变动旧执行的资源。仅当旧执行已终结且再无其他追加材料持有该信用时，才执行此前延后的容量归还。

## [008] 恢复只在持久层激活后事件驱动

插件初始化必须保持纯接线（pure wiring）：严禁读取持久化执行记录、启动恢复逻辑、申请容量或注册会自发推进状态的后台任务。只有在持久层底座成功激活后，恢复模块才可折叠未终结执行，并完全由投影激活、容量变动、宿主事件或类型化失败事件驱动重入正常的准入或结算流程；严禁引入计时器（timer）、休眠（sleep）、超时截止期、轮询或重启计数来参与恢复正确性判定。

【程序待改】插件构造期尚有未完全解耦的初始化逻辑需待 GAP-126 补齐纯 wiring 与因果唤醒接线。

## [009] 进程本地对象永不持久化

容量租约句柄、等待器（waiter）、回调函数、队列节点、取消令牌（cancellation token）与事件订阅均属于进程本地对象（process-local artifact），严禁写入执行事实、快照或恢复令牌。系统恢复只能从持久化的语义事实重新构建新的本地对象；旧本地对象的丢失不得被解释为执行已达终态。

操作系统进程崩溃或重启不等于平滑卸载：同一持久化工作区重启后，已落盘的 `Accepted` 事实必须完整保留；旧进程持有的 exact lease、容量所有权、令牌与等待器必须全部作废消失，新进程必须经正常业务路径从持久化事实重新建立状态。进程本地对象严禁跨进程继承。

## [010] 取消与删除精确终结并排空

逻辑取消（logical cancel）与会话删除（session delete）必须枚举持久化投影中该作用域内所有尚未终结的 exact execution key，逐个发起类型化结算，并等待每个已准入执行完成持久化终态写入与 exact 容量归还后，方可宣告生命周期排空完毕。严禁按会话盲目整池释放、使用计时器宽限期或依赖轮询来推断排空完成。

## [011] 准入原子消费启动前权限事实

`Accepted` 必须原子消费已固化的准入意图（ChatAdmissionIntent）与 `interaction-authority` 发布的当前权限事实，建立包含完整版本化 `ParticipantIdentityEvidence` 的 exact `AcceptedChatExecutionEvidence`，但不包含 ProviderRunIdentity。provider 启动模块随后只能将宿主实际观察到的 ProviderRunIdentity 与 accepted evidence 组合生成 `ProviderStartedEvidence`。

这两个阶段均只逐字段投影所有者发布的事实；严禁从外部 agent 文本、会话缓存、宿主父级上下文、模型名称或旧执行中推导、补全、改写或独立缓存 participant、Role、初始 Tier、Persona 或物理 run。显式指定的外部 agent 仅作为输入并与 participant 进行一致性校验；续传保持 participant 不变，路由转换亦不得篡改 participant。此处租约绑定仅指容量所有者持有的 exact lease 身份，执行身份不要求宿主侧第二张表登记。

## [012] 恢复决策只由持久化执行与显式物理证据决定

`managed-chat-execution` 独占纯 `Evidence → Decision` 判定。Evidence 仅包含规范化 `ChatExecutionState`、宿主公开的 provider 回执观测（missing、ambiguous、absent、alive、terminal）、确切的物理资源观测、持久化提交承诺，以及 `execution-failure-policy` 发布的失败决议。Decision 属于封闭代数：`Ignore | ReconcilePhysical | ResumePreProvider | Finalize | MarkManualIntervention`。恢复系统不拥有 provider 重试或回退的解释权，严禁自发启动 provider 或生成隐式重新排队请求；所有 provider 启动后的重试与回退唯一由 `Wanxiangshu.Participant.Provider.Attempt.Fallback.ProviderRecoveryWorkflow` 裁决。每个带副作用的分支携带执行所需的事实证据，但本公式自身不执行副作用。

处于 `Accepted` 且确定外部 provider 不存在（absent）时，才允许恢复启动前准入；跨进程崩溃恢复与普通会话续传必须服从 crash-reconciliation-017 与 crash-reconciliation-018：新进程不得自动重放中断的工具或无边界自发推进执行，重启后的归位由系统在加载阶段自行完成，不存在显式续传命令。若宿主提供 `ExactAcceptedMessageRecoveryPort`，仅且仅在此端口明确接受（返回 true）时才代表 exact 准入被接管，且失败策略不再重算；若未提供该端口（生产默认 None）或端口拒绝接管，恢复决策绝不产生后台隐式消费的假象，而是生成明确、可观察的 manual 处置事实（MarkManualIntervention）。

若已观察到 provider 存在，必须先对齐已落盘的 started 或 terminal 事实。若处于 `ProviderStarted` 且 provider alive，则只保持观察；收到确切 terminal 才执行 Finalize；若确切 absent 则由 failure policy 发布 terminal 并进行终态结算，不通过 managed-chat 自行重试。若已持久化为终态但物理资源仍被占用（held），则发起精确对齐（ReconcilePhysical），否则幂等忽略。回执缺失、回执模糊、持久化或资源状态未知、缺乏策略授权时，一律安全封闭为人工介入（MarkManualIntervention）；陈旧的外部证据不得改写当前执行。严禁将 Role、错误文本、自由文本、空闲时长、计时器、进程运行时间、游标或进程本地容量状态作为恢复判定依据。相同的输入 Evidence 必须始终产生一致的 Decision。

## [013] 可靠性查询与诊断生命周期

未终结执行（`Accepted without Terminal`、`ProviderStarted without Terminal`）以及每个 `LogicalRunId` 的物理尝试次数，只能从只读规范投影 `ChatExecutionProjection` 导出。查询返回不可变的进程本地快照，严禁写入持久化事实、终结执行、授权重试/回退，或读取诊断计数器作为恢复依据。

Recovery 产生的 manual intervention 登记只投影自 `PluginRecoveryScope` 持有的诊断状态（如 `ManualChatInterventions()`），不复制或额外伪造其状态；manual intervention 记录是纯只读报告与现场处置事实，绝不是进程间恢复命令。当 exact execution 达到真实终态时，其对应的 manual 诊断登记必须立即被撤销（`RevokeManualIntervention`），禁止已失效的请求永久驻留诊断快照中。

【程序待改】文档历史所称的 PendingChatRecoveryOwnership 命名在代码中对应为 ManualChatInterventions / RevokeManualIntervention，接口语义需统一收敛。

## [014] 现场证据采集与重放无正确性特权

现场证据包（Incident envelope）必须版本化、确定性序列化，且仅包含规范化的序列化 ChatExecution 事实与状态、不可变的容量快照与对齐决议、因果诊断记录、公开宿主版本与契约证据、类型化恢复观测与决议。采集过程必须复用所有者投影并拒绝未知字段；诊断模块负责清除凭据、路径、调用栈以及 prompt、文本内容与载荷数据。重放过程必须重新折叠规范投影、重跑容量对齐与恢复运行时，遇到数据篡改、未知版本或字段、缺失证据、不受支持的宿主契约或观测不匹配时，必须安全封闭（fail closed）报错。

重放仅返回类型化的所有者副作用请求；严禁向账本写入事实、清除计数器、释放资源栅栏、改动队列与容量、执行重试或回退，重放操作本身绝不具备覆盖系统规则的特权。相同证据包的重放必须保持幂等。若宿主未提供重放已受理物理消息的确定能力，必须升级人工排查，禁止直接重发消息或手工注入虚假状态。

【程序待改】目前 exact-accepted-message 物理重放能力尚无正式宿主能力接线，依赖人工排查与升级处理。

## [015] Provider 启动边界拒绝的精确定夺

宿主转换层在 provider 启动边界拒绝某个 exact execution 时（例如尝试计划冻结失败：`durable-journal-unavailable`、`physical-user-message-missing`、`attempt-plan-freeze-failed`、`frozen-attempt-plan-missing`、`accepted-execution-missing`、`accepted-execution-already-terminal`、`authority-evidence-invalid`、`persistence-failed`），该拒绝必须作为一次类型化启动前故障（pre-provider failure）报告给结算模块；报告携带 exact `(SessionId, PhysicalUserMessageId)` 与诊断码，自由文本仅作排查，不决定终态处置。

该物理标识必须在消息投影改写前直接从本次真实宿主输入中获取，并贯穿尝试计划冻结、ProviderStarted 确认及拒绝报告全过程。合成消息 ID、消息摘要或投影后的最后一条 user 消息不得替代真实物理标识；严禁读取会话当前 ID 为旧请求冒领身份。

- 当投影中该 exact key 处于 `Accepted ∧ ¬ProviderStarted ∧ ¬Terminal` 时，结算所有者写入类型化启动前终态 `Failed`，并在持久化提交确认后精确归还该执行持有的 exact 容量租约；定夺即为终局，同一 key 的重复报告保持幂等（已终结为 no-op；终态已存在但容量仍被持有时只发起精确释放）。
- 当投影中不存在该 exact key（`AcceptedExecutionMissing`）时，严禁伪造执行记录或虚假结算，亦不得登记人工介入；保留原始拒绝与诊断码，交由准入模块处置。
- 严禁以会话级整池释放、计数器减一、空闲或时间猜测来代替精确结算；严禁调用 provider；严禁终结同一会话内的其他执行；原始拒绝异常仍按宿主 hook 既有失败路径上报，不得被内部定夺吞没。
- 持久化提交结果未知时，保留显式未知状态，不释放资源；排查诊断信息不得影响定夺结论。
