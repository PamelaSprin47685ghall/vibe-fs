# managed-session-lifecycle — WHAT

## [001] Attached 生命周期单一 Owner

所有 Attached 会话的创建、恢复、注册、级联取消与回收由单一 lifecycle owner 负责。各 AttachmentKind 只提供参数与终态策略，不另建生命周期框架；durable chat execution transition 委托 `managed-chat-execution`。

## [002] 关联先于首 Prompt

子会话的 SessionAssociation 必须先持久确认，才可发送首个 prompt，使首次交互即可判定分类。并发的首次 ensure 单飞：同一子会话只创建一次，重复请求复用同一创建结果。

## [003] 恢复只认确切关联

重启时仅复用与 journal 的 SessionId、agent、title 恰好单一匹配的 Attached 会话。无关联则新建并挂到 family root，不收养无关联会话；属性冲突、多候选或查询失败均 fail closed，不猜测、不创建。

## [004] Reusable 与 OneShot

Dedicated 会话在同一作用域跨调用复用，调用完成不销毁。OneShot 每次新建，完成即终止并释放；两种生命周期不混用。

## [005] ReuseScope 绑定

Dedicated 绑定键为 `(OwnerReuseScopeId, Role)`。同一 scope 内每个 Role 至多一个活动会话，兼容续问按此键复用，不同 scope 隔离。

## [006] Handle 四态

Handle 仅有 `Active | CompletedAwaitingJoin | Retired | Abandoned` 四态。Retired 与 Abandoned 为持久终态，不可恢复为活动状态。作废（`ChildWorkVoided`）不是第五个 handle 态：它结清该 handle 的未解除义务，并要求 join 以 `voided` 报告一次；退役不得先于这一次报告发生，否则报告无处送达、父方的义务在名册里静默消失。

## [007] Completion 单赋值

成功终态、发送失败与取消竞争同一 completion cell，首个事实获胜，后来者不能覆盖。

## [008] Consume 与 Retire

join 消费完成结果时原子写入 HandleRetired 墓碑；提交未确认不返回 payload。完成事实在重启后仍仅投递一次。作废结算的成员同样只报告一次：join 以 `voided` 报告该次作废，随后退役；已退役则不再报告，重启不重复。

## [009] 父取消等待子收束

父会话获授权逻辑取消后，为全部所属活动子会话持久写入 HandleAbandoned，并等待每个子会话物理中断和清理完成，才发布父终止。

## [010] HostOwnedHidden

HostOwnedHidden handle 对父会话的列表、等待、视图及恢复不可见；持久记录只供宿主审计与恢复，不由历史角色名称决定可见性。

## [011] 永久丢失才替换

关联子会话已确认永久丢失才允许替换。替换原子进行，次序为：建立新子会话 → 持久关闭旧关联 → 建立新关联，不出现双活动关联。

## [012] Child Run 与父记录分离

子会话的忙碌、空闲、中断、关闭由独立物理生命周期管理，与父工作记录解耦；父记录不充当子完成事实。

## [013] Durable re-enlist

重启仅依 durable HandleLinked 与完成数据重建生命周期，过滤隐藏 handle，不使用旧内存或猜测补状态。

## [014] Dedicated 生命周期

Dedicated 随 OwnerReuseScope 存活，仅在 scope 显式关闭时清理释放，父会话单轮迭代退出不结束它。

## [015] Handle 身份稳定

Agent 子会话的 handle 就是其运行时 Agent ID；重启后同一 handle 仍绑定同一子会话实体。

## [016] Attempt Interrupt 与 Logical Cancel

内部控制只能中断子会话的当前 attempt，不因此逻辑取消或级联销毁。自动化机制不得任意中断用户根会话。

deliver 可凭已落地的退休事实终止残余尝试：交付已成立（提交面事实）后，本次请求在 transform 钩子被交付判定拦下——抑制下一次 provider step、清空本次请求的消息、中断本次 attempt。物理中断不在 deliver 工具内部发出，deliver 也不等 Host 中断回执才返回。有继任者的 Relayed 不发此中断；第一次确认也不中断。该判定只命中「登记时那次物理输入仍是当前物理输入」的交付，旧交付不得停住新输入或后任。「中断已发出」与「已观察到中断成功」是两个独立事实：中断未确认不推翻已提交的交付，失败必须如实呈现为「中断未确认」，不得用交付回执的成功文字代替停止保证。work-011 的 S4 Delivered 属于这种完成停止，不是任意取消。

真实外部物理用户输入只允许打断 `join` 等待，不得中断当前 LLM 输出或其他工具，也不得调用 Host `AbortSession` / `InterruptAttempt` 排空当前 attempt。新输入作为下一次 LLM 请求的材料，由 Host 在当前输出或工具自然结束后纳入；当前物理 assistant 与工具结果仍须正常保存。

work-049 的 `ask` 是模型主动请求的人类等待，不是「新用户输入到来即中断」：它在自身工具结果得到 Host 确认后中断当前 attempt，保留 logical run 与工作资源。用户随后回复仍走上一段的普通人类输入准入，不再发一次 interrupt，也不因此注销任期。

准入 owner 以 durable `Accepted` 和 owner-issued 的 `HumanRoot` / `HumanMessage` evidence 精确接纳新输入。已有同一 run 的活跃租约时，`HumanMessage` 与 `BusyAgentNudge` 先持久接纳并投影已有目标，保留旧 exact lease，供当前输出、工具及已准备中的请求自然完成；Host 的下一次 provider 边界实际选择可见新输入后，才交接 exact lease 和原 capacity credit，不重新派工或抢占第二份容量。容量 owner 的 supersession 不授予物理中断权。同一 session 的准入投影与交接由一个 owner 串行处理，容量 pending 的完成等待不阻挡后来的输入入场；旧执行按 `Superseded` policy 精确结算，迟到旧回调不得取消或释放新执行；同次请求已包含、但未单独启动 provider 的较早追加材料也须精确结算，不留悬空执行。新输入不逻辑取消根会话、不级联 children，不自动发起新 mission 或工作记录；原 authority root、LogicalRunId、participant 与 obligation 延续。`PendingPromptIntent`、Guard、retry 与 HostInternal 也不得借此中断根会话。

## [017] 中断必须有后继

自动恢复的内部中断发起物理 abort 前，必须已有唯一 successor；没有 successor 则形成明确 Failed 终态并唤醒父等待。Failed 携当前物理用户消息提升所得的 Authority Root；缺失则 fail closed，不退化成 session-scoped/rootless 终态。work-049 的用户等待不属于失败重试：用户等待事实就是当前暂停的依据，下一步来自真实用户回复，不得为它制造 Failed、重试 successor 或逻辑关闭。

## [018] Abandon 需要不可逆授权

HandleAbandoned 表示 child 不会再沿当前 handle 返回，只由已确认的 logical parent/session 终止或 child 永久丢失证据授权。Attempt/TurnAborted、provider failure/retry、Fission、插件卸载、进程 shutdown 或 runtime dispose 均无此权限，只结束当前观察者/attempt 或 detach 本地资源，保留 durable Active handle 供恢复与 Join/Horizon。未知停止信号不升级成 ParentCancelled。

放弃原因（`HandleAbandonReason`）的合法集合是四项：`ParentCancelled`、`DeadlineExceeded`、`HostSessionGone` 与 `Voided`，也是 `wanxiangli/handle-lifecycle` 的 `recoverChild()` 接受的 `durableReason` 全集。`Voided` 表示该 child 工作已无法交付、以作废结清父方的未解除义务；作废不是完成，不写完成正文。两类作废事实都走 abandon，没有例外，差别只在于被 abandon 的对象：对 scoped 工作，其具体工作单元格转为 `Abandoned(Voided)`，该份工作义务就此终态，底层 handle 绑定仍可复用（它标识可再次参与的参与者身份）；对无 scope 历史，被 abandon 的就是 handle 绑定自身，转为 `Abandoned(Voided)`。两种情形都是 join 报告恰好一次 `voided` 之后退役；报告之后该 handle 经 `durableLifecycleByname` 的 `summary` 投影读出 `Retired`。结算按孤儿幂等：重启的进程不得再次作废同一份孤儿工作，每个孤儿一生只产生一条作废事实。

## [019] Cancel/Delete 精确排空与会话收束

获授权的 logical cancel/delete 等待 `managed-chat-execution` 确认作用域内全部已准入 execution 均 durable terminal 且 exact capacity 已归还，才发布完成。lifecycle 不复制执行状态机、不 blind release、不用 timer/deadline/sleep/polling 推断排空；process/plugin shutdown 仍只 detach。

会话收束（宿主 `SessionDeleted` 触发的 Main 收束，或 `PluginSessionScope.ClearSession` 覆盖的自删）在 Main 自身义务之外，还覆盖仍属于当前本体的 linked Attached InternalLeaf：对每个 leaf 名下已准入 execution 逐个结算——未终态的先写 typed terminal（pre-provider 阶段用 `PreProviderSettlement.settle`，after-provider-start 阶段用 `ManagedChatProviderLifecycle.terminal`，均等 durable 提交确认），已 terminal 但容量仍 held 的直接请求 exact release（`ModelRouting.releasePhysicalExecution`）。只允许 exact per-execution fence 释放；`AlreadyApplied` 与 `StaleFence` 按幂等接受，`Conflict` 不得吞没。归还完成后该 execution 不得再出现在 shared capacity 快照中。

收束同时取消该作用域名下全部 retained continuation input（execution-model-routing-006 在删除链上的落地），并完成由此解锁的旧 exact capacity 归还：取消以 exact `admissionOwner.ReleasePhysical` 路径回放挂起的旧 key 归还，不得用 `ReleaseExecution`/`ReleaseSession` 等 force 路径，不得触碰其他 session 的保留或无关 pending demand；最后一份保留撤销后旧 credit 立即归还，borrowed credit 不退休 lender。

收束链上任一步失败只隔离该步：Main 自身的结算、保留取消或释放失败不得跳过 linked leaf 的对应义务；`ClearSession` 失败后 `PluginRuntimeScope.DisposeSession` 仍执行其后续清理组（registry 条目、quiescence、join-interrupt、recovery scope、attempt plan、parked sites 等）；首个真实失败在其余义务完成后重新抛出。重复收束幂等：重放不产生第二份 terminal、第二次释放副作用或重复异常，已归还容量的 execution 再次处理为 no-op。

## [020] Fresh identity 先关闭旧 run

复用 SessionId 安装新身份前，依次完成 exact execution settlement、受权 child drain、`interaction-authority` 的 durable exact prior-run closure。closure 事实只由官方窄口写入（共享 Journal 的 `appendAuthorityLogicalRunClosed`，由 Manager 唯一 `review` 的 `Completed` 后置 stop effect 依 `completedClosureId` 触发）；`Relayed`、Plan 或缺少 exact retirement 时不得清除身份。association removal、detach、idle、timeout、Host observation、restart 都不能替代；缺证或 run 不匹配则拒绝。

## [021] Lifecycle fatal

Session delete、turn observation 或 strength semantic-cut incident 先完成其要求的 exact settlement、child drain 与 durable closure，再使用 composition 注入的 mandatory fatal capability。detach 不授权 fatal，不直接使用 physical adapter 或 optional/default/global fallback；同一 incident 至多一次 report/kill，不伪造 session terminal 或 child abandon。

## [022] Inspector finalize 的结算边界

Finalize 恒带 exact Inspector identity，commitment 闭合为 `Finalized | NothingToFinalize | NotCommitted | Unknown | PhaseConflict`。Bookkeeper 不可用或 store 写失败为 NotCommitted；archive 已提交但 index refresh 失败为 Unknown，不重做 archive；重复 finalize 为 PhaseConflict。仅 Finalized/NothingToFinalize 释放 identity；其他状态保留以便恢复后重试。Session deletion 先取得结算证据再决定 identity 去留，不在 finally 无条件删除。该收尾边界不授权创建新的活跃 Inspector。

## [023] 旧身份只收束、不升权

新任务和新子会话仅接纳当前合法活跃身份。历史事件原样保留，旧身份解码隔离在历史边界（`LegacyParticipantName` 拒绝），不自动升级为 Engineer 或获得写入/Fission 权限。运行或重启发现遗留旧角色活跃会话时，按受权 cancel/drain 或 retirement 显式写终态并排空资源，不自动恢复成合法活跃运行链。

## [024] 固定 DevOps 恢复

同一道路只有一个 DevOps 逻辑操作员权威；故障恢复可替换物理 Session，但活动 handle 拒绝第二个物理绑定，同时至多一个物理权威可执行。沿用初始化绑定的模型和 Persona，不借 resume 重置或切换。恢复不重复未决物理命令。真实进程及 PTY 随会话、道路关闭彻底排空，不留孤儿进程。

## [025] DevOps 每次工作返回即排空 PTY

固定 DevOps 每次 run 终态结算，立即收束其拥有的全部 PTY：TERM 后等待真实退出，必要时升级 KILL，随后清除记账。不得留到下次 resume，不因 Manager 退休触发，不影响其他会话的资源和生命周期。

## [028] Child 终态结算必须可观察

child run 已在 journal 中证实的 Terminal 事实必须总能转化为一次可观察的 settlement。结算路径的任何故障都不得让父 join 陷入无界等待，也不得只靠 shutdown 取消收场。

交付视图读取故障时降级为空视图继续结算，已完成的 child 仍写入可 join 的 `CompletedAwaitingJoin`，owner 的交付视图不因读取失败丢失。结算本身故障时，该 run 的唯一 completion promise 以显式 joinable 失败解决，join 消费到明确 outcome（完成、失败或取消），不静默、不搁浅 Source。PERSIST 形态：Source 结算为 PERSIST 失败而 durable work 仍 Active 时，join 经 pending Source 浮现该失败，不挂等 journal revision 前进；未知 durable commit 不放行新工作，durable work 保持 Active，不得伪装成成功或按完成释放。结算故障落为显式可 join 的失败事实并保留原始证据，供恢复后重试。

## [029] 已确认的子代理自然停机即可完成父 Join

Engineer/DevOps 的自然停止必须使父 join 取得一次真正可消费的完成事实：宿主确切观察到相同物理请求的 assistant message.updated（完成时间、合法 stop/length/content-filter 结尾）以及有 ProviderStarted 证据且 durable 提交成功的 ChatExecution.Terminal(Completed) 后，用现有带代际和在途工具检查的 SessionQuiescenceGate permit 唤醒 Reconcile；不额外要求另一个可能丢失的粗粒度 session.idle，后续 session.idle 幂等。

此规则只处理已证实的精确当前 Completed。旧物理 parent、superseded run、Cancelled/Failed、未完成的工具、没有匹配 ProviderStarted 的伪消息均不得产生完成事实；含 `time.completed` 的消息不充当 ProviderStarted。终态归属与持久写入仍由 Host/Fork owner 验证，不按时间或进程存活猜测完成。路由侧以解码所得的完整 `(SessionId, PhysicalUserMessageId, ProviderRunId)` 识别同一生命周期回调，不从当前 Session 猜 run；真实 Start 与独立送达的 Terminal 至少其一成立即可回调。
