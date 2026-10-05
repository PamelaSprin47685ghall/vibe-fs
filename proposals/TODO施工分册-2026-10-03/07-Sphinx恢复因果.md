# N06-B1-A2：让恢复 successor 属于实际触发它的 call

状态：A2-D0 Detached acceptance 通知前置已有限验收；三类 producer 的具体 call 因果归属仍只有调查和设计，未修复。本文不代表 WHAT[delegation-025] 已闭合。

B1-A 已有限验收 observed API、receipt 与 physical acceptance 分离、普通 exact terminal、fallback 守门和异常结算；D0 已取得真实 callback-empty 红灯并修复注册条件，gen159 正式238/238、1178/0，全静态通过。证据见[Detached 通知记录](../archive/2026-10-05/Sphinx恢复通知前置-2026-10-05.md)。下一步将它接入 Sphinx 的 Host 执行前，必须先处理本卡的来源归属和资源交接；不能把有限证据升级成完整 continuation 因果证明。

## 1. 已确认的问题与规范依据

WHAT[delegation-025] 要求：terminal 属于该 work unit 实际接受的 Authority Root/provider execution；旧 run、sticky terminal、旧 cache 不能结算新 work unit，订阅后的到达时序不能代替身份。WHAT[delegation-023] 保留 provider-owned retry 装饰器的所有权；本卡不调整 retry 预算或 provider policy。

`SyncDelegateRuntime.belongsToCall` 当前有两个成功分支：

1. turn physical 等于该 call 的 AcceptedPhysical。
2. root 相同，并且全 session 的 AcceptedContinuationIds 在该 physical 下为 ProviderRetryAttempt、DegenerationGuard 或 InteractionRepair。

第二个分支只有 continuation 分类，没有 assignment 归属。复用 child 时，ManagedDelegationAssignment 可以延续同一个 active root；先前 call 已接受的 retry/guard physical 仍在该 session 的 map 中。因此旧 call 的 terminal 具备被新 call 认领的条件。

目前只是源码因果证据，尚无真实 retry fixture 的正式失败证据。不要写成已执行复现。

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

最窄 retry plug 可为：

```fsharp
Retry:
    ReconciledTurn
        -> ExecutionFailure
        -> string
        -> (PhysicalUserMessageId -> unit)
        -> Task<Result<unit, string>>
```

类型是否需要同时携带 PromptKey / root / origin，由 dispatch owner 在实施时冻结。若引入 `AcceptedContinuationEvidence`，它必须由实际 PhysicalAccepted owner 产出，不能仅把 SDK receipt 包装成 evidence。

共享的 call 绑定能力可以是一个窄方法（名称待冻结）：

```fsharp
TryBindContinuationAcceptance:
    source: ChatExecutionKey * sourceRoot: AuthorityRootUserMessageId
        -> (PhysicalUserMessageId -> unit) option
```

它检查 source physical 属于该具体 call，root 与该 call 接受的 root 相同，随后返回捕获该 call 对象的 closure。closure 不再根据 delegate session 查当前 call。source 属于旧 call、未知 physical、错误 root 时不能拿到新 call 的能力。

observer 必须在发送 effect 前取得。即使 receipt 尚未返回、confirmation 尚未完成，callback 仍只能更新原 call；原 call 被取消或完成后，旧 callback 不能把 successor 加到之后复用 child 的新 call。可以忽略已结束 call 的通知，也可以仅更新原已退休对象；绝不能转移到新对象。

terminal 判断最终只认该 call 的实际 accepted attempts 和已接受 root。不要用最新 physical 相等代替集合，否则合法 current retry/repair/guard 会被拒。

只返回 successor physical 的 Task 不足以完全解决顺序：acceptance 和 terminal 可能在 await 返回前到达；应让 actual acceptance observer 先发布这个事实，再完成等待。RetryVerdict 仍可表达装饰器 continue/terminal 结果。

## 4. 接线时必须额外核实的真实边界

### 4.1 Detached acceptance 通知前置已完成

原 SendClaimedContinuation 和 SendAgentOwnerRootCore 只在 `AwaitMode.Await, Some callback` 时注册，Detached 的 callback 被忽略。D0 已把 register 与 confirmation waiter 分开：Some callback 在两种模式都于 Host 调用前注册，waiter 仍仅属于 Await root；实际通知点仍由 canonical managed acceptance 持久成功后拥有。gen157 六个真实业务红、gen159 八个新用例及相关238文件复绿证明这个有限能力。不要重复施工此条件。

D0 证明 SDK 未决时早返回、OwnedSettled 不构成落地、Unknown 保 Pending 后实际接纳通知，以及既有明确 Refused 后原 key 拒绝晚接纳；不证明 SDK 分类、回调抛错、取消、Dispose 或内存 subscription 释放。尤其 `PhysicalAcceptance.accepted` 先移除 callback/waiter 再调用 callback：若 callback 抛错，Await waiter 后续结算可能丢失。这是相邻源码线索，未取得正式反例，D0 没有增加吞错或 fatal 政策。

具体 call 接线前先冻结观察资源合同。现 register 返回 unit、每 key 单 callback，journal Dispose 不释放它；call 完成后捕获旧对象可避免污染新 call，但不能据此宣称资源已释放。若扩展已有 registration lease，释放须核对同一个 registration 的所有权，只解除本地观察，不将 Unknown 写成 Abandoned，也不取消其它 owner 的等待。不要另造执行 registry 或恢复状态机；正式取消/释放反例按实际合同补齐后单独结算。

### 4.2 AlreadyAdmitted 与 Pending 不等于 Accepted

GateNudgeAlreadyAdmitted 为 pending 或 accepted 都返回 true。重复调用不得发第二个 prompt，但也不能凭 AlreadyAdmitted 将 physical 加入 call。

已 accepted：用 exact gate occasion 的 GateNudgeAcceptedPhysical / AcceptedDispatch 重读真正 physical 并关联同一个原 call。

尚 pending：继续由原 PromptKey 的 actual acceptance 路径发布；保留 unknown 的 Pending，不自动重发。若原 owner 不再有 observer，需要先明确如何恢复这个原 key 的 observer，不能另造 physical 或覆盖属于另一 invocation 的 callback。

PromptPhysicalAcceptance 当前每 key 只有一个 callback，register 会覆盖；不能随意给 pending key 附加一个新 call callback。重复同一 source 的接手必须证明仍是同一具体 call，或由 dispatch owner 维护同一 observer 的幂等绑定。

### 4.3 pop 必须保留具体 call 所有权

popIfAcceptanceMatches 当前先捕获 call、await AcceptedRoot，再按 delegate generic pop。A2 引入 source closure 后，需核实跨 await 没有把另一个 call pop 掉的可能；如果存在正式可达反例，应补 exact call 比较/原子 pop。此项是相邻审查线索，尚未正式复现，不纳入已确认缺陷数量。

## 5. 正式 fixture 前提与 red 位置

现 SyncDelegateSurface.dispatchRetryAttempt 只 `Readiness.Mark(child, "provider-retry-attempt")`，scriptRetry 只脚本化 decorator verdict。这两个入口没有 retry Claimed / Submitted / PhysicalAccepted，没有新 retry physical，不能拿它们证明 A2。

现 Harness 已保存构造实际 Runtime 的原 dispatcher，且 SessionPort 保存 prompt native metadata 的 actual PromptKey。这些资源足够扩展一个有限 facade：走实际 producer/send owner，再由实际 dispatcher 接受 retry/repair/guard physical。禁止直接塞 AcceptedContinuationIds 或伪造 observed terminal。

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

## 8. 可原子推进的实施顺序

1. D0 已完成，不重复改 Detached 注册条件。先冻结具体 call observer、source key、successor evidence 与本地观察资源释放合同；核对当前 active WHAT023/025/026/031、dispatch physical acceptance 和 guard exact-run 规则。不得新增 retry 预算或 rank 等政策。
2. 手工增加窄 public fixture，证明它真的产生 Claimed→Submitted→PhysicalAccepted、原 PromptKey 和不同 physical。写上述 business red，旧业务实现下正式失败；保存 build generation、freshness 和排空证据。
3. 增加 call 本地 accepted attempts 和 exact source 绑定；同步三个 producer 的 observer 交接，包括 Detached、AlreadyAdmitted/Pending 和晚 callback。删除 root+kind 推定分支。
4. 跑 delegation 对应 023/025/031 与实际新增跨包边界的 dispatch、interaction repair、degeneration guard 定向套件。保持 runner 原预算，不通过放大 timeout/改变 worker 数掩盖问题。
5. 更新施工计划：只标 A2 矩阵证明的范围。Host 接线、真实 provider 端到端、所有恢复/取消/冷恢复的总体 TODO 仍按实际剩余边界记录。提交前确认没有 draft 或额外生成物进入仓库。

接手依据主要文件：

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

最初记录于2026-10-05 gen152冻结期间；D0在gen159有限验收。下一批按这里冻结归属与资源合同、取得真正 successor 的业务红灯，再实施三类 producer 的因果交接。

## 9. D0之后的接口冻结与分包（198e8251e只读复核）

本节是下一批指引，尚未实现、未取得正式红绿，不能另销TODO。按依赖串行冻结公共类型，再分工修改独立producer；不要三人同时猜低层签名。

1. **先定资源合同。** register的三个真实owner是Send root、Send claimed continuation和Fork/Host/RunLifecycle的Unknown reattach。下一包可在现PhysicalAcceptance模块内为每次注册返回opaque lease，每次都有不同token，即使callback实例相同也不能共用身份；Dispose只compare-remove自己的callback，幂等、不碰waiter、不写journal、不调用Abandon。不要新建registry。Sender必须在Host effect之前把实际lease交给具体call scope，不能在Detached返回时用`use`释放。Fork reattach必须同时迁移，不能留下按全keycancel清其它owner的路径。
2. **单独核实等待与通知异常。** 现awaitConfirmation共享每key TCS，但每caller有独立timeout，任一timeout会全keycancel；现accepted先移除callback/waiter再调用callback，异常能阻止waiter结算。这两项目前只有源码证据。先用薄公开接缝制造真实行为红：短waiter timeout后长waiter和observer仍能由实际ingress结算；callback抛错时durable accepted不倒退、另owner waiter仍得到exact physical。确认等待和callback registration是不同资源，不能把lease.Dispose写成cancel waiter。异常处置依原owner合同，不伪造Refused，也不在Surface吞错充成功。
3. **再定低层source载体。** 原生四字段为SessionId、source PhysicalUserMessageId、source AuthorityRootUserMessageId、source ProviderRunIdentity。Guard的SessionContract处于低层，只依Foundation.Identity的source record和窄opaque handoff；不得反向引用高层SyncDelegate/Dispatch、偷渡完整ReconciledTurn或obj。类型名和签名在此冻结后才改fsi，不把本节候选当成现存API。
4. **冻结exact occasion读取。** Dispatch owner从原projection返回未准入、原key Pending、原key actually Accepted的原生证据，accepted核对精确PhysicalLandings。保留RunGateNudgeOnce的single-flight；不自行解析payload或另建flight。retry的recoveryAlreadyAdmitted会在redispatchAfterFailure提前返回Superseded，只改fresh sendRecoveryContinuation会漏接这条路。同key不证明同call；缺原call证据时明确未确认，不转移给新call、不重发。
5. **接具体call binder及retry。** source在effect前核对所有权，返回只捕获该具体call的observer；实际accepted才加入它的attempt集合。lease Attach和call Close由同一个owner排序，已结束后Attach立即释放；accepted已取出callback时退订不能撤回在途通知，closure仍核对自己的call/resource存活。随后一次下传RetryPort、PluginSessionWiring、ProviderRecoveryWorkflow、SessionNudge，fresh与AlreadyAdmitted两路共同验收。
6. **接普通repair。** 在sendRepair仍持有完整turn时绑定，观察能力随原quiescence permit穿过trySendIdleGateRepair/idle wrapper/SendIdleGateNudge；保留最后physicalsend处TryConsume及definiteNotSent才release的政策。没有SyncDelegate call的普通repair仍按原owner执行。Blogger repair是另一生命周期，留在原路径，不顺手统一。
7. **接guard。** armed/activeInterrupt只有session/run，没有physical/root。HostTurnObserver必须在ConsumeAbortCause之前从完整turn绑定source，再沿原StartContinueWork/ContinueWorkerTask/owned proxy/runOwnedWork传到HostSignalBootstrap.continueFn与原Detached sender。保留exact-run、task ReferenceEquals、interrupt await和finally FinishContinue。不得在continueFn补查latest physical，或在Consume返回后才绑定，此时effect已启动。
8. **最后删宽匹配。** 三个真正生产入口各完成第6节矩阵及晚acceptance反例后，才删除same-root+历史kind分支；不可先只认initial physical破坏本次合法retry，也不可保旧分支作兼容兜底。ordinary HandleTurn和fallback分别证明不污染B，合法B仍可完成。原key冷启动缺同call持久证据不授予新call。lease、等待/异常、来源接线、真实Host和冷恢复分别更新状态，整A2/B1/GAP仍按实际范围结算。

每包验收都先保存实际红灯和正控、冻结input、Fable构建、相关正式套件与freshness，再同步本卡状态、证据和未证边界。新API缺失只能记能力前置，不能算业务红；标题仍保持NNN文件对应的唯一WHAT锚点。
