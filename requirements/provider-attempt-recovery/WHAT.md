# provider-attempt-recovery — WHAT

## [001] 失败预算属于逻辑运行

每个逻辑运行（Logical Run）各自记录自己的连续 provider 失败次数，并固定参与者（SelectedAgent、Role 与 Persona 身份）。新运行不继承旧运行的失败计数。

## [002] 预算证据损坏时拒绝

预算只统计连续失败。落盘字节无法解码时返回解码错误并拒绝该信封；既不变成未捕获异常，也不被当作一次未知提交。

## [003] 一次失败只计一次

由唯一权威记录确认失败与耗尽。按 `(SessionId, LogicalRunId, AuthorityRootUserMessageId, ProviderRun)` 去重：最新一次失败重放同一准入，不新增事实或计数；更早的失败返回 `EpisodeSuperseded`，不启动恢复。每次重试使用新的 PhysicalUserMessageId 与 ProviderRunIdentity；一次失败至多允许一次物理发送。

## [004] 失败计账与请求种类证据

每次确认失败加一；预算未耗尽时产出一份精确的类型化失败许可。业务主工作成功（WorkMain）记录成功并清零计数。计账不改变角色，也不判定 provider 健康；目标选择归 [021]。

恢复的 RequestKind 取自匹配 `(SessionId, PhysicalUserMessageId)` 的持久 ProviderStarted，包括后续工具步骤；ProviderRun 的精确授权另行检查。普通子运行无论关联如何都保持 WorkMain。

已 Accepted 而没有 ProviderStarted 的执行，可由 accepted origin 建立 WorkMain 或 InteractionRepair。正文、失败序号、角色或会话关联都不能补出缺失的种类。

## [005] 有界的自动重试

自动重试有有限的连续失败上限（当前实现为 10 次，`wanxiangli/src/failure-budget` 的 defaultBudget）。达到上限即耗尽，不再发出自动物理请求。之后的恢复需要新的 Authority Root 或用户显式操作。

## [006] 参与者固定，模型路由独立

重试保持逻辑运行的参与者与系统提示。模型路由为该参与者选择执行者，但不能改变身份，也不能绕过耗尽。

## [007] 非法预算记录终止重放

计数不是合法后继、超过预算、或在已耗尽的运行上推进的失败记录一律拒绝；拒绝即终止重放。

## [008] 空终态与 Provider 故障统一恢复

无有效正文且无工具调用的终态（自然 stop 但正文为空或只有 XML、thought-only 无最终输出、upstream_error 等 provider 错误）与普通 provider 故障不作割裂，统一在 ReconcilePass 汇聚为待修复的未完成态（TurnNeedsContinuation）。

真实闲置（quiescence/idle）到达时，按 InteractionRepair 的 exact-terminal gate 向会话发送统一催促提示词（「刚刚你异常停止，请你继续工作。」，文案在 `resources/provider/runtime/interaction-continue/`），催促模型在同一会话中重新输出。

统一恢复受连续失败预算控制；未耗尽时每次异常停止都可催促，耗尽才判定为终端失败。人工取消（Abort / Cancelled）保持终结，不触发催促。

## [009] 宿主活动不是域失败

宿主的传输尝试序号既不进入域失败计数，也不决定预算或身份。宿主清理把在途工具调用标记为中断，同样不推进 provider 失败预算。

## [011] 不可变重试计划

每份 AttemptPlan 在渲染前依据 RequestKind 与持久材料固定重试选择，并绑定自己的物理消息。跨回调的临时状态不得决定这些选择，也不得携带恢复许可。

## [013] 换执行者不变身份

重试保持持久的参与者身份（Role、Persona、来源/版本）、SessionProviderLanguage、系统提示、CanonicalRole 与 Authority。Persona 与来源取自该运行的持久身份证据。一个参与者在一次执行中绑定一个远端 LLM 目标；换身份需要先精确结清旧运行，再开新运行。机器簿记不进入 provider 视野。

## [014] 续行需要失败许可与宿主停止

恢复续行需要三个条件：已记录的确认失败、剩余预算、以及观察到确切的失败 ProviderRunIdentity 已按 [022] 停止宿主重试。发送本身既不推进也不重置预算。粗粒度的 session.error 不能授权发送；[023] 处理已接受但未启动的重试。

## [016] 成功计账跟随已证实的 RequestKind

只有成功的 WorkMain 记录 SuccessRecorded 并清零连续失败；InteractionRepair 不计入。`finish=tool-calls` 是一次有效的 provider 尝试成功，即使宿主回合仍在继续。

续行种类需要 AcceptedContinuationIds。角色或终端文本不能把维护性工作记成业务主成功。

## [019] 只有类型化策略许可授权重试

恢复消费 execution-failure-policy 的 RetryFreshAttempt 许可（ProviderTransient 或 ProviderPermanent），绑定确切的 ProviderRunIdentity、RequestKind 与策略决定。失败账本核对当前尝试，不重新分类失败，也不重算预算。

LocalInvariant、ProtocolRejection、AuthorizationDenied、UserCancelled、Superseded、CapacityQueueFull、AcceptanceUnknown、StreamInterruptedAfterFirstToken 与所有 PersistenceFailure 既不消耗 provider 预算，也不产生重试。禁止通配重试、按诊断文本分类、把待定接受当失败。

## [020] 预算证据不是恢复授权

预算只折叠已提交的 Authority Root、类型化 provider 失败与合格的业务主成功事实；相同事实序列得到相同视图。它不含回调、续行、下一步动作、发送许可或工作流入口。每次重试仍消费自己的精确类型化失败许可，执行内容取自重试策略。

## [021] 模型选择遵循 OpenCode 原生配置，失败原样重试

角色与 agent 的执行模型与 OpenCode 官方机制一致：优先用户当前选择，其次 opencode.json / opencode.jsonc 中指定的模型。失败或催促重试时，用该确定模型原样重试，不拉黑 provider，不在后台切换候选模型池。失败预算为 10 次。

【程序待改】`src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs` 的恢复策略仍实现「同一 provider 第二次确认失败即拉黑并改试下一候选」，与本条不符，应改为始终原样重试同一目标。

## [022] 发送须等待确切宿主终态

宿主停止自动重试的唯一证据是确切 `(SessionId, ProviderRunIdentity)` 上 finalized 的 errored assistant message 投影。session.error 只触发失败定局；session.idle、其它 run 或其它尝试都不能授权发送。缺少持久 ProviderStarted 不妨碍观察该宿主终态并判定失败。

该发送栅栏（`wanxiangli/src/failure` 的 ProviderAttemptStopFence）只在当前进程内有效，不写 Journal，也不从崩溃恢复重建；没有观察就不自动发送。[023] 处理悬挂义务。中止、替换或删除 session 永久撤销其 pending 恢复发送。等待只由宿主终态事件推动，不用 timer、deadline 或轮询。

## [023] Accepted 未启动必须定夺

已 Accepted 且没有 ProviderStarted 的执行不得静默悬挂。session.idle 只检查该 session 中恰好处于此形状的执行；进程重启后的 boot 恢复扫描（`OpenCode/Host/SessionRecoveryHostSurface`）检查同样的义务。

session 级 idle 只是唤醒，不能证明该 session 所有已接受输入都已被宿主停止。在线扫描须依据公开宿主快照中最新已完成 assistant 的确切 `parentID` 确认物理执行已停止，只定夺自己的未启动义务。已被 owner 明确取代的旧 assistant idle 不定夺新输入，也不为它铸造 idle 许可；尚未保存到宿主或仍在容量排队的已接受后继由其准入 owner 和独立恢复扫描负责。

定夺必须终局：有类型化恢复能力时，用确切已接受材料恢复执行；否则将该执行结为终态并报告该回合失败。本义务不发送新文本、不生成替换 PromptClaim，也不放宽 [003] 的单次物理发送约束。
