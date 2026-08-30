# managed-session-lifecycle — WHAT

## MANAGED-SESSION-001: Attached 会话单一生命周期 Owner

`AttachedSessionRuntime` 是所有 Attached 会话的唯一创建、恢复、注册、级联取消与回收所有者；各具体 `AttachmentKind` 仅提供参数与终态策略，严禁各自实现生命周期框架。该 session lifecycle owner 不拥有 durable chat execution transition；后者必须委托 `managed-chat-execution`。

## MANAGED-SESSION-002: 创建协议先写关联后发首 Prompt

创建子会话时必须遵循严格的时序：先持久化写入 `SessionAssociation` 关联记录，随后才允许向子会话发送首个 prompt，确保所有拦截器在首轮交互前即能判定其分类属性。

## MANAGED-SESSION-003: 重启恢复判据与 Fail-Closed 原则

系统重启恢复 Attached 会话时，仅在 journal 关联（SessionId + agent + title）恰好单一匹配时方可复用；关联记录不存在则执行 Replacement 新建并挂载至 family root；无关联记录一律直接新建；若存在属性冲突、多候选或查询失败，必须直接 fail closed 拒绝恢复。

## MANAGED-SESSION-004: Reusable 与 OneShot 生命周期互斥

专用代理（Dedicated Sync*）实行 Reusable 生命周期，调用完成后不销毁、在同一作用域内跨轮次复用；单次任务（OneShot）则每次新建并在完成后立即终止与释放。两种生命周期模型严格互斥，不得混用。

## MANAGED-SESSION-005: ReuseScope 为 Dedicated 绑定生命周期 Key

Dedicated 会话的绑定键为 `(OwnerReuseScopeId, Role)`；同一 scope 内至多存在一个活动 Dedicated 会话，同 scope 兼容续问复用，不同 scope 间相互隔离。

## MANAGED-SESSION-006: Handle 四态与不可逆 Terminal

Handle 生命周期严格限制于 `Active`、`CompletedAwaitingJoin`、`Retired` 与 `Abandoned` 四态；`Retired` 与 `Abandoned` 是持久化终态，绝对不可逆转为活动状态。

## MANAGED-SESSION-007: Completion Cell 单赋值与首胜规则

成功终态、发送失败与取消信号共同竞争唯一 completion cell，实行先到先得的单赋值语义，后续到达者一律直接拒绝覆盖。

## MANAGED-SESSION-008: Retire 为 Consume 的唯一写口

`join` 消费完成结果时必须原子写入 `HandleRetired` 墓碑事实；写入未确认时绝不返回有效负载，确保每个完成事实在重启视角下仅被投递一次。

## MANAGED-SESSION-009: Abandon 为 Durable Terminal 与级联取消顺序

父会话取消时必须对所拥有的所有活动子会话逐一写入 `HandleAbandoned` 事实；父会话发布终止状态前，必须异步等待所有子会话的物理中断与清理彻底完成。

## MANAGED-SESSION-010: HostOwnedHidden Handle 对父不可见

宿主拥有的隐藏 handle（如 Distiller、隐藏评审员）对父会话的列表、等待、视图及恢复完全不可见，其持久化记录仅供宿主自身审计与恢复。

## MANAGED-SESSION-011: 永久丢失替换资格与 Durable 关联显式迁移

当且仅当已确认关联子会话永久丢失时，方允许执行替换操作；替换迁移必须遵循原子时序：先建立新子会话，再持久化关闭旧关联，最后建立新关联。

## MANAGED-SESSION-012: Child Run 物理生命周期与父记录分离

子会话运行时的物理状态（忙碌、空闲、中断、关闭）由独立的 ChildRun 机制管理，与父会话的工作记录严格解耦，父工作记录不得冒充子会话的完成事实。

## MANAGED-SESSION-013: 重启按 Durable Handle 投影 Re-enlist

重启恢复时完全依据持久化的 HandleLinked 事实与完成数据重建子会话生命周期，过滤隐藏 handle，严禁依据内存残留或猜测恢复状态。

## MANAGED-SESSION-014: Dedicated 会话生命周期与 ReuseScope 绑定

Dedicated 会话的生命周期严格等同于对应 OwnerReuseScope 的生命周期，仅在 scope 显式关闭时执行清理与释放，不受父会话单轮迭代退出的影响。

## MANAGED-SESSION-015: Handle 对应 Agent ID 且重启严格对齐

Agent 子会话的 handle 即为其运行时的 Agent ID，重启后必须保证同一 handle ID 严格绑定至相同的子会话实体。

## MANAGED-SESSION-016: Attempt Interrupt 与 Logical Cancel 权限分离

内部控制机制仅有权请求中断子会话当前的物理尝试（attempt interrupt），无权触发逻辑会话取消或级联销毁，且绝对禁止主动中断根会话。

## MANAGED-SESSION-017: 内部 Interrupt 必须闭合 Successor 与 Parent Wake

任何内部尝试中断在发起物理中止前，必须确保已存在唯一的后继处理机制（如 AABB、DegenerationGuard 等）；若无后继者，必须转为明确的 Failed 终态以唤醒父会话的等待，严禁产生悬挂的孤儿尝试。该 Failed 终态必须携带当前物理用户消息提升得到的 Authority Root；拿不到当前 Authority Root 时必须 fail closed，绝不能退化成 session-scoped/rootless 终态，因为后者无法证明属于当前 reusable work unit。

## MANAGED-SESSION-018: Abandon 需要不可逆丢失授权，Process/Attempt 生命周期无权宣判

`HandleAbandoned` 是“该 child 不会再沿当前 handle 返回”的不可逆业务终态，不是 cleanup 标记。生产写入必须具有明确的不可逆丢失授权：仅允许已确认的 logical parent/session 终止（例如 session deletion、显式 successor-less termination）或恢复流程对 child 永久丢失的证明。`AttemptAborted` / `TurnAborted`、provider failure/retry、degeneration-guard、Fission、插件卸载/进程 shutdown、runtime dispose 都不构成该授权；这些路径只能结束当前物理观察者/attempt 或执行 process-local detach，必须保留 durable `Active` handle 供恢复与后续 Join/Horizon 使用。模糊或无法证明的停止信号一律不得升级为 `ParentCancelled`。

## MANAGED-SESSION-019: Cancel/Delete 通过 execution owner 精确排空

logical session cancel 或 delete 获得终止授权后，lifecycle owner 必须调用 `managed-chat-execution` 的 exact settlement barrier，并等待其报告该作用域内所有已准入 execution 均 durable terminal 且 exact capacity 已归还，随后才可发布 session 删除/取消完成。lifecycle 不复制 execution transition law，不执行 session-wide blind release，不以 timer、deadline、sleep 或 polling 推断排空。process/plugin shutdown 仍只执行 detach，不得借该 barrier 升级为 logical cancel。

## MANAGED-SESSION-020: Fresh identity 等待 durable exact prior-run closure

同一 `SessionId` 被复用于 fresh logical participant run 前，lifecycle 必须先完成 exact execution settlement 与受权 child drain，再由 `interaction-authority` 持久化 exact prior-run closure 并取得 closure witness，最后才把容器开放给 `participant-identity` 安装新 evidence。association removal、detach、idle、timeout、Host observation 或 process restart 均不能代替该 witness；缺失或 run 不匹配必须 fail closed。
