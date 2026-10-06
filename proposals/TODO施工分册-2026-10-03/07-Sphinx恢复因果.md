# N06-B1-A2：让恢复 successor 属于实际触发它的 call

状态：R0真实恢复夹具、R1观察资源/source交接及R2三producer有限矩阵已验收；20项实际恢复场景通过，旧宽匹配已删除。相邻Unknown Root Pending与typed序号清理也已验收，Manager真实出口复用exact owner；gen184相关246/246、1290pass/0fail、28skip/137TODO，全静态/Fable与freshness通过。最终证书见[本轮记录](../archive/2026-10-06/Sphinx真实恢复与资源前置-2026-10-06.md)。下一入口转[真实Host接手卡](08-Sphinx真实Host接手.md)，不重新认领装配、lease或三路归属；WHAT[delegation-025]、Fork整体GAP及真实Host义务仍未闭合。

B1-A 已有限验收 observed API、receipt 与 physical acceptance 分离、普通 exact terminal、fallback 守门和异常结算；D0 已取得真实 callback-empty 红灯并修复注册条件，gen159 正式238/238、1178/0，全静态通过。证据见[Detached 通知记录](../archive/2026-10-05/Sphinx恢复通知前置-2026-10-05.md)。下一步将它接入 Sphinx 的 Host 执行前，必须先处理本卡的来源归属和资源交接；不能把有限证据升级成完整 continuation 因果证明。

## 1. 已确认的问题与规范依据

WHAT[delegation-025] 要求：terminal 属于该 work unit 实际接受的 Authority Root/provider execution；旧 run、sticky terminal、旧 cache 不能结算新 work unit，订阅后的到达时序不能代替身份。WHAT[delegation-023] 保留 provider-owned retry 装饰器的所有权；本卡不调整 retry 预算或 provider policy。

本轮修复前，`SyncDelegateRuntime.belongsToCall` 有两个成功分支：

1. turn physical 等于该 call 的 AcceptedPhysical。
2. root 相同，并且全 session 的 AcceptedContinuationIds 在该 physical 下为 ProviderRetryAttempt、DegenerationGuard 或 InteractionRepair。

第二个分支只有 continuation 分类，没有 assignment 归属。复用 child 时，ManagedDelegationAssignment 可以延续同一个 active root；先前 call 已接受的 retry/guard physical 仍在该 session 的 map 中。本轮gen179取得16个旧/晚successor业务红后删除此分支，当前只认该具体call真正接受的physical集合与root。

2026-10-06 gen176/gen177 已由真实生产retry夹具取得普通与fallback两个正式业务失败；A原terminal以相同字节重放，旧宽匹配误结算同child/同root的新B。原gen175异字节重放触发PERSIST-010，不计归属红灯。新夹具明确受控exact-stop边界，真实Host义务仍保留。

## 2. 每个 producer 拥有的证据、丢失位置和最窄修改

| Producer | 已有 source 身份 | 丢失位置 | 需要修改的 contract | acceptance 返回路径 |
| --- | --- | --- | --- | --- |
| ProviderRetryAttempt | SyncDelegate 传入的 ReconciledTurn 有 SessionId、PhysicalUserMessageId、AuthorityRootUserMessageId、ProviderRun；policy authorization 有 exact failed ProviderRun | Fallback/Workflow 的 continueWorkMain 把 GateContinuationOutcome.Sent key / AlreadyAdmitted 合并成 RetryVerdict.Dispatched；PluginSessionWiring 又转为 Ok unit | SyncDelegateRetryPort.Retry 接受本 call 绑定的 acceptance observer；continueDelegateCallAfterConfirmedFailure 和 WorkMain send 路径显式传递该 observer | HostSessionNudge 的 gate wrapper 把 observer 传给已有 SendGateNudge onAccepted；只在真正 PhysicalAccepted 后通知 |
| InteractionRepair | InteractionRepairWorkflow.sendRepair 已拥有完整 source ReconciledTurn，包括 physical、root、ProviderRun；idle quiescence permit 属于该精确上下文 | trySendIdleGateRepair → trySendIdleGateContinuation → admission wrapper 未携带 observer；SendIdleGateNudge 把 onAccepted 硬编码为 None | 在 effect 前用 source turn 绑定具体 call；沿 idle gate wrapper 和 SendIdleGateNudge 传递 observer | 复用 SendContinuationWithDigestAttempt 的现有 onAccepted，而非从 Sent/receipt 宣告 successor 已接受 |
| DegenerationGuard | LoopSensor 的 armed anomaly / ConsumeAbortCause 已有 exact SessionId + expected ProviderRun；HostTurnObserver.abortCauseOfTurn 同时有完整 source turn 的 physical 和 root | LoopSensor 的 continueSession / HostSignalBootstrap.continueFn 只传 session、kind、directory，providerRun 和 source physical 在 plug 上丢失；生产 guard 使用普通 text digest，不是前驱 run digest | 将 HostTurnObserver 已有的 exact source physical/root 沿 ConsumeAbortCause 的 continuation 交接带到 guard sender，或在 consume 前绑定本 call observer并随该次任务传递；不要在 continueFn 中按 session-current 补 source | HostSignalBootstrap 仍用现有 sendContinuationResult，但把该具体 source 的 observer 传下去；Detached sender 必须能在 PhysicalAccepted 时通知 |

建议先读并冻结上表 contract，再分工。先改公共签名和 opaque observer，再改三个 producer 与 Host 接线，最后改 terminal 判定；避免中途删除 generic branch 造成合法 guard 被拒。

## 3. call 本地资源与建议接口

本卡的推荐方向是 effect 前绑定具体 call，而非 terminal 到达后猜 predecessor。

call 本地保留它真正接受的 physical attempts 集合。初始 assignment 在现有 accept 回调加入；successor 仅由该 call 因果绑定的 acceptance observer 加入。这个集合是 live invocation 的外部 effect 资源，不是 durable Phase/Stage/ActiveWorkUnit，不产生第二状态机或第二 fold。

已冻结的低层交接在既有SessionContract owner；retry plug使用同一typed observer：

```fsharp
type ProviderAttemptSource =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      ProviderRun: ProviderRunIdentity }

type ContinuationAcceptanceObserver =
    { Notify: PhysicalUserMessageId -> unit
      AttachDisposable: System.IDisposable -> unit }

Retry:
    ReconciledTurn
        -> ContinuationAcceptanceObserver option
        -> ExecutionFailure
        -> string
        -> Task<Result<unit, string>>
```

实际physical仍由canonical managed PhysicalAccepted owner发布，SDK receipt不授予该事实。未引入另一套AcceptedContinuationEvidence或durable执行状态。

具体call的绑定接口已经接入生产：

```fsharp
BindContinuationAcceptance:
    source: ProviderAttemptSource -> ContinuationAcceptanceObserver option
```

它检查 source physical 属于该具体 call，root 与该 call 接受的 root 相同，随后返回捕获该 call 对象的 closure。closure 不再根据 delegate session 查当前 call。source 属于旧 call、未知 physical、错误 root 时不能拿到新 call 的能力。

observer 必须在发送 effect 前取得。即使 receipt 尚未返回、confirmation 尚未完成，callback 仍只能更新原 call；原 call 被取消或完成后，旧 callback 不能把 successor 加到之后复用 child 的新 call。可以忽略已结束 call 的通知，也可以仅更新原已退休对象；绝不能转移到新对象。

terminal 判断最终只认该 call 的实际 accepted attempts 和已接受 root。不要用最新 physical 相等代替集合，否则合法 current retry/repair/guard 会被拒。

只返回 successor physical 的 Task 不足以完全解决顺序：acceptance 和 terminal 可能在 await 返回前到达；应让 actual acceptance observer 先发布这个事实，再完成等待。RetryVerdict 仍可表达装饰器 continue/terminal 结果。

## 4. 接线时必须额外核实的真实边界

### 4.1 Detached acceptance 通知前置已完成

原 SendClaimedContinuation 和 SendAgentOwnerRootCore 只在 `AwaitMode.Await, Some callback` 时注册，Detached 的 callback 被忽略。D0 已把 register 与 confirmation waiter 分开：Some callback 在两种模式都于 Host 调用前注册，waiter 仍仅属于 Await root；实际通知点仍由 canonical managed acceptance 持久成功后拥有。gen157 六个真实业务红、gen159 八个新用例及相关238文件复绿证明这个有限能力。不要重复施工此条件。

D0证明SDK未决时早返回、OwnedSettled不构成落地、Unknown保Pending后实际接纳通知，以及明确Refused后原key拒绝晚接纳。其后D1已用正式业务红灯修复共享waiter timeout和callback抛错阻止等待者结算；最终gen166相关238/238、1186/0。callback原异常传播，finally仍结算真实Accepted。SDK分类、registration lease、call来源及其Dispose仍未证；不要按本节旧线索重做D1。

本轮register已返回每次独立的opaque lease；具体call与Fork live scope在Host effect前接手，Close只释放自己。Unknown保durable Pending，其他waiter仍由实际Accepted结算。此资源合同与D1确认等待分开，不另造执行registry；primitive、实际Send和captured Fork Runtime的正式证据分别记账。

### 4.2 AlreadyAdmitted 与 Pending 不等于 Accepted

GateNudgeAlreadyAdmitted 为 pending 或 accepted 都返回 true。重复调用不得发第二个 prompt，但也不能凭 AlreadyAdmitted 将 physical 加入 call。

已 accepted：用 exact gate occasion 的 GateNudgeAcceptedPhysical / AcceptedDispatch 重读真正 physical 并关联同一个原 call。

尚 pending：继续由原 PromptKey 的 actual acceptance 路径发布；保留 unknown 的 Pending，不自动重发。若原 owner 不再有 observer，需要先明确如何恢复这个原 key 的 observer，不能另造 physical 或覆盖属于另一 invocation 的 callback。

Dispatcher.ObserveGateNudgeAcceptance只读原exact profile/kind/run occasion：Accepted核原PhysicalLanding后通知；唯一Pending原key附实际lease；缺失或歧义不造证据。通知只捕获原call。每key仍只有一个当前registration，但旧lease不能移除后来注册者，重复同一callback也有新token。

### 4.3 pop 必须保留具体 call 所有权

popIfAcceptanceMatches现跨await后调用TryPopExactCall，以对象身份核对仍是原call；旧batch Dispose也先核原invocations，不能移除后继batch。关闭旧call的acceptance资源不授予B，也不伪造accepted root。

## 5. 正式 fixture 前提与 red 位置

现 SyncDelegateSurface.dispatchRetryAttempt 只 `Readiness.Mark(child, "provider-retry-attempt")`，scriptRetry 只脚本化 decorator verdict。这两个入口没有 retry Claimed / Submitted / PhysicalAccepted，没有新 retry physical，不能拿它们证明 A2。

本轮独立进程Harness已装配真实PluginRuntimeScope/PluginBloggerScope、隔离ModelRouting配置与生产shared stop fence，同一journal/dispatcher和实际HostTurnObserver/LoopSensor factory。实际producer/send产生原key，再由原managed ingress接纳不同physical；没有直接塞AcceptedContinuationIds。exact-stop仍为受控能力，不等于真实Host终端订阅或GAP-139关闭。

建议先在 delegation 的 025 正式文件增加因果用例；若接手时另一个 owner 仍修改025，由一个 owner 手工合并。031只放它自己要求的 frontier/单次交付证明。每个 NNN 文件的测试标题必须且只能使用对应唯一 WHAT 锚点；跨条款关系写 README 和本卡，不在一个标题并列025/031。D0 gen158 的 requirement-system/017 正式失败已证明这一门禁，随后只改标题复绿，业务断言未动。测试名称直接描述旧 successor 与新 work，不声称覆盖整个 requirement。

新 facade 缺失/import 错误不是 business red。先生成 test-only 的冻结 artifact，使 facade 能制造真实 canonical accepted successor；生产 belongsToCall 保留旧实现，再跑正式 runner 证明业务断言失败。

## 6. 最小正反例矩阵

每种 producer 至少做同一个三步因果矩阵，不仅只测拒绝：

1. call A 初始 assignment 真实接受；从 A 的 exact source 发 successor，真实 PhysicalAccepted 产生不同 physical。同 root 的这个 successor terminal 成功交付 A 的正式文本。这个正控阻止纯 physical equality 的错误修法。
2. call B 在同一个 child、同一个 root 接受新的 ManagedDelegationAssignment。A 的已接受 successor terminal 此时再次到达：返回 false，B 仍 pending，B 的 observedCompletion / WorkRecord / handoff frontier 不得变成 A 的内容。
3. B 的 initial physical 或 B 自己真正触发的 successor terminal 完成 B；正式文本和 provider run 身份均为 B，child 数量仍为一。

针对 effect 前绑定，另加一个晚 acceptance 反例：为 A 发出真实 pending successor claim，但未确认 physical；A 结束/取消后 B 已接受新 assignment；再确认 A 的原 PromptKey 新 physical，并发送其 terminal。B 仍不能被它认领。这阻止按订阅时间、acceptance 时间或启动时 snapshot 排除旧 id 的伪因果修补。

保留 receipt-only 不可成功、wrong root、ordinary old physical、writer failure fallback 的本批已有回归。至少让同一个旧 successor 经过普通 HandleTurn 和 fallback 两入口证明不可结算 B；第二次路径不得由于第一次错误已把 call 消耗而虚假 green。

成功测试必须有 own successor 的不同 physical，不得继续使用 latest initial physical 作为 retry 的正控。

## 7. 不能代替因果证据的现有 lookup

- AcceptedContinuationIds：只提供分类，无 assignment 来源。
- root 相同：复用 child 的新 ManagedDelegationAssignment 可延续同 root。
- physical 在 invocation 开始后接受 / terminal 在订阅后到达：WHAT025 明确禁止时序代替身份。
- XTrace StartCursor / frontier：是 WorkRecord 窗口，不是 successor 的 source identity。
- ModelRouting.tryProviderStepIdentity：由权威 Host start 写入，单次 lookup 是真实关系；但 retireCurrentExecution 删除该 session 的历史 run，不足以承担跨 assignment/冷恢复的完整追溯。不能 fallback 到 session-current physical。
- ChatExecutions.ByKey.startedEvidence：每 physical 只保建立它的那个 ProviderStarted；同一 physical 可有多个 provider step，不能以它猜所有 predecessor run。可以核对已知 exact key，不能散扫来造历史 oracle。
- ProviderFailureProjection：只有有限 32 项 dedupe keys，成功时清空，没有完整 provider-run→physical 关系。
- PhysicalLandings.PayloadDigest：retry gate digest 携带 failed ProviderRun 是真实线索，但没有现成完整 predecessor lookup；DegenerationGuard 当前只有普通 text hash，更不包含这一因果轴。不要在 SyncDelegate 私自解析 opaque payload string 造新的协议。
- PromptKey 存在 / Submitted receipt / RetryVerdict.Dispatched：均不等于 actual PhysicalAccepted，也不等于它属于新 call。

目前未发现一个现成统一 owner 接口能无修改地解决三个 producer。共同底层 SendContinuationWithDigestAttempt 已有 actual acceptance callback，但上游有上表的 source 丢失和 wrapper 丢失；必须修这些因果交接。

## 8. 本轮已执行顺序摘要（回归依据，不作新认领队列）

1. D0/D1已完成有限验收。R0已补同owner/config/shared fence的真实恢复夹具，取得合法successor正控及第6节归属业务红灯；generation、freshness与排空证据均保存。缺API/配置不是业务红。
2. R1由一人冻结具体call observer、source key、successor evidence与本地registration lease合同；核对active WHAT023/025/026/031及guard exact-run规则，迁移Send和Fork真实调用方，不新增retry预算或rank政策。
3. R2在合同冻结后接三个producer，包括Detached、AlreadyAdmitted/Pending和晚callback。每路完整正反矩阵成立后才删除root+kind推定分支，不能以只认initial physical取绿。
4. 跑 delegation 对应 023/025/031 与实际新增跨包边界的 dispatch、interaction repair、degeneration guard 定向套件。保持 runner 原预算，不通过放大 timeout/改变 worker 数掩盖问题。
5. 更新施工计划：只标 A2 矩阵证明的范围。Host 接线、真实 provider 端到端、所有恢复/取消/冷恢复的总体 TODO 仍按实际剩余边界记录。提交前确认没有 draft 或额外生成物进入仓库。

R0/R1/R2已按以上顺序有限验收，下一批从08卡B1-H0认领。回归依据主要文件：

- src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fs（belongsToCall / settleFailedAttempt）
- src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs（具体 call 资源）
- src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs（delegateRetryPort）
- src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs（sendRecoveryContinuation / continueWorkMain）
- src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs（sendRepair）
- src/Wanxiangshu/Interaction/Dispatch/OpenCode/SessionNudge.fs（gate / idle wrapper）
- src/Wanxiangshu/Interaction/Dispatch/Send.fs（SendClaimedContinuation / SendGateNudge / SendIdleGateNudge）
- src/Wanxiangshu/Interaction/Dispatch/PhysicalAcceptance.fs（现有单 key callback）
- src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs（exact source turn）
- src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs（guard continueFn）
- src/Wanxiangshu/OpenCode/Host/LoopSensor.fs/.fsi 与 SessionContract.fs/.fsi（guard exact-run 交接）
- src/Wanxiangshu/Interaction/Authority/Model.fs、Run.fs、ProjectionQueries.fs（actual accepted landing）
- src/Wanxiangshu/OpenCode/Host/ModelRouting.fs（exact run lookup 的所有权与清理边界）

最初记录于2026-10-05 gen152冻结期间；D0在gen159、D1在gen166有限验收。本轮R0/R1/R2已接通并按新证据结算，下一批从08卡的B1-H0前置开始；本文件不改变现行WHAT。

## 9. D0之后的接口冻结与分包（198e8251e只读复核）

本节保留本轮施工前的接口调查明细，不是当前API清单。第2项已由D1完成，其余方向在本轮R1/R2落实；当前实际签名、资源与证据见第3/4节及本轮记录，不按下列历史候选重复施工。

1. **先定资源合同。** register的三个真实owner是Send root、Send claimed continuation和Fork/Host/RunLifecycle的Unknown reattach。下一包可在现PhysicalAcceptance模块内为每次注册返回opaque lease，每次都有不同token，即使callback实例相同也不能共用身份；Dispose只compare-remove自己的callback，幂等、不碰waiter、不写journal、不调用Abandon。不要新建registry。Sender必须在Host effect之前把实际lease交给具体call scope，不能在Detached返回时用`use`释放。Fork reattach必须同时迁移，不能留下按全keycancel清其它owner的路径。
2. **等待与通知异常：D1有限验收完成。** 每invocation独立TCS，短waiter timeout不取消长waiter或observer；callback原异常传播而finally仍给等待者exact physical。gen163六业务红、gen165定向36/0、gen166相关1186/0保留正式证据。这一等待资源与callback registration仍分开，后续lease.Dispose不得取消waiter、改journal或伪造Refused；不重复施工已修问题。
3. **再定低层source载体。** 原生四字段为SessionId、source PhysicalUserMessageId、source AuthorityRootUserMessageId、source ProviderRunIdentity。Guard的SessionContract处于低层，只依Foundation.Identity的source record和窄opaque handoff；不得反向引用高层SyncDelegate/Dispatch、偷渡完整ReconciledTurn或obj。类型名和签名在此冻结后才改fsi，不把本节候选当成现存API。
4. **冻结exact occasion读取。** Dispatch owner从原projection返回未准入、原key Pending、原key actually Accepted的原生证据，accepted核对精确PhysicalLandings。保留RunGateNudgeOnce的single-flight；不自行解析payload或另建flight。retry的recoveryAlreadyAdmitted会在redispatchAfterFailure提前返回Superseded，只改fresh sendRecoveryContinuation会漏接这条路。同key不证明同call；缺原call证据时明确未确认，不转移给新call、不重发。
5. **接具体call binder及retry。** source在effect前核对所有权，返回只捕获该具体call的observer；实际accepted才加入它的attempt集合。lease Attach和call Close由同一个owner排序，已结束后Attach立即释放；accepted已取出callback时退订不能撤回在途通知，closure仍核对自己的call/resource存活。随后一次下传RetryPort、PluginSessionWiring、ProviderRecoveryWorkflow、SessionNudge，fresh与AlreadyAdmitted两路共同验收。
6. **接普通repair。** 在sendRepair仍持有完整turn时绑定，观察能力随原quiescence permit穿过trySendIdleGateRepair/idle wrapper/SendIdleGateNudge；保留最后physicalsend处TryConsume及definiteNotSent才release的政策。没有SyncDelegate call的普通repair仍按原owner执行。Blogger repair是另一生命周期，留在原路径，不顺手统一。
7. **接guard。** armed/activeInterrupt只有session/run，没有physical/root。HostTurnObserver必须在ConsumeAbortCause之前从完整turn绑定source，再沿原StartContinueWork/ContinueWorkerTask/owned proxy/runOwnedWork传到HostSignalBootstrap.continueFn与原Detached sender。保留exact-run、task ReferenceEquals、interrupt await和finally FinishContinue。不得在continueFn补查latest physical，或在Consume返回后才绑定，此时effect已启动。
8. **最后删宽匹配。** 三个真正生产入口各完成第6节矩阵及晚acceptance反例后，才删除same-root+历史kind分支；不可先只认initial physical破坏本次合法retry，也不可保旧分支作兼容兜底。ordinary HandleTurn和fallback分别证明不污染B，合法B仍可完成。原key冷启动缺同call持久证据不授予新call。lease、等待/异常、来源接线、真实Host和冷恢复分别更新状态，整A2/B1/GAP仍按实际范围结算。

每包验收都先保存实际红灯和正控、冻结input、Fable构建、相关正式套件与freshness，再同步本卡状态、证据和未证边界。新API缺失只能记能力前置，不能算业务红；标题仍保持NNN文件对应的唯一WHAT锚点。

## 10. D1完成：确认等待资源与通知异常

[D1正式记录](../archive/2026-10-05/Sphinx确认等待隔离-2026-10-06.md)保留gen163旧业务6个正式红灯、gen165修复后的3/3、36pass/0fail、1TODO，以及最终gen166相关238/238、1186pass/0fail、28skip/129TODO。每invocation独立TCS；timeout只释放自己，真实accepted/rejected/cancel按原key取固定snapshot广播。callback异常原样传播，finally仍给等待者exact Accepted。007采用long先注册、short后注册及journal原字节不变，避免FIFO清理取巧；004核对原异常对象与已经落盘的managed Accepted；009保留双waiter明确拒绝正控。

第9节第2步已在上述有限范围完成，不再重做共享TCS timeout或callback finally。D1本身未实现lease或修改call来源；这些能力在本轮R1/R2另行验收，不能借D1旧证书销项。

## 11. 本轮R0/R1/R2实施顺序与回归依据

以下顺序已在本轮执行，保留why/what与停止线；它不是下一批待领队列。正式红、修复与剩余边界见本轮记录，下一施工转08卡。

### R0：同一真实owner内的恢复夹具（有限交付）

1. **实际managed acceptance。** 现SyncDelegateSurface持有真实journal、dispatcher和capturing Host port，可扩展现registered surface，不另造业务Runtime。其canonical order为1345/1346，DispatchSurface为1499/1500，不能直接引用后者或重排逃避边界。由原PromptIngress.resolveDecision与harness.Dispatcher.AcceptManagedChatIntent兑现exact key/physical，并公开原managed projection的只读结果。保留当前普通fixture，不能把原AcceptPhysicalRoot/脚本确认冒称managed execution Accepted。
2. **实际Host资源。** 当前ToolRuntimeScope不实现IBloggerRuntimeHost；真实ProviderRecoveryWorkflow需要PluginBloggerScope等既有资源。先核其构造、写者、Host port和Dispose边界，证明与上述journal/dispatcher是同一实例。禁止临时加一个“万能scope”或复制恢复决策。
3. **配置与ModelRouting。** 完整恢复会读shared ModelRouting；current()在未初始化时明确拒绝。initialize读取HOME/.config/opencode/wanxiangshu.mjs，可能bootstrap。必须复用既有isolated-env进程夹具预置真实配置，不能在普通unit harness污染用户HOME或把初始化缺失当业务红。相关shard公开合同已存在，但增加引用必须核DAG与canonical order。
4. **同一stop fence。** 正式Surface.create得到的局部ProviderAttemptStopFence不是生产shared实例，不能拿它解除生产fence。需从真实Host观察沿原bootstrap交接exact run/stop证据；若本切片只能注入受控exact-stop能力，明确其证明范围，GAP-139真实Host义务保留。禁止调一个不相干fence、伪造Granted或跳过失败许可。先验证不匹配的run仍拒绝。
5. **实际retry路径。** retry port调用ProviderRecoveryWorkflow.continueDelegateCallAfterConfirmedFailure，经原HostSessionNudge、SendGateNudge和RunGateNudgeOnce产生真实claim/send；不能换普通SendContinuation。Host port捕获原PromptKey，receipt不授予physical；真实ingress再接受不同physical。脚本Dispatched/dispatchRetryAttempt readiness只保原脚本测试，不算本包正控。
6. **冻结R0证据。** 正控需actual accepted initial+不同successor，重放相同失败episode必须保原key与单次send；Pending/Accepted的AlreadyAdmitted都要保留。先把新API缺失/配置前置与实际行为失败分开。只有这个夹具能稳定产生真生产trace后，才写下一步来源归属红灯。

**R0交付边界。** 先提交能力装配与原生产retry正控，附原key、不同physical、实际许可/stop和资源回收证据。随后新增025归属反例时，重新捕获并冻结完整输入，在新generation取得业务红；不能沿用上一提交的freshness身份。permission相关测试分别归provider-attempt-recovery的003/014/022，call因果归delegation/025，各标题只用所属WHAT。idle及错误run不能授权发送；同失败episode的Pending/Accepted重入仍一key一send。受控exact-stop只能说明受控边界，不能关闭真实Host的GAP-139。

### R1：冻结窄合同与资源owner（公共文件只给一人）

host-boundary/host-session-contract的OpenCode/Host/SessionContract.fs/.fsi已放ProviderAttemptSource四字段与ContinuationAcceptanceObserver的Notify/同步AttachDisposable。所有生产caller已迁移；没有Foundation万能类型、完整ReconciledTurn、obj或反向高层依赖。新增真实ProjectReference已核DAG，首次focused漏引用失败另存，不计业务红。

先定register每次新opaque token及Dispose compare-remove，再同步迁移Send两处和Fork reattach。call原资源作用域同步Attach、Close只释放自己；关闭后Attach立即Dispose。callback取出后不可撤回，必须核对捕获的具体call仍存活。D1 waiter是另一资源，不让lease碰它，也不以call-local释放调用Abandon。明确拒绝仍由原业务owner全key清理。

**R1正式验收。** 旧lease不能移除后注册者，同一个callback重新注册也有新token；重复Dispose无副作用。释放仅本地观察，Pending与journal原字节不变，其他waiter仍由真实Accepted完成。call关闭后Attach立即释放；已取出的旧A callback不能更新复用child的新B。必须观察实际sender在Host effect前交出lease，只有接口返回IDisposable不算通过。

### R2：三producer因果接线与正式矩阵（R0/R1之后）

1. 公共types/lease/具体call binder由一人先冻结；retry、ordinary repair、guard实现可再分给独立agent，不能重叠编辑公共fsi。
2. retry同时处理fresh send和recoveryAlreadyAdmitted提前Superseded；由dispatch owner给原key Pending/actual Accepted的精确证据，accepted核对PhysicalLandings。不能解析payload造第二历史oracle。
3. ordinary repair在完整source turn与原permit尚在时绑定，并保最后TryConsume和definiteNotSent才release；无delegate call的普通repair仍合法，Blogger不并入。
4. guard在ConsumeAbortCause启动owned continue task前绑定完整source，经原proxy/runOwnedWork/finally传下；保expectedRun和ReferenceEquals，不能在continueFn补latest session。
5. 每producer分别做第6节完整矩阵：A own successor完成A；复用same-root child B后，旧A terminal或旧A晚acceptance不能认领B；B own successor仍正常完成。ordinary HandleTurn和fallback各独立验收，不能第一入口错误消费后让第二入口假绿。
6. 三者全部证明后才删same-root+历史kind宽匹配。先删到只认initial会破坏合法retry，保旧分支作兜底也不算修复。冷启动缺原call证据仍未确认，不授予新call、不自动重发。

### 分包停止线

R0缺同一shared stop fence或隔离配置时，先补能力装配及正式正反例，不能靠临时mock把A2做绿。R1缺真正lease handoff时，只登记能力前置，不能称资源闭合。R2只完成一producer时，不关闭整个025/T111/GAP-153/B1；Sphinx真Host/profile/答案继续依赖完整A2。每个完成包在总计划、04分册及本卡同步状态、raw证据和未证边界，再提交推送。
