# K2-D：SessionDeleted 的实际所有权与接手边界

2026-10-07 从 `523d41de4` 认领的 D0-P 已有限完成。SpikePlugin 只提取一次原装配，public 入口返回同 Hooks；薄 Surface 借同 Scope 的原 SyncDelegateRuntime，不建立第二 runtime。022 正式前提已证 Manager HumanRoot 接纳、attached Engineer 的原 AgentOwnerRoot/seed、实际 SDK prompt、原 transform/provider/turn 完成、非空 WorkRecord，以及同 Journal 的精确 ProviderRun terminal/head、正文和 SHA256；最后才消费原 production draft。fixture 的原 dispose 外加 finally，保证拒绝时也释放额外 terminal/Journal 引用，未改变生产算法。

gen301 有限直接相关完整选集为61/61文件、377pass/0fail/4skip/33TODO（77.97s wall），outer41061 accepted=true/71.037ms，退出1仅pending；独立 native 前提1pass/0fail/0skip/0TODO、5910.435625ms、退出0。完整 Fable、175 Surface/835模块链接、完整check和局部Fantomas通过。四skip均为未启用integration，33TODO原文保留，不计通过。最终文档刷新另验，见同目录交付收据。完整SessionDeleted、Bookkeeper/Capture、身份释放和public tool自然入口均未授予验收。

## 本包失败与验证边界

原件见[本包证据目录](archive/2026-10-07/baselines/session-delete-d0p/)，按SHA256保存，不能删失败只留绿。

- gen299 首次022为4pass/0fail/0skip/1TODO，证明原完成链。随后加durable terminal/payload断言，避免只从fallback正文推断持久结算。
- gen300 的新工程漏登canonical compile-order，完整check拒绝；195/195选集为1028pass/6fail/7skip/86TODO及2failed containers，213.69s wall。五fail及两container来自清单遗漏，另一个普通fail是Host023监听启动失败；不是产品业务红。补两条compile-order登记后重新Fable构建及check通过。
- gen301 同195选集在原5000ms静默门禁处截断：155drained/10active/30queued，无权威全局summary，计数unknown；5034ms silent，最后participant-identity010不是故障归因。owned termination的initial/frozen capture又发生原`ps ETIMEDOUT`/deadline失败；outer38640后续accepted=true/127.562ms不撤销此失败，也不证明未知库存已清。另观察到Host023再次普通FAIL。没有扩大预算或重跑大选集取绿。
- Host023使用独立、无Spike import的installed canary，在进入任何产品hook前未观察到监听输出。当前binary/version/architecture检查没有发现缺失，原场景已回收、当轮完整env及Host PID未留证；原因unknown，不能猜冷启动或CPU竞争，不能改skip或放大启动超时。本包61选集和native结果只作有限前提证据，不替代该失败或195文件验收。

下一有限D0-G按原child/owner SessionDeleted、原Bookkeeper active owner与SDK SendPrompt barrier验证同Scope dispose等待；不消费前提draft，不手工stage/noteAnswer，不借scripted runtime。实际cut链仍需原shared Store的合法受控composition接缝，不能把K2-C手传另一Store冒充真实删除。

2026-10-07从bbd77cdc1只读审计；没有运行删除链验证，也未修改其生产代码。K2-C的原Boot owner/helper物理证明另见[Casebook记录](U0-A1-Casebook消费者-2026-10-07.md)，不能代替本卡。

## 不同状态归不同 owner

| 状态 | 当前 owner | 不得混淆的边界 |
| --- | --- | --- |
| active participant identity、LogicalRun/Root | Journal的PromptAuthority.ActiveLogicalRun及原authority projection | participant-identity[009]、interaction-authority[018]要求durable exact closure；SessionDeleted或association removal不能替代。 |
| 物理会话目录、parent、owned/router、quiescence | PluginSessionScope和同scope HostSignalRouter | 清物理注册不等于关闭durable工作。 |
| 已删除attached child到owner的暂存引用 | SyncDelegateStore.deletedDelegatesByOwnerScope | 源码明确仅用于child/owner删除间的draft/session cleanup，不是durable identity registry。 |
| 草稿、观察、已提交Case | SessionDraft/Lifecycle；已提交投影归EventStore | 临时pointer保留也不自动证明草稿仍在或允许Unknown后重投。 |

PluginSessionScope.DropSessionIdentity只忽略sessionId：旧每会话语言状态已退役，不能以这个no-op证明业务身份保留或释放。没有发现一个名为SessionsRegistry的新权威应当被补建。

managed-session-lifecycle[022]仍写exact Inspector finalize identity及Finalized/NothingToFinalize释放、其他状态保留；当前归档staging只接Engineer，U0又增加PersistenceFailed。先明确条款所指取证载体、原角色迁移与新settlement边界，不能根据计划措辞把所有process-local字典变成持久权威，更不能恢复活跃Inspector或ActiveLogicalRun。

普通失败后CancelSession会ClearDeletedDelegate是静态事实，但尚无已核条款认定该临时pointer就是[022]取证身份，不能据此宣布产品违约或立即修生产。Lifecycle在Bookkeeper前已tryTake草稿并Drain观察；“再调用得到NothingToFinalize”也不能成为前次Unknown已解决的证据。

## 原删除链与下一有限包D0

原hooks.event/订阅必须收到已owned的SessionDeleted，才能经HostSignalRouter进入Bootstrap。Bootstrap同步prepare并stage精确attached child，再用同一scope.RunBackground依次等待finalize、撤stop fence、chat recovery与HostSessionDeletion.handle。原event hook返回不表示删除任务完成。

PluginRuntimeScope.DisposeAsync关闭admission并等待owned-work/reconcile drain，随后清runtime/session/SyncDelegate，最后释放shared Journal。cut在finalize await交原owner并拒绝，因而不会继续handle；这个原事件全链尚未正式执行证明。

D0只认领“已接纳的真实删除归档必须被同scope shutdown等待，错误不能变成普通成功”：

1. 沿原公开plugin入口建立真正Manager Root和attached Engineer work，完成真实工作形成draft；不能只塞parent字典或直调helper。
2. 经原child/owner删除事件，用Bookkeeper原SendPrompt的deferred barrier固定归档已开始。原hooks.dispose必须等它完成，store不能先释放；另保非空decoy。
3. 放行合法路径核真实Capture与cold bytes，拒绝控制核原NotCommitted/Unknown，不造成功事实。单一回调失败须可从owned drain观察；多失败选择顺序尚需明确合同。
4. 无sleep、预算放大或概率重跑。已有保护就补基线绿，可用精确变异检验oracle，不称生产业务红。
5. 有意dispose拒绝的fixture须局部双finally，先放barrier/排空，再释放额外Journal和terminal引用，最后清目录，不能被首个await异常跳过。

开始前先核原attached binding/owned event是否可由现fixture观察。若只能第二scope/router、直接stage或自造settlement，就停在接缝方案，不伪造完整删除证明。暂不修改HostSessionDeletion、SyncDelegate Store、Casebook草稿或Authority。

## 保留边界

### 2026-10-07 原装配入口复核

PluginHooks把原SyncDelegateRuntime交给ToolRegistry，但baseSpecs没有Sphinx/同步Engineer consumer，该参数未被工具行为消费。唯一OpenCodeHostPort.Dispatch来自Sphinx OpenCode Surface自建harness，不能借给另一plugin。公开fork属于Road/HostFork，不建立HostSessionDeletion读取的SyncDelegate attached binding；也不能直接stage队列冒充公开工作。

D0-P现已按上述方案有限落地并运行，证据以本卡顶部为准。SpikePlugin原装配只生成一次原Hooks/Scope，public入口继续返回同Hooks；delete/finalize/dispose继续走原hooks，不新增第二runtime。下段是完整删除继续施工的前提，不再指示重复认领D0-P。

即使借用接缝完成，也须先证明原Manager chat admission、原Reconciler完成产生非空WorkRecord、production callbacks产生draft，以及删除时原Bookkeeper仍有active owner。不能用scripted Bookkeeper、手工noteAnswer或profile重绑补前提。薄Surface证明与public插件共用原装配，仍不等于public tool自然触发已接通；这项保留给实际consumer施工。

完整[022]的取证identity/角色和PersistenceFailed合同、重复delete与父子交错、durable exact closure、crash/跨进程恢复、全部child drain/fatal顺序、installed Host及整个knowledge013均未闭合。

另一个只读发现：RecordBackgroundFailure与StartDisposeAsync的Option.orElse参数顺序让后来失败覆盖旧失败，与firstFailure命名不符。当前WHAT尚未规定通用scope多错聚合的first优先级；此点只登记为合同待核候选，不在D0或K2-C顺手交换两行。
