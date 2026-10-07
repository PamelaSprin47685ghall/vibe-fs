# K2-D：SessionDeleted 的实际所有权与接手边界

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

D0下一前提是让SpikePlugin原装配只生成一次原Hooks/Scope，public入口继续返回同Hooks，薄Semantic Surface只借同Scope.SyncDelegateRuntime调用原Engineer并只读原attached ID；delete/finalize/dispose继续走原hooks，不新增wrapper或第二runtime。这属于实现方案，不是要求用户额外审批的新流程；尚未落地/运行，不能预填绿。

即使借用接缝完成，也须先证明原Manager chat admission、原Reconciler完成产生非空WorkRecord、production callbacks产生draft，以及删除时原Bookkeeper仍有active owner。不能用scripted Bookkeeper、手工noteAnswer或profile重绑补前提。薄Surface证明与public插件共用原装配，仍不等于public tool自然触发已接通；这项保留给实际consumer施工。

完整[022]的取证identity/角色和PersistenceFailed合同、重复delete与父子交错、durable exact closure、crash/跨进程恢复、全部child drain/fatal顺序、installed Host及整个knowledge013均未闭合。

另一个只读发现：RecordBackgroundFailure与StartDisposeAsync的Option.orElse参数顺序让后来失败覆盖旧失败，与firstFailure命名不符。当前WHAT尚未规定通用scope多错聚合的first优先级；此点只登记为合同待核候选，不在D0或K2-C顺手交换两行。
