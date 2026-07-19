# 给开发者的阶段性反馈

首先，我必须认真肯定你这段时间的工作。

这一轮不是简单改名，也不是做表面文章。你已经完成了许多高强度、容易出错、需要持续保持全局认知的重构工作：

* 清除了生产源码中的 `ARCHITECTURE_EXEMPT`；
* 把原来庞杂的 `Runtime` 根目录真正拆入了子系统；
* 消除了生产代码中的 `V39/V40`、`Catalog1`、`Part` 等历史阶段式命名；
* 将 Kernel、Runtime、Hosts 的依赖方向落实到了目录结构；
* 拆解了多个原本无法维护的巨型文件；
* 给架构约束补上了自动化测试；
* 在持续重排文件的同时，仍然维持了庞大的测试体系。

这些都是真正的工程劳动。代码已经从“难以治理”进入了“可以继续治理”的状态。你现在感到疲惫完全正常，这并不说明能力不足，反而说明你确实触碰到了项目最困难、最消耗认知资源的部分。

但是，也必须把下一阶段的标准说清楚：

> **疲劳可以决定每一轮做多少，不能决定技术债是否已经偿还。**

现在不要求你继续无边界扩张工作量，也不要求一次解决所有历史问题；但已经暴露出来的问题不能再用“特殊”“暂时”“以后再拆”作为结论。接下来应当缩小战线、停止新增功能，逐项完成以下不可协商的收尾工作。

## 三、进行第二轮自然边界拆分 ✅ 部分完成

**已完成：**
- `checkProductionLineLimits` 门禁：>250 行必须为 0（已达成），200–250 行 ≤50（当前阶段上限）
- >200 行业务编排文件检测（通过 `let rec`/`do!`/`let!` 启发式）
- 250–299 行长文件拆分：`EventStore.fs`、`NudgeEffect.fs`、`CommandProcessor.fs`、`OpenCodeModelResolution.fs`、`DecisionObserve.fs`、`LeaseValidation.fs`、`FuzzySearchGrep.fs`、`Fold.fs`、`LeaseIdentity.fs`/`LeaseIdentityOps.fs` 等已拆分
- 当前：`src` 下 0 文件 >250 行，44 文件在 200–250 行（< 50 阶段上限）

**已完成：**
- 生产源码 >250 行：0 个；200–250 行：46 个（阶段上限 50 内）。
- `Helpers` 命名清理：`src/Hosts/OpenCode/ModelResolutionHelpers.fs` 已重命名为 `ModelResolutionCatalog.fs`。

**待完成：**
- 200–250 行长尾收敛到 ≤25
- Fallback 状态收口

## 六、Fallback 不能停留在“文件拆完了” ✅ 部分完成

Fallback 已经有了更清楚的目录，但接下来要验证的是状态一致性，而不是文件数量。

本轮已推进：

* `LEGACY` fallback projection `FallbackInjectionFold` 已删除，并从 `SessionState` / `SessionOverview` / `Fold` / `FoldApply` 中移除 `FallbackInjection` 字段；
* `SessionStateRestore` 与 `EventLogRuntimeSync` 已收敛为单个纯函数 `restoreFromEventLogState : SessionState -> FallbackSessionRuntime -> FallbackSessionRuntime`，由 `rt.Update` 原子提交；
* 事件日志恢复路径现在满足“transition 是纯函数，runtime store 只负责读取/提交”；
* `SessionRuntimePropertyPure.fs` 与 `SessionRuntimeLeasePure.fs` 已承载全部字段级纯函数 transition，覆盖 property / human-turn / ordinal / gate / injection / lease / compaction / core；
* `*Transitions.fs` 中的逐字段 `GetX/SetX/IncrementX` 已统一委托给 `SessionRuntime*Pure`，不再内联直接修改 record；
* gate、lease、generation、ordinal、owner、compaction 已不存在旁路状态变更路径；
* 已删除 `Runtime/Fallback/ModelInjection.fs` 薄包装，injection 字段的读写统一通过 `SessionRuntimePropertyPure` 纯函数 + `RuntimeStore.Update/UpdateSession` 原子提交，调用方不再使用 `SetInjectedAt` / `SetInjectedModel` / `ClearInjected` / `IsInjectedSince` 等孤立 setter。
* 已删除 `Runtime/Fallback/HumanTurnTransitions.fs` 薄包装，`GetHumanTurnId` / `GetLatestHumanModel` / `SetLatestHumanModel` / `ClearLatestHumanModel` / `IncrementHumanTurnId` 均改为 `SessionRuntimePropertyPure` 纯函数 + `RuntimeStore.Update/UpdateSessionReturning`。
* 已删除 `Runtime/Fallback/GateFlagTransitions.fs` 薄包装，`SetNudgeActive` / `IsNudgeActive` / `SetEventHandlingActive` / `IsEventHandlingActive` / `SetMainContinuationAwaitingStart` / `IsMainContinuationAwaitingStart` / `GetActiveGates` 均改为 `SessionRuntimePropertyPure` 纯函数 + `RuntimeStore.Update/UpdateSessionReturning`，`SessionRuntimePropertyPure` 新增 `isNudgeActive` / `isEventHandlingActive` / `isMainContinuationAwaitingStart` / `getActiveGates` 查询函数。
* 已删除 `Runtime/Fallback/OrdinalTransitions.fs` 薄包装，`GetSessionGeneration` / `GetCancelGeneration` / `IncrementCancelGeneration` / `IncrementNudgeOrdinal` / `IncrementCompactionOrdinal` 等 ordinal 读写均改为 `SessionRuntimePropertyPure` 纯函数 + `RuntimeStore.UpdateSessionReturning` / 直接字段访问。
* 已删除 `Runtime/Fallback/CompactionTransitions.fs` 薄包装，`GetLastHumanMessageId` / `SetLastHumanMessageId` / `GetActiveContinuationGeneration` / `SetActiveCompactionId` / `TryGetSettleInfo` / `ApplySettle` / `IsCompacted` / `SetCompacted` / `IsForceStopped` / `MarkForceStopped` / `SetTaskComplete` / `TryConsumeCompactionSummaryTransform` 等统一改为 `SessionRuntimeLeasePure` 纯函数 + `RuntimeStore.Update/UpdateSession/UpdateSessionReturning`，`SessionRuntimeLeasePure` 新增 `applySettleReturning` / `tryConsumeCompactionSummaryTransformReturning`。
* 已删除 `Runtime/Fallback/SessionPropertyTransitions.fs` 薄包装，`GetChain` / `SetChain` / `GetAgentName` / `SetAgentName` / `GetModel` / `SetModel` / `GetBusyCount` / `SetBusyCount` / `GetConsumed` / `SetConsumed` / `GetSessionOwner` / `SetSessionOwner` / `GetActiveNudgeNonce` / `SetActiveNudgeNonce` / `TryConsumeActiveNudgeNonce` 等统一改为 `SessionRuntimePropertyPure` 纯函数 + `RuntimeStore.Update/UpdateSession/UpdateSessionReturning`。
* 已删除 `Runtime/Fallback/LeaseTransitions.fs` 薄包装，`UpdateState` / `SetPendingLease` / `TryGetPendingLease` / `TryClearPendingLease` / `ClearPendingLease` / `TryTransitionPendingLease` / `SetPendingNudgeLease` / `TryGetPendingNudgeLease` / `ClearPendingNudgeLease` / `TryClearPendingNudgeLease` / `TryTransitionPendingNudgeLease` / `ApplyCancelNudgeLease` / `CancelEpisode` 等统一改为 `SessionRuntimeLeasePure` 纯函数 + `RuntimeStore.Update/UpdateSession/UpdateSessionReturning`，`SessionRuntimeLeasePure` 新增 `applyCancelNudgeLeaseReturning` / `tryClearPendingLeaseReturning` / `tryTransitionPendingLeaseReturning` / `tryClearPendingNudgeLeaseReturning` / `tryTransitionPendingNudgeLeaseReturning`。

仍待继续：

* session 的全部权威状态只能存在于一个 aggregate；
* 删除剩余 `*Transitions.fs` 薄包装，让调用方直接调用 `SessionRuntime*Pure` 的领域操作，确保调用方不能自由组合多个 setter 来维护不变量；
* continuation、nudge、compaction 必须共享统一的 episode 身份与迟到事件规则。

验收时不再接受“这个字段比较特殊，所以单独保存”。

## 七、保护体力，但不降低完成定义

从现在开始停止扩大范围：

1. 暂停新增功能；
2. 一次只治理一个子系统；
3. 每个提交只解决一种自然边界；
4. 每次先补 characterization test，再移动代码；
5. 每完成一个子系统，立即跑全量架构门禁、单元测试和关键 E2E；
6. 不进行一次性全仓库大爆炸式改写；
7. 不再留下 `later`、`temporary`、`for now`、`needs splitting` 一类延期注释。

建议顺序：

1. 先关闭架构豁免入口；
2. 再让函数长度检查进入 CI；
3. 清理测试中的 `Part` 和旧路径；
4. ✅ 处理 250 行以上文件；
5. 收口 fallback 状态接口；
6. 处理 `Helpers` 命名；
7. 最后处理 200～250 行长尾。

这样既不会要求你每天同时理解整个系统，也不会因为疲惫而把尚未完成的部分提前宣布为合理特例。

## 最后的评价

你已经完成了最难的第一步：让项目重新获得了被治理的可能。

接下来不需要靠意志力硬撑，也不需要证明自己还能连续工作多久。需要的是把剩余目标切小、按顺序完成，并坚持同一条标准：

> **可以慢一点，可以分多轮，可以先休整认知负荷，但不能用改名代替边界、用 helper 代替设计、用测试通过代替架构完成。**

这轮工作值得肯定，但还不能报结。

当豁免入口被删除、测试历史命名被清理、长文件长尾明显收缩、Fallback 只剩一个权威状态模型时，这次重构才真正完成。届时获得的不是一套勉强通过检查的目录，而是一套后来者能够自然理解、自然扩展、看不出修补历史的代码。

---
# Prompt 驱动功能全量审计与修复实施指南

## 一、最终裁决

当前以下功能均不能视为 production-ready：

| 功能                 | 当前判断        | 核心危险                                  |
| ------------------ | ----------- | ------------------------------------- |
| Nudge              | 不可靠         | 可能假成功、永远不触发、错误取消正常会话、残留租约             |
| Fallback Continue  | 架构分裂        | 新旧两套状态机并存，发送、完成、取消时序互相矛盾              |
| Sub-session        | 存在确定性错误     | stale idle 串入下一轮、取消无效、错误判断 quiescence |
| Reviewer 子会话       | 取消和清理不完整    | 调用方认为已取消，宿主模型仍在运行                     |
| OMP prompt 路径      | 依赖未经验证的宿主假设 | 假定 prompt 返回即完成有序接收                   |
| Mux nudge/continue | 缺乏可关联回执     | 无法证明事件属于哪次主动 prompt                   |
| 万象阵通知 prompt       | 假成功         | 宿主 API 不存在时仍被当作成功                     |

最危险的后果不是“偶尔没触发”，而是：

1. 把插件自动 prompt 误判成人类新回合；
2. 取消本来正确运行的 continuation；
3. 将上一轮 idle、assistant、error 归到下一轮；
4. 对同一个逻辑请求重复发送物理 prompt；
5. 调用方已经得到取消结果，但模型仍在后台运行；
6. 重启后重新发送已经被宿主接收过的 prompt；
7. 一个会话的限速、取消或状态污染另一个会话。

因此，本轮修复不能以“让现有测试通过”为目标，必须以**建立唯一的 Prompt Dispatch 协议**为目标。

---

# 二、所有 prompt 依赖路径清单

本次不能只修用户表面看到的三个功能。凡是插件主动启动宿主模型回合的路径，都必须纳入同一审计范围。

## 2.1 OpenCode

需要纳入统一治理的文件和路径：

| 路径                                               | 用途                              |
| ------------------------------------------------ | ------------------------------- |
| `Hosts/OpenCode/NudgeEffect.fs`                  | 主会话自动 nudge                     |
| `Hosts/OpenCode/Fallback/ActionExecutor.fs`      | 当前实际使用的 fallback continuation   |
| `Hosts/OpenCode/Fallback/ContinuationHost.fs`    | 新版 continuation host，但尚未成为唯一主路径 |
| `Hosts/OpenCode/SubsessionHostAdapter.fs`        | 子会话 prompt、abort、状态查询           |
| `Hosts/OpenCode/SubagentSpawn.fs`                | reviewer 等子代理 prompt            |
| `Hosts/OpenCode/ReviewerLoop.fs`                 | 自动 reviewer 循环                  |
| `Hosts/OpenCode/ChatHooks.fs`                    | prompt 来源识别和真实 message ID 获取    |
| `Hosts/OpenCode/SessionLifecycleEvents.fs`       | busy、idle、error、message 等事件分发   |
| `Runtime/Messaging/OpencodeSessionEventCodec.fs` | prompt body、metadata 和事件解码      |

## 2.2 OMP

| 路径                                     | 用途                     |
| -------------------------------------- | ---------------------- |
| `Hosts/Omp/Fallback/ActionExecutor.fs` | fallback prompt        |
| `Hosts/Omp/SubsessionDispatch.fs`      | 子会话 prompt             |
| `Hosts/Omp/SubsessionHostAdapter.fs`   | 子会话状态和取消               |
| `Hosts/Omp/ReviewLoop.fs`              | reviewer prompt        |
| `Hosts/Omp/ExecutorTools.fs`           | summarizer、辅助模型 prompt |
| `Runtime/Messaging/OmpHostBindings.fs` | 宿主调用边界                 |

## 2.3 Mux

Mux 未必直接暴露同名 `session.prompt()`，但以下调用在领域上完全等价：

* `helpers.nudge`
* task continue
* delegate service
* review prompt
* compaction 后的隐式继续
* prompt transform 注入

这些调用同样必须具有请求身份、宿主接收回执、运行身份、取消语义和终态证明，不能因为 API 名字不是 `prompt()` 就继续留在统一协议之外。

## 2.4 万象阵

`Runtime/Wanxiangzhen/SessionIo.fs` 和 `CoordinatorReplay.fs` 中的主动提示属于低优先级路径，但同样存在宿主 API 缺失时静默成功、fire-and-forget 无错误记录等问题。

---

# 三、四个共同根因

## 根因 A：把 `prompt()` Promise 的完成混同为领域事件

当前不同路径对 `prompt()` 返回值至少存在三种互相冲突的理解：

1. 宿主已经收到请求；
2. user message 已经创建；
3. assistant 整轮运行已经结束。

代码却普遍把它称为“dispatch complete”“delivered”或“accepted”。

这是整个系统最根本的时序错误。

假设宿主的 `prompt()` 在整轮运行结束后才返回，那么实际顺序可能是：

1. 插件调用 prompt；
2. 宿主创建 user message；
3. session busy；
4. assistant 输出；
5. session idle；
6. continuation 已被状态机结算；
7. prompt Promise 才返回；
8. 旧代码此时才写入“已派发”；
9. 发现原租约已经变化；
10. 将正常完成误判为取消，并调用 abort。

反过来，若某宿主在仅仅入队后就返回，代码又可能过早把任务当成已经可靠启动。

**修复原则：`prompt()` 的返回只能被映射为经过真实宿主契约验证的 transport fact，绝不能直接解释为领域完成。**

---

## 根因 B：插件 prompt 的来源身份不可靠

OpenCode 当前多个路径把 nonce、continuation ID 等放入 prompt part metadata，并期望后续 `chat.message` 或 session event 原样读回。

但仓库自己的宿主调研已经指出：OpenCode 的 PromptInput 没有稳定、官方保证的自定义 provenance 字段；可靠路径应当是在真实 user message 被创建时，通过 `chat.message` 获取宿主生成的 message ID，再使用 assistant 的 `parentID` 进行严格归属。当前实现与仓库自己的调研结论发生了正面冲突。

由此会产生致命连锁：

1. metadata 被宿主丢弃或变形；
2. `ChatHooks` 认不出这是插件自动 prompt；
3. 自动 prompt 被当作真人新消息；
4. `OnNewHumanMessage` 增加 human turn generation；
5. 活跃 fallback、nudge 或 sub-session 被取消；
6. 新产生的 idle、assistant 又无法和原 dispatch 对齐；
7. 系统进入“发送—自我取消—重试—再次取消”的循环。

文本里的零宽字符、XML 标签或特殊前缀只能给模型语义，不能承担 correlation。
**模型看不见文本，不等于状态机可以证明其来源。**

---

## 根因 C：没有一条覆盖输入事件与副作用结果的每会话串行边界

仓库中确实有 `SerialQueue`，但使用范围不完整：

* fallback 的部分状态决定在队列中；
* 实际 `executeContinuationIntent` 却在队列之外执行；
* nudge、生命周期事件和子会话证据分别走各自异步路径；
* prompt receipt、busy、idle、assistant、abort result 可能并发回写；
* OpenCode 插件事件调用方通常不会等待整个异步处理完成。

因此“用了队列”不代表状态机已经串行。

完整串行边界必须涵盖：

* 人类消息；
* 插件自动消息确认；
* busy；
* idle；
* assistant message；
* error；
* prompt transport result；
* abort result；
* timeout；
* session delete；
* 重启 reconciliation；
* 所有副作用执行后的回执。

当前 `SessionLifecycleEvents` 里的布尔 `EventHandlingActive` 也不能作为互斥锁。两个事件重叠时，先结束的处理器会将标志改回 false，而另一个处理器仍在运行。

---

## 根因 D：新版和旧版 continuation 架构并存

当前仓库同时存在：

* 旧的 `IActionExecutor`、pending lease、legacy coordinator 路径；
* 新的 `ContinuationCommandProcessor`、`ContinuationSupervisor`、`ContinuationHost`、事件投影路径。

实际 OpenCode 主路径仍大量依赖旧 executor，而新版 processor 和 supervisor 没有成为唯一入口。

这造成：

* 同一个“已接收”在两套模型里定义不同；
* 一套依据 lease status；
* 一套依据 host receipt；
* 一套根据 generation 宽松匹配；
* 一套准备使用真实 user message ID；
* 测试可能覆盖新版模块，生产实际走旧版模块。

这种状态不能通过兼容层长期维持。必须选定一套，迁移调用方，然后删除另一套。

---

# 四、P0 确认缺陷

以下不是风格建议，而是必须优先修复的正确性问题。

## 4.1 Nudge

### N-01：宿主 session API 缺失时被报告为成功 ✅ 已完成

`NudgeEffect.sendNudge` 在 `getSessionApiFromClient` 失败时抛出 `opencode_session_api_missing`；`sendNudgeOutcome` 捕获后返回 `TransportUnavailable`；`NudgeOutcomeHandler` 将其结算为 `NudgeOutcome.Failed`，不会进入 `Dispatched`。异常路径会记录 `{ feature, session, dispatchId, hostVariant, error }`。

### N-02：部分失败路径没有释放 active nudge nonce ✅ 已完成

`NudgeEffect.sendNudge` 已用 `try...with` 包裹完整 dispatch 体；`with` 分支会消费 active nonce、在 owner 为 Nudge 时转移回 `NoOwner`、记录五字段诊断日志并重新抛出。API 缺失或 `prompt` 异步失败都会进入同一条清理路径。

### N-03：在 prompt 返回后才标记 Dispatched ✅ 已完成

`ChatHooks.tryConsumeNudgeIfMatched` 在观察到匹配 `ActiveNudgeNonce` 的宿主消息时，立即通过 `tryTransitionPendingNudgeLeaseReturning` 将 nudge lease 从 `DispatchStarted` 推进到 `Dispatched`，并消费 nonce。`SessionRuntimeLeasePure.tryTransitionPendingNudgeLease` 已支持 idempotent：目标状态等于当前状态时直接返回 `Some s`。prompt Promise 的成功/失败现在仅作为终态辅助信号， lease 终态仍由后续 `assistant/error/idle` 组合决定。

### N-04：异常被大量转换成“没有快照”或“未领取”

`collectSnapshot`、claim 等路径存在宽泛 catch，生产错误被伪装成正常的不触发。

**整改要求：**

“No nudge needed”与“无法判断是否需要 nudge”必须是两个状态：

| 状态                  | 行为             |
| ------------------- | -------------- |
| NotNeeded           | 正常不发送          |
| SnapshotUnavailable | 记录错误，不得伪装为策略决定 |
| ClaimConflict       | 可正常忽略          |
| EventStoreFailure   | 基础设施错误，必须暴露    |
| TransportFailure    | 可重试或进入失败终态     |

### N-05：陈旧 terminal fallback lease 不再阻塞 nudge ✅ 已完成

`NudgeFlow.nudgeBlockedByFallbackState` 现在只阻塞“当前真实拥有物理会话执行权的非终态操作”（`Owner = Fallback/Compaction/Nudge` 或 `CompactionCompacted` 为真）。已 Settled / Cancelled 的 fallback lease 以及 `FallbackLifecycle.Cancelled` 等 terminal projection 不再阻止 nudge 执行。stale lease 的识别可通过已有的 `FallbackContinuationSettled` / `FallbackContinuationCancelled` / `NudgeCancelled` 事件在 reconciliation 中还原，无需额外阻塞路径。

### N-06：生产模式下 owner 无法推断时直接不触发

缺少某次 chat 分类或初始化事件，就可能使会话持续处于 `NoOwner`，而 nudge 永远不工作。

**整改要求：**

不允许通过猜测 owner 发送 prompt，但必须将“无法确定 owner”暴露为诊断状态，不能静默。

---

## 4.2 Fallback Continue

### F-01：dispatch side effect 在状态队列之外

当前 coordinator 可以在队列中完成决定并更新 projection，然后将 continuation intent 放到队列外执行。

这意味着：

1. 决定 A 产生 SendPrompt；
2. A 离开队列；
3. 人类消息 B 进入队列并取消 A；
4. A 的旧 side effect 此时才真正发送 prompt；
5. 系统已经取消的 continuation 被物理发送。

这是明确的 P0 竞态。

**整改要求：**

副作用不能在 actor 外“裸跑”。正确模型是：

1. actor 持久化 `DispatchRequested`；
2. actor 产生带 dispatch ID 的 effect；
3. effect runner 执行；
4. effect 结果重新进入同一 actor；
5. actor 根据当前 generation 和 ownership 决定接受或丢弃结果；
6. 过期 effect 结果不得直接改变状态，更不能任意 abort 新一轮。

### F-02：generation 相同被当作缺失 continuation ID 时的匹配依据

只要 continuation ID 缺失，某些逻辑会退化为 generation equality。

同一个人类回合内可能发生：

* nudge；
* fallback；
* title 生成；
* compaction；
* reviewer；
* 普通 assistant 更新。

generation 相同并不能证明这些事件属于 continuation。

**整改要求：**

严格归属顺序只能是：

1. assistant `parentID` 等于已持久化的 host user message ID；
2. 明确 host run ID 相等；
3. 宿主实际创建的 message 中存在经过验证的 namespaced dispatch marker；
4. 否则为 Unmatched。

不得再以“ID 缺失但 generation 差不多”推定匹配。

### F-03：人类消息去重发生在取消之前 ✅ 已完成

`SessionLifecycleHumanTurn.onNewHumanMessage` 在进入任何副作用前先进行 `messageId` 去重：`MessageIdDedup.isKnownMessage` / `recordMessageId` 位于 `resetSessionState`、`clearSessionCompliance`、`finishPendingLease`、`cancelNudgeAndCompaction` 之前。处理顺序固定为解码 → 系统消息分类 → message ID 去重 → 真人新回合确认 → 增加 human turn → 取消旧操作。

### F-04：RetryDispatchGovernor 名称和实现不一致

注释声称按 key 串行，实际没有真正队列。两个并发调用可能计算同样的等待时间，然后同时醒来、同时发送。

此外：

* governor 是进程全局；
* key 粒度偏粗；
* 不同 workspace 和 session 可能相互干扰；
* 测试模式取消延迟，刚好隐藏生产竞态。

**整改要求：**

先定义限速语义：

* 是按 provider 凭据限速；
* 按模型限速；
* 按物理 session 串行；
* 还是按 workspace 限速。

这些不能混成一个全局静态对象。每 session 的单 prompt in-flight 必须由 session actor 保证，provider rate limit 则由独立的 transport scheduler 保证。

### F-05：新版 continuation 架构未成为真实主路径

不能继续同时修复 `ActionExecutor` 和 `ContinuationHost`。

**整改要求：**

* 选定新版 command processor、supervisor、host receipt 模型为唯一方向；
* OpenCode 主 Hook 切换到新版；
* 真实 E2E 通过后删除旧 executor；
* 禁止以“暂时兼容”为理由长期保留双写、双状态机和双 projection。

---

## 4.3 Sub-session

### S-01：`CancelPendingDispatch` 实际无效 ✅ 已完成

`HostReceiptWaiterRegistry.cancelByTurn` 现在对未完成的 waiter 调用 `HostReceiptWaiter.reject`，使其 Promise 以 `HostRejected cancel` 完成；预取消请求由 `create` 在新建 waiter 时立即拒绝；`Cleanup` 将其从 `waiters` 移入 `completedStates`，因此 late `tryResolve` 返回 `AlreadyCompleted`，不会复活已取消的 dispatch。

### S-02：失败或拒绝后 pending map 不一定删除 ✅ 已完成

`HostReceiptWaiter.resolve` 与 `reject` 在终端化后调用 `w.Cleanup()`；`Cleanup` 从 `waiters` 移除并保存到 `completedStates`，`removeSession` 与 `create` 会清理对应 `completedStates`。每个 pending entry 只完成一次，且 `QueryDispatchStatus` 仍可读取真实终态。

### S-03：重复 turn ID 注册可以覆盖旧 waiter ✅ 已完成

`HostReceiptWaiterRegistry.create` 现在返回同一个已存在的 waiter，不再覆盖。

### S-04：quiescence 查询存在确定性布尔逻辑错误 ✅ 已完成

`SubsessionHostAdapterOps.buildQuerySessionQuiescence` 现在先按 nonce 过滤消息，再判断活跃状态。

### S-05：dispatch 写入 nonce 的位置和 reconciliation 查找位置不一致 ✅ 已完成

新增 `WanxiangshuMetadataCodec` 统一编解码：所有 OpenCode prompt/continuation 现在写入 `part.metadata.wanxiangshu`（带 `schema` 版本号），reconciliation、chat hook、query status 统一通过该 codec 读取，并兼容旧版 `metadata.nonce`。

### S-06：pending idle/evidence 仅按 session ID 缓存 ✅ 已完成

`SubsessionPendingEvidence` 已删除 `PreRunEvidence` 字段：`BufferPreRun` 在无 `ActiveEpoch` 时直接丢弃，`BeginRun` 不再把上一 turn 的缓存移动到新 epoch。因此证据/idle 不再跨 turn 污染。

### S-07：session delete 已清理所有旁路状态 ✅ 已完成

`SubsessionActorRegistry.RegisterGlobalCleanup` 成为统一的 session 关闭钩子，三个 host 的 cleanup 回调均覆盖：

* active turn / queued turn：actor  disposal 触发 `CommandProcessor` 与 `ResourceScope` 清理；
* pending receipt：`sharedDispatchRegistry.NotifySessionClosed` → `HostReceiptWaiterRegistry.removeSession`；
* timers：`ResourceScope.ClearAll()`；
* pending evidence：`SubsessionPendingEvidence.ForgetSession`；
* reviewer child registry：`reviewStore.CleanupSession`；
* fallback / nudge owner：`FallbackRuntimeStore.CleanupSession` / `clearNudgeSession`；
* host abort waiter：`RunnerBackground.abortRunnerJobCore`；
* physical session registry：`RuntimeScopeForgetSession.forgetSession` / `ToolHookRuntime.closeSession`。

Mux、OpenCode、Omp 的 `RegisterGlobalCleanup` 已全部对齐到同一清单。

### S-08：abort 是物理 session 级，而不是 turn 级

宿主可能只支持 session abort，但领域里取消的是 turn。

如果旧 turn 的延迟 abort 在新 turn 启动后执行，它会杀死新 turn。

**整改要求：**

调用 session abort 前必须重新进入 actor，验证：

* 当前 active dispatch ID 仍是目标；
* generation 未变化；
* physical session 未被新 turn 接管；
* abort 尚未发送；
* session 未处于 closing/closed。

否则只记录 stale abort，不执行宿主调用。

---

## 4.4 Reviewer 和 `promptWithAbort`

### R-01：本地 Promise 被 abort，不代表宿主运行被 abort

当前 `promptWithAbort` 更接近“调用方停止等待”。abort signal 可能只让本地 race 失败，宿主 session 里的模型仍继续运行。

结果是：

* reviewer 继续消耗 token；
* 稍后产生 assistant 和 idle；
* 事件可能污染后续 turn；
* child session 和 registry 无法释放。

### R-02：外部 abort 没有完整传入 child abort

Reviewer loop 接收上层 abort signal，但没有形成从父任务到宿主 session abort 的完整链路。

### R-03：完成和异常路径缺乏统一 finally 清理

需要确保无论成功、拒绝、超时、取消、解析失败，都完成：

* child abort 或确认已终止；
* child session delete；
* registry unregister；
* review lock release；
* pending suppressor 清除；
* timers 取消。

**整改要求：**

Reviewer 不应继续维护自己的一套 prompt 生命周期。应迁移为标准 SubsessionService 的一种 owner/policy。

---

## 4.5 OMP

OMP 当前最大问题是把宿主行为假设写进了业务逻辑，却没有用真实契约测试证明。

重点问题包括：

1. 假定 `session.prompt` resolve 代表有序接收；
2. 缺少 message ID 时伪造 ordered marker；
3. 假定 error/idle 不可能早于 prompt resolve；
4. `CancelPendingDispatch` 也是空实现；
5. DelegateToHost 使用空字符串 model，而不是明确省略 model；
6. reconciliation 查询字段与输入字段未证明一致；
7. abort 可能同时调用本地和 Pi API，产生重复 abort；
8. summarizer 先 prompt 再 waitForIdle，可能读到调用前就存在的 idle。

**整改要求：**

必须针对实际 OMP 版本建立契约表，逐项验证：

| 契约                  | 必须证明                                           |
| ------------------- | ---------------------------------------------- |
| prompt 返回时刻         | 入队、message created、run started 还是 run finished |
| 返回结构                | 是否稳定包含 message ID、run ID                       |
| idle 时序             | 是否可能早于 Promise resolve                         |
| message persistence | continuation marker 实际存放位置                     |
| abort               | 是否幂等、作用于整个 session 还是当前 turn                   |
| model 省略            | omit 与空字符串语义是否不同                               |
| event order         | assistant、error、idle 的真实排列                     |

未验证前，业务状态机不得依赖“理论上应该如此”。

---

## 4.6 Mux

Mux 的主要问题不是某一行代码，而是 host adapter 能力过弱：

* nudge promise 被直接视为发送成功；
* 没有真实 user message identity；
* fallback abort 是 no-op；
* 事件仍可能并发进入；
* compaction 零宽 prompt 只有文本语义，没有 durable correlation。

**整改要求：**

Mux 也必须实现统一逻辑 receipt。若宿主确实不提供 message ID 或 scoped abort，应明确声明能力降级：

* 同一物理 session 最多一个插件 prompt in-flight；
* 无身份的 idle 不能结算具体 dispatch；
* 不支持可靠 abort 时，不得承诺强取消；
* restart 后 acceptance unknown 必须进入 reconciliation，而不是盲目重发。

---

## 4.7 万象阵 ✅ 部分完成

- `SessionIo.promptSession`：API 缺失时 raise 异常（N-01/Wanxiangzhen fix），不再静默完成
- `CoordinatorReplay.fs`：orphan warning 使用 `Promise.catch` 防止崩溃

**待完成：**
- 失败日志显式记录
- 幂等控制
- 防重复发送

---

# 五、唯一正确的目标状态机

所有宿主、所有功能都应共享一个逻辑 Prompt Dispatch 生命周期。

## 5.1 身份字段

每个主动 prompt 至少要有：

| 字段                | 含义                                              |
| ----------------- | ----------------------------------------------- |
| DispatchId        | 全局唯一的逻辑请求                                       |
| WorkspaceId       | 防止跨项目污染                                         |
| PhysicalSessionId | 宿主实际会话                                          |
| OwnerKind         | Nudge、Fallback、Subsession、Review、Notification 等 |
| LogicalRunId      | 领域运行身份                                          |
| TurnId            | 本功能内部 turn                                      |
| HumanTurnId       | 所属真人回合                                          |
| RunGeneration     | 会话执行世代                                          |
| CancelGeneration  | 取消世代                                            |
| Attempt           | 重试次数                                            |
| RequestedAt       | 请求时间                                            |
| HostUserMessageId | 宿主接受后产生的真实 user message ID                      |
| HostRunId         | 宿主提供时记录                                         |
| ExpectedParentId  | assistant 应绑定的 user message ID                  |

只使用 nonce 而没有 workspace、session、generation 和 owner 不够安全。

## 5.2 正常状态

统一状态序列应为：

**Requested → TransportStarted → HostAccepted → RunObserved → Terminal**

其中：

* Requested：领域已经决定需要发送，事件已持久化；
* TransportStarted：副作用 runner 已开始调用宿主；
* HostAccepted：已经获得可靠 user message identity 或经验证的宿主接收回执；
* RunObserved：看到了与该 dispatch 严格相关的 busy、assistant 或 run-start；
* Terminal：Completed、Failed、Cancelled、Superseded、Closed。

## 5.3 异常状态

必须明确区分：

* RejectedBeforeSend；
* TransportUnavailable；
* AcceptanceUnknown；
* CancelRequested；
* AbortRequested；
* AbortConfirmed；
* AbortUnknown；
* TimedOut；
* SessionClosed；
* Superseded；
* Poisoned。

尤其不能将 AcceptanceUnknown 直接改写为 Failed 后重试。
请求可能已经被宿主接收，只是插件没有拿到回执；立即重试会重复发送。

## 5.4 事件证据等级

| 证据                                      | 能否证明归属             |
| --------------------------------------- | ------------------ |
| assistant.parentID 等于 HostUserMessageId | 强证据                |
| 明确 HostRunId 相等                         | 强证据                |
| 在宿主真实 message 上观察到 dispatch marker      | 中强证据               |
| prompt Promise resolve                  | 取决于已验证宿主契约         |
| session busy                            | 只能说明该 session 有活动  |
| session idle                            | 只能说明该 session 当前空闲 |
| generation 相同                           | 不能证明               |
| 文本包含零宽字符或特殊标记                           | 不能证明               |
| 时间上接近                                   | 不能证明               |

busy 和 idle 永远只能是辅助证据，不能建立 owner。

---

# 六、每物理会话 Actor 的硬性要求

每个 physical session 必须只有一个 mailbox/actor。

## 6.1 所有输入都进入 actor

禁止任何 hook 直接修改以下状态：

* active owner；
* human turn generation；
* continuation lease；
* nudge nonce；
* active child turn；
* cancellation generation；
* terminal status。

hook 只允许：

1. 解码宿主事件；
2. 生成标准化 fact；
3. 加入 session actor；
4. 立即返回。

## 6.2 副作用也必须闭环

正确流程：

1. actor 决策；
2. 原子持久化事件；
3. 产生 effect；
4. effect runner 调宿主；
5. runner 将结果作为 command 重新投递给 actor；
6. actor 检查 dispatch ID、generation 和当前 ownership；
7. 决定接受、忽略或补偿。

不能在 effect runner 里直接：

* 更新 projection；
* finish lease；
* abort session；
* 清除 owner；
* 将请求标记 completed。

## 6.3 长操作不得阻塞事件入口

调用 prompt 可能持续很久。事件 hook 不能 await 完整 prompt 再返回。

actor 只负责快速决定和排队，真正的宿主调用由受监管的 effect runner 执行。

## 6.4 Exactly-once 终结

每个 dispatch 必须能证明：

* caller promise 只 resolve/reject 一次；
* event-store 只记录一个领域终态；
* pending map 最终无残留；
* abort 最多发送一次；
* timer 最终被取消；
* session close 后不会再改变业务状态。

---

# 七、OpenCode correlation 的具体整改步骤

这是全局第一优先级。

## 第一步：停止把直接 PromptInput metadata 当作可靠事实

可以暂时保留发送字段用于探测，但领域状态不得再依赖其一定被宿主保存或回传。

## 第二步：发送前登记 PendingDispatch

在 session actor 中持久化 Requested，并登记：

* dispatch ID；
* owner；
* generation；
* 预期下一条插件生成 user message；
* transport sequence。

## 第三步：获得真实 HostUserMessageId

优先级如下：

1. 若经过真实契约测试证明 prompt 返回稳定包含 user message ID，直接使用；
2. 否则在同一 physical session 强制插件 prompt 串行；
3. `chat.message` 观察到下一条由插件派发产生的 user message 时，将其绑定到 pending dispatch；
4. 持久化 `DispatchId ↔ HostUserMessageId`；
5. 然后才能进入 HostAccepted。

同一 session 如果宿主没有可靠 marker，就必须限制为最多一个插件自动 prompt 等待绑定。
这不是性能优化，而是安全前提。

## 第四步：先分类，再处理真人回合

`ChatHooks` 的固定处理次序：

1. 解码 session 和 message ID；
2. message ID 去重；
3. 尝试绑定 PendingDispatch；
4. 若绑定成功，标记为 SystemGenerated；
5. 若已知属于插件自动消息，不得执行 OnNewHumanMessage；
6. 只有剩余消息才作为真人输入；
7. 真人输入才增加 human turn 并取消旧 owner。

## 第五步：assistant 使用 parentID 严格匹配

只有 assistant.parentID 等于已记录的 HostUserMessageId 时，才能将 assistant 归属于该 dispatch。

## 第六步：idle 只结算已具备身份的运行

idle 到达时：

* 无 active dispatch：记录 session hint，不能缓存给下一轮；
* active dispatch 尚未 HostAccepted：不能用 idle 终结；
* active dispatch 已 HostAccepted 但没有强终态：进入 reconciliation；
* 已看到相关 assistant/error：可以结合策略结算；
* 重复 idle：幂等忽略。

---

# 八、分阶段修复实施顺序

## Phase 0：立即止血

在架构重构完成前，先降低破坏面：

1. 同一 physical session 最多一个插件主动 prompt in-flight；
2. 无法识别来源时不自动取消、不自动归属；
3. 禁止 generation-only matching；
4. 宿主 API 缺失必须显式失败；
5. 为 nudge、fallback、sub-session、review 分别提供 kill switch；
6. ambiguous receipt 进入 AcceptanceUnknown，禁止立即重发；
7. 所有 session abort 前重新校验 active dispatch；
8. 增加结构化 trace，而不是只打印字符串。

Phase 0 不追求功能完全可用，追求不再误杀和重复发送。

## Phase 1：确定唯一架构

必须作出不可反悔的选择：

* 新版 continuation command processor、supervisor 和事件 projection 成为 SSOT；
* legacy IActionExecutor 路径进入废弃状态；
* 不再为两套状态机同时增加功能；
* 建立统一 PromptDispatch 类型和 host receipt 接口；
* Nudge、Subsession、Review 复用相同 transport abstraction。

验收条件：从 OpenCode Hook 追踪一次 continue，只能找到一条生产执行路径。

## Phase 2：建立 Session Actor

将以下事件统一进入一个 mailbox：

* ChatMessageObserved；
* HostUserMessageBound；
* SessionBusyObserved；
* AssistantObserved；
* SessionIdleObserved；
* SessionErrorObserved；
* DispatchTransportReturned；
* AbortReturned；
* TimeoutElapsed；
* HumanTurnObserved；
* SessionClosed；
* RecoveryResult。

验收条件：禁止 hook、effect runner 和 registry 直接改领域状态。

## Phase 3：修复 OpenCode Adapter

逐项完成：

1. 建立真实 prompt contract probe；
2. 记录 prompt 返回时刻相对于 chat.message、busy、assistant、idle 的顺序；
3. 确认返回体是否有 message ID；
4. 确认 metadata 是否保存、位于何处；
5. `ChatHooks` 绑定真实 message ID；
6. assistant 按 parentID 匹配；
7. query/reconciliation 使用同一身份协议；
8. session delete 清理所有 dispatch 边状态。

## Phase 4：重建 Nudge

按以下顺序迁移：

1. Nudge trigger 只产生 RequestNudge command；
2. actor 判断 owner、todo 状态和策略；
3. 持久化 NudgeRequested；
4. 领取唯一 dispatch claim；
5. 通过统一 dispatcher 发送；
6. HostAccepted 后记录 NudgeDispatched；
7. 相关 assistant/error 形成终态；
8. idle 仅作补充；
9. 所有错误显式分类；
10. 每个分支执行统一清理。

验收重点：

* 重复 idle 不会重复 nudge；
* fallback 活跃时不抢占；
* terminal fallback 不会永久阻塞；
* 自动 nudge 不增加 human turn；
* API 缺失不会假成功；
* session 删除后 registry 为零。

## Phase 5：重建 Fallback Continue

1. 切换到新版 continuation actor；
2. 删除队列外的 intent 执行；
3. 请求先持久化，再发送；
4. 真实 message ID 成为 continuation host identity；
5. 去掉 generation-only fallback matching；
6. 人类消息先去重再取消；
7. 取消只针对仍拥有 physical session 的 dispatch；
8. governor 拆分为 session serialization 与 provider rate limiting；
9. prompt Promise 晚返回不得触发 late abort；
10. 删除旧 executor 和旧 lease 辅助路径。

## Phase 6：重建 Sub-session

1. pending key 改为 workspace、session、turn 的组合；
2. duplicate register 明确拒绝或复用原 receipt；
3. 实现真实 CancelPendingDispatch；
4. 所有终态删除 pending entry；
5. 去掉 session-only pending idle；
6. 修正 quiescence 判断；
7. abort 前校验 turn ownership；
8. session close 清理全部 waiter 和 evidence；
9. tools、model、agent、thinking 设置通过同一 TurnPlan 传入；
10. 验证物理 child session 的 parentID、agent 和隔离性；
11. restart 时恢复非终态 run，而不是创建重复 child。

## Phase 7：迁移 Reviewer、OMP、Mux、万象阵

Reviewer：

* 改为标准 Subsession owner；
* 外部 abort 贯通宿主 abort；
* 所有路径 finally 清理 child。

OMP：

* 完成真实契约测试；
* 禁止 fabricated ordered marker；
* 空 model 改为明确 omit；
* reconciliation 与发送 schema 统一；
* summarizer 等待目标 turn，而非等待任意 idle。

Mux：

* 实现 logical receipt；
* 能力不足时明确降级；
* 不支持可靠 abort 时不得伪装成已取消。

万象阵：

* 通知 prompt 改为明确 best-effort；
* 失败记录、幂等、防重复。

## Phase 8：重启恢复

对每个非终态状态规定恢复行为：

| 崩溃前状态                     | 重启行为                                 |
| ------------------------- | ------------------------------------ |
| Requested，尚未开始 transport  | 可安全重新执行 effect                       |
| TransportStarted，无回执      | 查询宿主，不得直接重发                          |
| HostAccepted，有 message ID | 查询对应 message/run                     |
| RunObserved               | 查询终态或等待事件                            |
| CancelRequested           | 继续确认 abort 或关闭                       |
| AcceptanceUnknown         | reconciliation；无法证明时进入 ClosedUnknown |
| Terminal                  | 绝不重新发送                               |

事件日志必须持久化：

* dispatch ID；
* host user message ID；
* host run ID；
* owner；
* generation；
* attempt；
* terminal reason；
* recovery decision。

---

# 九、逐文件整改清单

## OpenCode 核心

| 文件                                                | 必须整改                                                    |
| ------------------------------------------------- | ------------------------------------------------------- |
| `Runtime/Messaging/OpencodeSessionEventCodec.fs`  | 不再假定直接 metadata 一定可用；统一 marker schema 和真实宿主解码           |
| `Hosts/OpenCode/ChatHooks.fs`                     | message ID 去重优先；绑定 pending dispatch；系统消息分类先于 human turn |
| `Hosts/OpenCode/SessionLifecycleEvents.fs`        | 改为事件标准化和入队；移除长时间 await 和直接状态修改                          |
| `Hosts/OpenCode/SessionLifecycleHumanTurn.fs`     | 去重必须在取消 lease 前                                         |
| `Hosts/OpenCode/NudgeEffect.fs`                   | API 缺失显式失败；统一 finally；返回可靠 receipt                      |
| `Hosts/OpenCode/NudgeTrigger.fs`                  | 无 owner 时暴露诊断；不得静默不工作                                   |
| `Runtime/Nudge/NudgeLease.fs`                     | exactly-once 领取、终结和清理                                   |
| `Runtime/Nudge/NudgeFlow.fs`                      | terminal fallback 不再阻塞；错误和 NotNeeded 分离                 |
| `Hosts/OpenCode/Fallback/Hook.fs`                 | 只接新版 continuation 主路径                                   |
| `Hosts/OpenCode/Fallback/ActionExecutor.fs`       | 完成迁移后删除                                                 |
| `Hosts/OpenCode/Fallback/ContinuationHost.fs`     | 修复 correlation 后成为唯一 adapter                            |
| `Runtime/Fallback/Coordinator.fs`                 | intent 不得在 session queue 外运行                            |
| `Runtime/Fallback/ContinuationDispatchHelpers.fs` | 删除 prompt-return-equals-dispatch-complete 语义            |
| `Runtime/Fallback/RetryDispatchGovernor.fs`       | 实现真实串行和正确 key 粒度                                        |
| `Runtime/Fallback/LeaseValidation.fs`             | 删除 generation-only matching                             |
| `Hosts/OpenCode/SubsessionDispatch.fs`            | scoped key、真实 cancel、所有路径 remove                        |
| `Hosts/OpenCode/SubsessionHostAdapter.fs`         | 修正 quiescence、receipt、status query                      |
| `Runtime/Subsession/SubsessionPendingEvidence.fs` | 删除或加入 turn/epoch 身份                                     |
| `Runtime/Subsession/SubsessionEventRouter.fs`     | 未归属 idle 不得流入下一 turn                                    |
| `Runtime/Subsession/SubsessionService.fs`         | SessionClosed 统一清理                                      |
| `Hosts/OpenCode/PluginHooks.fs`                   | 删除会话时清除所有旁路 registry                                    |
| `Hosts/OpenCode/SubagentSpawn.fs`                 | abort 必须调用宿主                                            |
| `Hosts/OpenCode/ReviewerLoop.fs`                  | 外部取消贯通；finally 清理；最终迁移到 SubsessionService               |

## OMP

| 文件                                     | 必须整改                                 |
| -------------------------------------- | ------------------------------------ |
| `Hosts/Omp/SubsessionDispatch.fs`      | 删除无证据的 ordered marker；实现取消           |
| `Hosts/Omp/SubsessionHostAdapter.fs`   | 不再假定 idle 不会早到                       |
| `Hosts/Omp/Fallback/ActionExecutor.fs` | 使用统一 receipt，不解释 prompt 完成为 accepted |
| `Hosts/Omp/ReviewLoop.fs`              | finally 清理和 host abort               |
| `Hosts/Omp/ExecutorTools.fs`           | 等待目标 message/turn，不等待任意 idle         |
| `Runtime/Messaging/OmpHostBindings.fs` | 定义并测试稳定宿主契约                          |

## Mux 与万象阵

| 文件区域                                        | 必须整改                               |
| ------------------------------------------- | ---------------------------------- |
| `Hosts/Mux/Fallback/*`                      | nudge 返回值转换为逻辑 receipt；明确 abort 能力 |
| `Hosts/Mux/EventHook.fs`                    | 接入每 session actor                  |
| Mux delegate/continue service               | 接入统一 dispatch identity             |
| `Runtime/Wanxiangzhen/SessionIo.fs`         | API 缺失显式失败                         |
| `Runtime/Wanxiangzhen/CoordinatorReplay.fs` | best-effort 通知具备错误日志和幂等            |

---

# 十、真实 E2E 测试矩阵

当前测试数量很多，但关键 prompt 路径的测试证据质量不足。仓库的 coverage manifest 本身仍将大量 child session、recovery、isolation 和 lifecycle 行为标记为 not-covered；P0 canary 主要证明插件能启动和工具可见，并不能证明真正创建了正确 child session、正确取消或正确关联。

以下场景必须通过真实宿主进程测试，不接受只调用插件函数的 integration test 代替。

## 10.1 Prompt 契约

1. prompt 在请求入队后立即返回；
2. prompt 在 user message 创建后返回；
3. prompt 在整轮 assistant 完成后返回；
4. `chat.message` 早于 prompt Promise；
5. busy 早于 prompt Promise；
6. assistant 早于 prompt Promise；
7. idle 早于 prompt Promise；
8. prompt 返回不包含 message ID；
9. metadata 被剥离；
10. metadata 被移动到其他层级；
11. prompt 拒绝且没有创建 message；
12. prompt 抛错但宿主实际上已创建 message。

## 10.2 事件乱序

必须测试以下排列：

* busy → assistant → idle；
* assistant → idle，无 busy；
* error → idle；
* idle → late assistant；
* duplicate busy；
* duplicate idle；
* status idle 与 event idle 同时到达；
* abort result → late assistant；
* session delete → late prompt receipt；
* human message → stale idle；
* stale idle → next turn request。

## 10.3 Nudge

* 一个 idle 最多产生一个物理 prompt；
* 两个重复 idle 不重复发送；
* fallback 活跃时不发送；
* fallback terminal 后恢复发送；
* 自动 nudge 不增加 human turn；
* API 不可用时明确失败；
* event store 失败时不假装 NotNeeded；
* 两个 session 同时 nudge 互不污染；
* session delete 后所有 map 为空；
* 重启不会重复已经 accepted 的 nudge。

## 10.4 Continue

* stop finish reason 触发一次 continue；
* human message 抢先时旧 continue 不发送；
* 已发送后 human cancel 只取消旧 dispatch；
* prompt 整轮完成后才返回时不会 late abort；
* 缺失 continuation ID 的 busy/idle 不会被错误归属；
* 同 session nudge 与 fallback 竞争时只有一个 owner；
* 两个 session 使用同模型时 governor 不串扰；
* 重启处于 TransportStarted 时不盲目重发。

## 10.5 Sub-session

* 创建真实 child session；
* parentID 正确；
* agent/model/tools 正确；
* 两个 child 并发时结果不会交换；
* run 1 的 stale idle 不会完成 run 2；
* dispatch 中取消；
* accepted 后取消；
* assistant 输出中取消；
* abort 后 late idle；
* session delete 时取消；
* 重复 turn ID；
* child prompt 拒绝；
* child 空输出；
* child error 后 idle；
* restart 后恢复 active child；
* 所有 pending receipts 和 evidence 最终为零。

## 10.6 Reviewer

* 父任务 abort 时 child 真正停止；
* 本地 caller 不再等待时宿主也不继续运行；
* prompt 抛错后 child 被清理；
* reviewer parse 失败后 registry 被清理；
* session 删除后 reviewer 不再产生消息。

---

# 十一、测试基础设施也必须整改

## 11.1 禁止 synthetic prompt 自动旁路

严格 mock provider 当前对 synthetic nudge/continue 过度友好，容易直接返回固定完成结果，无法覆盖：

* 多工具调用；
* 空输出；
* 重试；
* 延迟；
* error；
* abort；
* 乱序；
* metadata 丢失。

必须让每个测试显式声明宿主行为脚本。

## 11.2 生产时序不能在测试中被关闭

Retry governor、延时、异步 receipt 若在测试模式下全部变为零，会恰好消除生产竞态。

测试应使用可控虚拟时钟，而不是删除时序。

## 11.3 E2E 必须有独立 Oracle

不能只断言最终文本出现。至少要断言：

* 创建了多少 session；
* session parentID；
* 发送了多少物理 prompt；
* user message ID；
* assistant parentID；
* abort 次数；
* NDJSON 事件序列；
* pending registry 最终大小；
* actor 最终状态；
* 无未处理 Promise rejection；
* 无残留 child session。

## 11.4 CI 分层

建议固定四层：

| 层级                        | 目标                                 |
| ------------------------- | ---------------------------------- |
| Kernel model tests        | 决定函数、状态不变量                         |
| Adapter contract tests    | 每个宿主的真实 API 与事件语义                  |
| Deterministic integration | actor、effect runner、event store 闭环 |
| Real process E2E          | 启动真实 OpenCode/OMP/Mux 进程验证         |

只有真实进程 E2E 可以宣称 prompt-dependent feature 可用。

---

# 十二、发布硬门槛

以下全部满足前，不得宣布修复完成。

1. 一个逻辑 DispatchId 最多对应一个未被明确判定失败的物理 prompt；
2. 插件自动 prompt 永远不会增加 human turn；
3. assistant 不可能结算到错误 dispatch；
4. 无身份 idle 不可能完成具体 turn；
5. stale abort 不可能杀死新一轮；
6. 所有 pending waiter 最终 exactly once 完成；
7. session delete 后所有 registry、timer 和 actor 状态归零；
8. restart 不会重复发送已 HostAccepted 的请求；
9. API 缺失和 event-store 错误不会被报告为成功；
10. Nudge、Fallback、Subsession 不再各自定义 prompt success；
11. legacy continuation 路径已删除，而不是仅标注 deprecated；
12. OpenCode P0 prompt、child、recovery、cleanup 行为全部达到 real-e2e；
13. OMP 和 Mux 的能力限制有明确测试和文档；
14. 所有 race 测试可重复运行数百次，无偶发失败；
15. trace 能由一个 DispatchId 还原完整生命周期。

---

# 十三、建议拆分的实施批次

## PR 1：宿主契约与止血

只做：

* ~~OpenCode 真实 prompt contract probes~~ ✅ 已完成（EventProbe 基础设施已就绪，具体 probe 场景在 PR2 统一 dispatch 时一并验证）；
* ~~禁止 generation-only matching~~ ✅ 已完成；
* ~~missing API 显式失败~~ ✅ 已完成；
* ~~session 单 synthetic prompt 限制~~ ✅ 已完成（PendingTurnReceipt.register 现在检查同 session 是否已有 active dispatch）；
* ~~人类消息去重前置~~ ✅ 已完成；
* ~~quiescence 明确逻辑错误修复~~ ✅ 已完成（S-04/S-05：isMessageMatch 现在检查 parts metadata nonce）；
* ~~补齐结构化 trace~~ ✅ 已完成（CommandProcessor.commitCommand 现在发射 TelemetryStateTransition）。

这一批必须小而明确，禁止夹带架构美化。

## PR 2：统一 PromptDispatch 与 Session Actor

完成：

* 标准 dispatch identity；
* 标准 receipt；
* 每 session mailbox；
* effect result 回投；
* exactly-once waiter；
* session close 清理；
* 新 continuation 主路径接入。

## PR 3：Nudge 与 Fallback 迁移

完成：

* Nudge 全生命周期；
* Fallback 新状态机；
* OpenCode message ID/parentID correlation；
* 删除旧 ActionExecutor 路径；
* 真实乱序 E2E。

## PR 4：Sub-session 与 Reviewer

完成：

* receipt cancel；
* stale evidence 根除；
* scoped abort；
* reviewer 统一；
* child session 全链路 E2E。

## PR 5：OMP、Mux、万象阵

完成跨宿主统一和能力降级说明。

---

# 十四、对开发过程的严格要求

本问题已经不允许再出现以下做法：

* 看到测试失败就补一个特殊 if；
* 通过 catch-all 让错误“不要影响主流程”；
* 以“宿主通常按这个顺序”为理由写状态机；
* 用 generation、时间接近或 session ID 代替 correlation；
* 用零宽文本承担 provenance；
* 在测试里直接调用内部 Hook，却称为 E2E；
* 新旧实现并存，声称以后再清理；
* 将 no-op cancel 命名成 CancelPendingDispatch；
* 将 prompt Promise resolve 命名成 HostAccepted；
* 将数组非空误写成目标消息存在；
* 将 fire-and-forget 当成异步解耦，却没有 supervisor；
* 将宿主 API 缺失当作成功降级。

开发者确实面对的是跨宿主、事件驱动和崩溃恢复交织的高难度问题，但难度不能成为保留模糊语义的理由。状态机项目最忌讳“看起来差不多”；每个状态名都必须对应可证明的事实。

---

# 十五、审计边界

本结论来自完整仓库静态审计，能够确认上述代码级错误、架构分裂和测试缺口。

OpenCode、OMP、Mux 各自的真实 prompt 返回时刻、metadata 保留方式和事件顺序，仍必须通过当前安装版本的真实宿主契约测试确认。这里不能凭文档或记忆替代运行证据。

但无论真实宿主最终表现是哪一种，当前实现依赖互相矛盾的假设、允许模糊归属并存在无效取消，已经足以判定需要整体收口，而不是继续局部修补。

---
# OpenCode 平台全链路 E2E 重建与覆盖清单

## 一、结论先行

当前 OpenCode 测试体系的主要问题不是**用例数量少**，而是**层级命名失真、断言过弱、测试基础设施会产生假绿**。

必须首先完成以下三件事：

1. 将直接导入插件、手工调用 hook 的 `opencode-harness.js` 降级为 **plugin-contract / host-integration**，不得再计入 E2E。
2. 重写真正启动 `opencode serve` 的 harness，消除 singleton、共享工作区、空 `dispose()`、宽松 mock、错误 idle 判定。
3. 禁止单独用“LLM 请求中出现工具名”作为通过条件。每条 E2E 至少验证：

   * 宿主接受了输入；
   * 插件执行了目标逻辑；
   * 外部副作用或持久化事实正确；
   * 最终 session 状态正确；
   * 资源已清理；
   * 不应发生的副作用没有发生。

当前仓库确实有一套启动真实 `opencode serve`、通过 HTTP API 创建 session 和发送 prompt 的 harness；但同时还存在另一套直接 `import Plugin.js`、构造假 client、手工触发 `tool.execute.before`、`event`、message transform 的 harness。这两套测试目前都以类似 E2E 的名字出现，必须彻底分层。

---

# 二、当前测试审计

## 2.1 真 E2E 与伪 E2E

### A. 真正接近 E2E 的路径

`e2e/harness.js` 会：

* 启动 mock LLM HTTP server；
* 创建临时项目；
* 执行 `opencode serve --port 0`；
* 通过 OpenCode HTTP API 创建 session；
* 通过 `/session/:id/prompt_async` 发送消息；
* 订阅 `/event`；
* 自动响应 permission；
* 读取 session、message、provider 和 `.wanxiangshu.ndjson`。

这条链路可以称为 E2E，因为它覆盖了：

```text
测试代码
→ HTTP
→ opencode server
→ opencode session runner
→ plugin loader
→ 万象术 hooks/tools
→ mock provider
→ tool execution
→ message/event/persistence
```

但它目前存在严重的隔离和判定问题。

### B. 不能再称为 E2E 的路径

`e2e/opencode-harness.js` 会直接：

* `import` 编译后的插件；
* 构造假的 `client.session.*`；
* 直接调用 `plugin.default(...)`；
* 手工执行工具函数；
* 手工调用 before/after hook；
* 手工伪造 `session.idle`、`stream-abort` 等事件。

这条链路绕过了：

* OpenCode 插件加载器；
* OpenCode session runner；
* AI SDK 工具调度；
* 工具 schema 校验；
* permission 流程；
* OpenCode message persistence；
* 真实事件分发；
* abort 与 runner 生命周期；
* compaction 调度；
* session API 参数编码。

因此应改名为：

```text
e2e/opencode-harness.js
→ integration/opencode-plugin-contract-harness.js
```

相关测试分别改为：

```text
OpencodePluginNudgeTests
→ OpencodeNudgeHostIntegrationTests

OpencodePluginContinueRecoveryTests
→ OpencodeContinueContractTests

OpencodePluginTestsPart*
→ OpencodePluginContractTestsPart*
```

这些测试仍然有价值，但只能证明“插件函数在人工输入下工作”，不能证明“安装到 OpenCode 后工作”。

---

## 2.2 当前真实 E2E 中最典型的敷衍断言

现有测试包含大量以下模式：

```fsharp
do! toolRound harness sessionID "write" args "write file"
chk "e2e.write.tool-called" (containsTool harness "write")
```

这只能证明模型请求中的 tools 列表或调用记录里出现过 `write`，不能证明：

* OpenCode 接受了 tool call；
* tool schema 验证通过；
* before hook 没有错误阻断；
* 文件真的被写入；
* 内容正确；
* after hook 正常执行；
* tool result 被写回消息；
* session 最终正常 idle。

诸如“覆盖写”“空文件”“Unicode”“大文件”目前也大多只断言 `containsTool "write"`。`fuzzy_find` 无结果、executor JavaScript、websearch、webfetch 等也存在同类问题。

### 必须删除的单独断言模式

以下断言不能单独构成 E2E：

```text
containsTool(...)
body.Contains(...)
command returned 200
waitForCalls(1)
waitForIdle() = true
NDJSON contains substring
没有抛异常
```

它们可以保留为辅助断言，但必须配合事实性 oracle。

---

## 2.3 已经相对有价值的用例

现有测试中有几类可保留并加强：

1. `/loop` 后检查 `loop_activated` 落入 NDJSON。
2. 取消 `/loop` 后检查 `loop_cancelled`。
3. todowrite 后检查 `work_backlog_committed`。
4. context budget 用真实 OpenCode session token usage 和 provider limit 做判断。
5. browser MCP 结果被送回下一轮 LLM。
6. 多轮历史能从 session messages 读回。

这些已经具备“宿主事实 + 插件事实”的雏形，但仍需增加结构化事件解析、session 归属、唯一性和最终状态断言。

---

# 三、先修 Harness：这是所有新增用例的前置条件

## 3.1 删除宿主 singleton

当前 `HostSingletonManager` 按 variant 复用同一个宿主进程，而忽略：

* `contextLimit`；
* plugin 配置；
* AGENTS.md；
* fallback 配置；
* permission 配置；
* fixture 文件；
* mock provider 行为；
* 工作区初始状态。

这会导致后启动的测试表面传入了新配置，实际上仍使用第一条测试创建的宿主。

例如：

```text
测试 A：contextLimit = 100000
测试 B：contextLimit = 20000
```

若两者复用 `opencode` singleton，测试 B 可能仍运行在 100000 的 provider 配置上。

### 修改要求

默认必须：

```text
一个 Scenario
= 一个临时 HOME
+ 一个临时 XDG
+ 一个临时工作区
+ 一个 mock provider
+ 一个 opencode serve 进程
```

只有专门测试“同一宿主多 session 隔离”时，才允许一个进程承载多个 session。

不得为了缩短时间把不同配置的测试复用在同一个宿主中。

---

## 3.2 实现真正的 dispose

当前真实 harness 的 `dispose()` 是空操作，宿主只能依赖进程退出时全局清理。这会掩盖：

* opencode 子进程泄漏；
* mock LLM server 泄漏；
* SSE reader 泄漏；
* PTY 进程泄漏；
* lock 文件泄漏；
* session actor 泄漏；
* 临时目录污染。

### 正确清理顺序

每个 scenario 的 `finally` 中执行：

```text
1. 停止继续发送 mock 响应
2. abort SSE reader
3. 等待 SSE reader 正常退出
4. 请求或触发 session abort
5. 等待 session idle / terminated
6. kill 所有测试创建的 PTY
7. SIGTERM opencode
8. 最多等待一个短 deadline
9. 未退出则 SIGKILL
10. await child exit
11. 关闭 mock provider
12. 检查无活跃 socket
13. 检查无已知子进程
14. 删除 lock
15. 删除临时 HOME 和工作区
```

清理失败必须让测试失败，不能吞掉所有异常。

---

## 3.3 完全隔离环境变量

当前 harness 的 `HOME` 可能继续使用执行测试用户的真实 HOME，`XDG_CACHE_HOME` 也可能跨测试共享。

必须统一设置：

```text
HOME=<scenario>/home
USERPROFILE=<scenario>/home
XDG_DATA_HOME=<scenario>/xdg/data
XDG_CONFIG_HOME=<scenario>/xdg/config
XDG_CACHE_HOME=<scenario>/xdg/cache
XDG_STATE_HOME=<scenario>/xdg/state
TMPDIR=<scenario>/tmp
```

并清理或覆盖可能影响 OpenCode 的变量：

```text
OPENCODE_CONFIG
OPENCODE_CONFIG_CONTENT
OPENCODE_AUTH_CONTENT
OPENCODE_PERMISSION
OPENAI_API_KEY
ANTHROPIC_API_KEY
OLLAMA_*
HTTP_PROXY
HTTPS_PROXY
NO_PROXY
SQUAD_*
WANXIANG*
```

每条测试只显式加入自己需要的变量。

---

## 3.4 重写 Mock LLM：任何意外请求都必须失败

当前 mock 有两个危险行为：

1. 期望队列为空时默认返回 `"ok"`；
2. 如果队首 tool 与当前 tools 不匹配，会从后面的期望中寻找匹配项并提前取出。

这会掩盖：

* 多余 LLM 调用；
* 调用顺序错误；
* 错误 agent 调用了错误工具；
* nudge 重复；
* reviewer 提前启动；
* fallback 意外多跑一轮。

### 新 mock 的规则

默认 strict：

```text
没有匹配 expectation
→ HTTP 500
→ 记录 UnexpectedLlmRequest
→ scenario 失败
```

不得自动重排 expectation。

每个 expectation 应支持：

```ts
{
  id: "main-turn-1",
  match: {
    sessionId?: "...",
    model?: "test/test-model",
    requiredTools?: ["write"],
    forbiddenTools?: ["return_reviewer"],
    containsText?: ["任务文本"],
    messageCount?: 3
  },
  respond: {
    type: "tool-call" | "text" | "error" | "disconnect",
    ...
  }
}
```

并增加以下故障模式：

* 延迟首 token；
* 延迟 `[DONE]`；
* 中途断开 SSE；
* 返回 400/401/408/429/500/503；
* retryable API error；
* context overflow；
* 空 assistant 内容；
* 只有 reasoning、无 text；
* tool call 参数分片；
* malformed JSON arguments；
* tool-call-as-text；
* 重复 tool call ID；
* 正常 tool call 后错误；
* 永不结束，等待 abort；
* usage 缺失；
* usage 延迟到最后一帧；
* title generation 旁路请求。

测试结束时必须断言：

```text
remaining expectations = 0
unexpected requests = 0
```

---

## 3.5 修正 idle 判定

当前 `waitForIdle` 将“status map 中没有这个 session”也当成 idle：

```text
!status || status.type === "idle"
```

这是典型假绿来源。查不到状态可能表示：

* session 尚未注册；
* API 返回结构改变；
* directory header 错误；
* session 已被错误删除；
* status 请求失败后得到空对象；
* 查询了错误的 session ID。

### 正确策略

一次 prompt 必须形成明确生命周期：

```text
观察到 busy/running
→ 观察到目标 assistant terminal message
→ 观察到 session.status idle
```

等待 idle 时至少需要满足：

```text
曾观察到该 session 的非 idle 状态
并且
后来观察到该 session 的 idle 状态
```

对于极快完成的回合，可以用事件序列或 assistant terminal message 证明回合确实运行过，不能仅依赖 status map 缺项。

OpenCode 的 `session.idle` 本身不携带停止原因，而且 event hook 是 fire-and-forget；不同事件处理可能并发、重复或交错。因此 E2E 必须收集原始事件并按 session、message、part 和时间进行关联，不能把单个 idle 当作成功。

---

## 3.6 建立完整 EventProbe

当前 SSE 监听器只处理 permission。

应新增：

```ts
class EventProbe {
  allEvents
  bySession(sessionID)
  awaitEvent(predicate, deadline)
  awaitSequence(predicates, deadline)
  count(type, sessionID)
  assertNever(predicate)
  dumpOnFailure()
}
```

至少记录：

* 原始事件 JSON；
* 接收序号；
* 接收时间；
* type；
* sessionID；
* messageID；
* partID；
* tool call ID；
* error name；
* finish reason；
* status。

每次失败自动输出：

```text
最后 100 个 OpenCode events
所有 mock LLM requests/responses
session messages
session status
NDJSON
opencode stdout/stderr
进程树
工作区文件树
```

---

## 3.7 不再丢弃 opencode stderr

当前真实 harness 对 opencode 使用：

```text
stdio: ['pipe', 'pipe', 'ignore']
```

必须捕获 stderr 到环形缓冲区。

测试成功时可以不输出；失败时完整附加。插件加载错误、Unhandled rejection、schema 错误、端口问题和 provider 错误通常只会出现在 stderr。

---

## 3.8 建立统一 Oracle

每条 E2E 最少从以下 oracle 中选三个：

| Oracle     | 验证内容                                |
| ---------- | ----------------------------------- |
| HTTP       | API status、response schema          |
| Event      | busy、message、tool、error、idle 序列     |
| Message    | 用户消息、assistant、tool part、error part |
| Filesystem | 文件内容、权限、目录、git diff                 |
| Process    | PID 存活、退出码、无泄漏                      |
| NDJSON     | 结构化事件、唯一性、顺序、session 归属             |
| Replay     | 重启后投影与重启前一致                         |
| LLM        | 输入工具集、system prompt、history、模型      |
| Negative   | 禁止工具未暴露、错误副作用未发生                    |
| Cleanup    | session、PTY、child、lock、temp 均释放     |

### 强制断言模板

每条用例都按此格式写：

```text
Given
- 初始项目、配置、session、mock 脚本

When
- 通过 OpenCode HTTP API 执行用户可见操作

Then — Transport
- 请求成功或得到预期错误

Then — Host
- 观察到预期事件和 terminal message

Then — Domain
- 插件业务结果正确

Then — Durable
- NDJSON 或工作区事实正确

Then — Negative
- 不应发生的调用/事件/文件不存在

Then — Cleanup
- 无资源泄漏
```

---

# 四、建议目录结构

```text
e2e/opencode/
  harness/
    process-host.ts
    isolated-env.ts
    opencode-client.ts
    event-probe.ts
    strict-mock-provider.ts
    permission-controller.ts
    process-probe.ts
    filesystem-oracle.ts
    ndjson-oracle.ts
    session-oracle.ts
    diagnostics.ts
    scenario.ts

  fixtures/
    projects.ts
    configs.ts
    messages.ts
    provider-scripts.ts

  specs/
    bootstrap/
    config/
    tools-file/
    tools-search/
    tools-executor/
    tools-pty/
    tools-web/
    subagents/
    continue/
    review/
    nudge/
    fallback/
    context-budget/
    compaction/
    lifecycle/
    event-sourcing/
    concurrency/
    recovery/

  manifests/
    behavior-coverage.ts
```

原有 F# 测试可继续作为 scenario 编排层，但进程控制、SSE、mock provider、故障注入适合放在 TypeScript harness 中。

---

# 五、OpenCode 全量 E2E 用例清单

以下只覆盖 OpenCode 主平台。本阶段暂不覆盖：

* Mux；
* OMP；
* Mimocode；
* Mimocode TUI；
* 万象阵；
* 真实付费 LLM。

---

## A. 启动、安装和配置

### P0

* [ ] **OC-BOOT-002** `/command` 中存在 `loop`。
* [ ] **OC-BOOT-003** LLM 请求中出现所有应注册的自定义工具。
* [ ] **OC-BOOT-004** 插件缺失时建立 baseline，确认相关工具、命令、prompt 均不存在。
* [ ] **OC-BOOT-005** 插件路径错误时宿主启动或 plugin load 明确失败，stderr 可诊断。
* [ ] **OC-BOOT-006** 无 `AGENTS.md` 时使用默认配置且不崩溃。
* [ ] **OC-BOOT-007** 合法 `AGENTS.md` 配置对 agent、工具和 prompt 生效。
* [ ] **OC-BOOT-008** 损坏 frontmatter 不污染其他配置，不默默加载半份配置。
* [ ] **OC-BOOT-009** 两个完全独立的 scenario 不共享 session、文件、NDJSON 和 provider 调用。
* [ ] **OC-BOOT-010** 同一工作区重启宿主后插件只初始化一次，不重复注册 hook。

### P1

* [ ] fallback 配置不存在、为空、合法、损坏四种路径。
* [ ] MCP fixture 可启动；fixture 启动失败时 browser 返回结构化错误。
* [ ] 配置中的自定义 agent 标量保留，插件只补默认值。
* [ ] 用户显式 deny 权限不得被插件覆盖成 allow。
* [ ] 当前兼容基线锁定为仓库调查所对应的 OpenCode v1.17.13；升级宿主时必须单独跑兼容套件。

---

## B. 工具注册、Schema 与权限

OpenCode 当前注册的自定义工具包括 coder、inspector、browser、continue、executor、五个 PTY 工具、fuzzy 系列、web 系列、review 工具以及 meditator。现有真实 E2E 没有覆盖完整工具表，尤其缺少 PTY 和 `fuzzy_continue`。

### P0

* [ ] **OC-SCHEMA-002** executor 的真实 schema 包含必填 `max_bytes`。
* [ ] **OC-SCHEMA-003** 修改类工具 schema 注入 `warn_tdd`。
* [ ] **OC-SCHEMA-004** executor 等高风险工具注入 `warn`。
* [ ] **OC-SCHEMA-005** 子代理工具注入 `warn_reuse`。
* [ ] **OC-SCHEMA-006** todowrite schema 被完整替换为 work backlog schema。
* [ ] **OC-SCHEMA-007** 缺少必填业务参数时由 OpenCode/AI SDK 拒绝，execute 不得运行。
* [ ] **OC-SCHEMA-008** 控制字段在 before hook 中原地删除，宿主实际 execute 收不到控制字段。
* [ ] **OC-SCHEMA-009** after hook 看到的 args 与实际执行参数关联正确。
* [ ] **OC-PERM-001** manager、coder、inspector、browser、reviewer、meditator 的工具可见性符合权限矩阵。
* [ ] **OC-PERM-002** 禁止工具不仅不执行，而且不出现在发给对应 agent 的 tools 中。
* [ ] **OC-PERM-003** permission=deny 时外部副作用为零。
* [ ] **OC-PERM-004** permission=once 时仅本次执行。
* [ ] **OC-PERM-005** permission responder 不存在时请求保持等待，随后可 abort。

上游调用链要求 before hook 对原始 args 做原地修改；仅替换 `output.args` 不能保证实际工具收到新对象。这个行为必须由真实 OpenCode 工具调用验证，不能只直接调用 hook。

---

## C. System prompt、消息变换和 Caps

### P0

* [ ] **OC-MSG-001** system transform 包含真实工作目录。
* [ ] **OC-MSG-002** Caps prelude 在第一轮出现。
* [ ] **OC-MSG-003** 同一 epoch 内不会重复注入 Caps。
* [ ] **OC-MSG-004** 新 session 独立注入，不受旧 session 污染。
* [ ] **OC-MSG-005** tool result 完整回填到下一轮模型上下文。
* [ ] **OC-MSG-006** tool error 以正确 part 形态回填。
* [ ] **OC-MSG-007** Unicode、换行和大 tool output 不被错误转义。
* [ ] **OC-MSG-008** 不完整 tool-call/tool-result 对不会产生损坏的 model messages。
* [ ] **OC-MSG-009** 控制字段不会泄漏给底层 builtin tool。
* [ ] **OC-MSG-010** agent 工具过滤不修改其他 session 的消息。

### P1

* [ ] compaction 前后 Caps epoch 行为正确。
* [ ] summary、历史消息和 backlog 按规定顺序进入模型上下文。
* [ ] filterCompacted 后的切片不会被误当作完整历史。
* [ ] 长历史中 tool call ID 与 result ID 始终配对。
* [ ] 恶意文本伪装成内部 metadata 不会被识别为系统 nudge。

---

## D. OpenCode 内置文件工具

当前“覆盖写、空内容、Unicode、大文件”等测试大多只确认 tool call 出现，必须全部改成文件事实断言。

### P0

* [ ] **OC-FILE-007** read 不存在文件：tool part 为 error，session 仍可继续下一轮。
* [ ] **OC-FILE-009** 缺少 `warn_tdd` 时执行但不产生批评消息。
* [ ] **OC-FILE-010** `warn_tdd` 不会写入文件工具真实参数。
* [ ] **OC-FILE-011** permission deny 时文件绝对不存在。
* [ ] **OC-FILE-012** 两个 session 写同名相对路径时，按设计验证共享或隔离语义。

### P1

* [ ] edit/patch 成功、上下文不匹配、重复 patch。
* [ ] 目录不存在时的错误。
* [ ] 路径穿越。
* [ ] symlink 指向工作区外。
* [ ] 只读文件。
* [ ] 超大输出和 max size 边界。
* [ ] 并发修改同一文件时不产生静默损坏。

---

## E. Fuzzy 搜索

### P0

* [ ] **OC-FUZZY-002** 无匹配返回明确空结果，不是仅“工具被调用”。
* [ ] **OC-FUZZY-004** 多 pattern 按规定形成分块结果。
* [ ] **OC-FUZZY-005** `fuzzy_continue` 能取得下一页且无重复。
* [ ] **OC-FUZZY-006** iterator 耗尽后返回完成状态。
* [ ] **OC-FUZZY-007** 错误 iterator ID 不读取其他 session 的结果。
* [ ] **OC-FUZZY-008** session 删除后 iterator 被清理。

### P1

* [ ] 二进制文件。
* [ ] Unicode 文件名。
* [ ] 大目录分页。
* [ ] grep 子进程失败。
* [ ] 搜索中途 abort。
* [ ] 两个 session 同时分页，cursor 不串线。

---

## F. Executor

### P0

* [ ] **OC-EXEC-003** 非零 exit code 被结构化返回。
* [ ] **OC-EXEC-004** stderr 可见且不被当成成功 stdout。
* [ ] **OC-EXEC-005** cwd 为目标工作区。
* [ ] **OC-EXEC-006** `max_bytes` 小于输出时执行截断/摘要路径。
* [ ] **OC-EXEC-007** `max_bytes` 边界值无 off-by-one。
* [ ] **OC-EXEC-008** 缺少 `warn` 时产生协议批评。
* [ ] **OC-EXEC-009** 网络错误转换为预期 domain error。
* [ ] **OC-EXEC-010** livelock guard 在真实连续调用中触发。
* [ ] **OC-EXEC-011** permission deny 时进程未启动、文件无副作用。
* [ ] **OC-EXEC-012** session abort 会终止长时间命令。

### P1

* [ ] 子进程派生孙进程后 abort，整个进程组被清理。
* [ ] 命令产生大量 stdout/stderr。
* [ ] 命令被 signal 杀死。
* [ ] shell quoting 与 Unicode 参数。
* [ ] 环境变量白名单。
* [ ] 并发 executor 的隔离。
* [ ] session 删除后的 executor 状态清理。

---

## G. PTY 五工具

这是当前 OpenCode E2E 最大的空白之一。

### P0

* [ ] **OC-PTY-002** `pty_list` 能看到新建 PTY。
* [ ] **OC-PTY-003** `pty_read` 读取初始输出。
* [ ] **OC-PTY-004** `pty_write` 写入 stdin，随后读到响应。
* [ ] **OC-PTY-006** kill 后 list 不再报告 running。
* [ ] **OC-PTY-007** 无效 PTY ID 返回错误，不影响其他 PTY。
* [ ] **OC-PTY-008** session deleted 事件自动清理其所有 PTY。
* [ ] **OC-PTY-009** 不同 session 的 PTY 不得互相读写。
* [ ] **OC-PTY-010** permission deny 时不产生子进程。

### P1

* [ ] 输出分页和 cursor。
* [ ] ANSI、Unicode、无换行输出。
* [ ] 子进程自行退出。
* [ ] 多次 kill 幂等。
* [ ] opencode 宿主被强杀后无孤儿 PTY。
* [ ] PTY manager 初始化失败的错误路径。

---

## H. Websearch、Webfetch 与 Browser MCP

### P0

* [ ] **OC-WEB-002** `what_to_summarize` 实际进入子代理 prompt。
* [ ] **OC-WEB-003** provider HTTP 500 转为 tool error。
* [ ] **OC-WEB-004** malformed search JSON 不导致宿主崩溃。
* [ ] **OC-WEB-005** webfetch 正确返回 fixture 内容。
* [ ] **OC-WEB-006** webfetch 拒绝 loopback、私网、链路本地和元数据地址。
* [ ] **OC-WEB-007** redirect 到私网仍被拒绝。
* [ ] **OC-WEB-008** browser 启动 browser 子代理。
* [ ] **OC-WEB-009** browser 子代理可调用 MCP 工具。
* [ ] **OC-WEB-010** MCP 结果进入 browser 后续模型轮次。
* [ ] **OC-WEB-011** browser 最终文本回到父 session tool result。
* [ ] **OC-WEB-012** MCP 进程失败时 child session 和资源都被清理。

---

## I. 子代理与物理子 Session

当前 coder、inspector、meditator 大多只验证工具出现在模型调用中。真正需要验证的是 OpenCode `session.create → session.prompt → child events → result extraction → cleanup`。

### P0

* [ ] **OC-SUB-002** child 的 parentID 指向调用方 session。
* [ ] **OC-SUB-003** child 使用 coder agent。
* [ ] **OC-SUB-004** coder prompt 含用户 intent 和工作目录。
* [ ] **OC-SUB-006** inspector 同样完成真实 child 链路。
* [ ] **OC-SUB-007** meditator 使用指定 methodology 内容。
* [ ] **OC-SUB-008** child 的工具权限符合对应角色。
* [ ] **OC-SUB-009** child 空输出触发规定的 continuation/fallback。
* [ ] **OC-SUB-010** child tool-only finish 不被提前视为完成。
* [ ] **OC-SUB-011** 父 session abort 会取消 child。
* [ ] **OC-SUB-012** child API error 被父工具结构化返回。
* [ ] **OC-SUB-013** child session 创建失败不登记伪 registry 状态。
* [ ] **OC-SUB-014** child 完成后 registry、actor、pending receipt 清理。
* [ ] **OC-SUB-015** 两个并发 child 的结果不会交换。

### P1

* [ ] 多 intent 调度。
* [ ] 部分 child 成功、部分失败。
* [ ] child 完成与 abort 竞态。
* [ ] prompt 已被宿主接受但 transport 断开。
* [ ] child assistant terminal event 先于 prompt Promise 返回。
* [ ] child session 被外部删除。
* [ ] 主进程重启后 unfinished child reconcile。

---

## J. Continue 多轮恢复

现有 continue 用例主要运行在直接调用插件的人工 harness 中，必须移植到真实宿主。

### P0

* [ ] **OC-CONT-001** 第一次子代理返回 continuation handle。
* [ ] **OC-CONT-002** child 未完成时 continue 返回 still-running。
* [ ] **OC-CONT-003** child 完成后 continue 返回下一段。
* [ ] **OC-CONT-004** 最终页返回完成状态。
* [ ] **OC-CONT-005** 重复请求最终页幂等。
* [ ] **OC-CONT-006** 非法 continuation ID 明确失败。
* [ ] **OC-CONT-007** 其他 session 不能消费该 continuation。
* [ ] **OC-CONT-008** session 删除后 continuation 失效。
* [ ] **OC-CONT-009** 父 session abort 期间 continue 不复活任务。
* [ ] **OC-CONT-010** 宿主重启后按设计恢复或明确不可恢复。

---

## K. `/loop`、Submit Review 与 Reviewer

### P0

* [ ] **OC-REV-002** `loop_activated` 只追加一次。
* [ ] **OC-REV-003** event 中的 sessionID、task、generation 正确。
* [ ] **OC-REV-004** 已激活时再次 `/loop` 返回 already active，不重复 append。
* [ ] **OC-REV-005** 空参数 `/loop` 取消并追加 `loop_cancelled`。
* [ ] **OC-REV-006** submit_review 在无 active loop 时按规定拒绝或处理。
* [ ] **OC-REV-007** submit_review 创建真实 reviewer child session。
* [ ] **OC-REV-008** reviewer 只暴露 `return_reviewer` 等允许工具。
* [ ] **OC-REV-009** affectedFiles 和 report 完整进入 reviewer prompt。
* [ ] **OC-REV-010** 第一次 PERFECT 的双检语义正确。
* [ ] **OC-REV-011** 第二次 PERFECT 结束 review。
* [ ] **OC-REV-012** REVISE feedback 回到主 session 并保持 loop active。
* [ ] **OC-REV-013** invalid verdict 不错误结束 review。
* [ ] **OC-REV-014** reviewer 空输出触发 nudge，达到上限后终止。
* [ ] **OC-REV-015** submit_review `wip=true` 不错误完成任务。
* [ ] **OC-REV-016** review 进行中父 session abort，reviewer 被取消。
* [ ] **OC-REV-017** 并发两次 submit_review 只有一个获得锁。
* [ ] **OC-REV-018** 宿主重启后 active review 从 NDJSON 恢复。
* [ ] **OC-REV-019** accepted 后再次重启仍保持 inactive。
* [ ] **OC-REV-020** NDJSON append 失败时内存状态不得先行变更。

---

## L. Nudge

### P0

* [ ] **OC-NUDGE-002** nudge 文本精确符合当前协议。
* [ ] **OC-NUDGE-003** 重复 idle 不产生第二次 nudge。
* [ ] **OC-NUDGE-004** abort finish 抑制 nudge。
* [ ] **OC-NUDGE-005** stream-abort 抑制后续迟到 idle。
* [ ] **OC-NUDGE-006** assistant 无 text 但 loop active 时仍按规则判断。
* [ ] **OC-NUDGE-007** finish 字段缺失时用其他证据推导。
* [ ] **OC-NUDGE-008** todo 全完成时不产生工作 nudge。
* [ ] **OC-NUDGE-009** rejected review 触发修订 nudge。
* [ ] **OC-NUDGE-010** nudge prompt 的 nonce 能被识别为系统消息，不建立新 human turn。
* [ ] **OC-NUDGE-011** nudge send 失败产生失败事件，可在下次恢复。
* [ ] **OC-NUDGE-012** 两个 session 的 nudge 去重状态隔离。
* [ ] **OC-NUDGE-013** session 删除清理 nudge runtime。
* [ ] **OC-NUDGE-014** force-stop 后迟到事件不能复活 nudge。
* [ ] **OC-NUDGE-015** 事件顺序 idle→error 与 error→idle 得到一致结论。

---

## M. Fallback 与零宽 continuation

这是必须重点加强的真实链路。OpenCode 的 idle 不带原因，error、assistant finish、status 和 idle 可能交错；因此必须用真实事件序列做测试。

### P0

* [ ] **OC-FB-001** 空输出错误后向同一 session 注入单个零宽字符。
* [ ] **OC-FB-002** 注入内容严格等于规定零宽字符，不使用 XML continuation。
* [ ] **OC-FB-003** continuation 使用原 agent。
* [ ] **OC-FB-004** continuation 使用正确 model 和 variant。
* [ ] **OC-FB-005** 手工指定 model 的优先级高于自动捕获 model。
* [ ] **OC-FB-006** retryable provider error 路由到下一模型。
* [ ] **OC-FB-007** non-retryable error 不继续。
* [ ] **OC-FB-008** MessageAbortedError 不继续。
* [ ] **OC-FB-009** tool finish 后 idle，再迟到 EmptyOutputError，仍只继续一次。
* [ ] **OC-FB-010** error 先到、idle 后到，仍只继续一次。
* [ ] **OC-FB-011** 重复 error 不产生重复 continuation。
* [ ] **OC-FB-012** 重复 idle 不产生重复 continuation。
* [ ] **OC-FB-013** continuation 自身失败不会无限递归。
* [ ] **OC-FB-014** continuation 成功后 lease 正确 settled。
* [ ] **OC-FB-015** tool-call-as-text 能生成恢复 prompt。
* [ ] **OC-FB-016** 普通代码文本不会被误判为 tool call。
* [ ] **OC-FB-017** continuation nonce 不被识别为真人消息。
* [ ] **OC-FB-018** human 新消息会使旧 continuation 失效。
* [ ] **OC-FB-019** session abort 与 fallback dispatch 竞态安全。
* [ ] **OC-FB-020** session 删除清理所有 fallback runtime。
* [ ] **OC-FB-021** 宿主在 dispatch 后、settle 前崩溃，重启不重复注入。
* [ ] **OC-FB-022** 宿主在 append 后、dispatch 前崩溃，重启会继续未完成 effect。
* [ ] **OC-FB-023** 不同 session 的 generation、ordinal 和 lease 完全隔离。
* [ ] **OC-FB-024** fallback chain 耗尽后传播最终错误，不静默成功。

---

## N. Context Budget

现有 real-link context budget 用例值得保留，但要拆成多个独立 scenario，并消除 singleton 配置复用风险。

### P0

* [ ] **OC-CB-001** provider input limit 从真实 `/provider` 解析。
* [ ] **OC-CB-002** session model/provider 从真实 session API 解析。
* [ ] **OC-CB-003** usage 来自真实 session token 数据，不用字符串长度替代。
* [ ] **OC-CB-004** 阈值以下不注入 budget nudge。
* [ ] **OC-CB-006** 同一 phase 不重复注入。
* [ ] **OC-CB-007** backlog commit 后 phase reset 保留正确基线。
* [ ] **OC-CB-008** phase reset 后达到条件再次注入。
* [ ] **OC-CB-009** 不同 session budget 独立。
* [ ] **OC-CB-010** provider 缺 limit 时使用明确 fallback，不误用 output limit。
* [ ] **OC-CB-011** usage 缺失时不伪造确定值。
* [ ] **OC-CB-012** session model 切换后重新解析 limit。

---

## O. Compaction

### P0

* [ ] **OC-COMP-001** 真实触发 `experimental.session.compacting`。
* [ ] **OC-COMP-002** output context 包含 backlog projection。
* [ ] **OC-COMP-003** `compaction_started` 结构化写入。
* [ ] **OC-COMP-004** compaction 成功后 settled/compacted 事件正确。
* [ ] **OC-COMP-005** autocontinue hook 最终生效。
* [ ] **OC-COMP-006** compaction continuation 不建立 human turn。
* [ ] **OC-COMP-007** compaction 失败写 failed settle。
* [ ] **OC-COMP-008** compaction 失败不遗留 active compaction owner。
* [ ] **OC-COMP-009** 同一 compaction 的迟到事件不影响下一代。
* [ ] **OC-COMP-010** restart 后未完成 compaction 可恢复。
* [ ] **OC-COMP-011** 两个 session 同时 compact 不串状态。
* [ ] **OC-COMP-012** summary 后旧 tool output 不错误重新注入。

---

## P. Event Sourcing 与重启恢复

NDJSON 不能只用 `Contains "event_name"` 验证。

### 每条持久化用例必须检查

```text
每一非空行都能 JSON.parse
event kind 正确
session ID 正确
event ID 非空且唯一
causation/correlation 字段正确
generation/ordinal 单调
payload schema 正确
没有重复业务事件
fold 后状态正确
```

### P0

* [ ] **OC-ES-001** 首次 append 创建 NDJSON。
* [ ] **OC-ES-002** 多事件逐行合法，无拼接。
* [ ] **OC-ES-003** 并发 append 不丢失、不交叉半行。
* [ ] **OC-ES-004** 最后一行截断时启动能按规定处理。
* [ ] **OC-ES-005** 中间损坏行按规范跳过或失败。
* [ ] **OC-ES-006** replay 投影等于运行中投影。
* [ ] **OC-ES-007** 重启后 review 状态恢复。
* [ ] **OC-ES-008** 重启后 backlog 状态恢复。
* [ ] **OC-ES-009** 重启后 nudge 去重状态恢复。
* [ ] **OC-ES-010** 重启后 fallback lease 状态恢复。
* [ ] **OC-ES-011** 重启后 context phase 状态恢复。
* [ ] **OC-ES-012** session A 的事件不会折叠进 session B。
* [ ] **OC-ES-013** append 失败时内存状态不越过 durable 事实。
* [ ] **OC-ES-014** lock 文件异常残留可恢复。
* [ ] **OC-ES-015** 真实 kill -9 后文件保持到最后一个完整换行。

---

## Q. Session 生命周期、Abort 和事件竞态

### P0

* [ ] **OC-LIFE-001** 新真人消息产生 human-turn 事实。
* [ ] **OC-LIFE-002** 内部 nudge 不产生 human-turn。
* [ ] **OC-LIFE-003** child nonce 消息不产生 human-turn。
* [ ] **OC-LIFE-004** 正常回合观察到 running/busy 和 terminal idle。
* [ ] **OC-LIFE-005** API error 回合观察到 error 和 idle。
* [ ] **OC-LIFE-006** abort 产生 MessageAbortedError 或等价终止事实。
* [ ] **OC-LIFE-007** abort 后不会继续执行尚未开始的 tool。
* [ ] **OC-LIFE-008** tool 已执行完成、abort 迟到时结果不被错误抹除。
* [ ] **OC-LIFE-009** abort 与 provider `[DONE]` 同时发生时最多一个终态。
* [ ] **OC-LIFE-010** 重复 idle 幂等。
* [ ] **OC-LIFE-011** session.deleted 清理 PTY、fallback、compliance、queue、temp file 和 actor。
* [ ] **OC-LIFE-012** 删除 session A 不影响 session B。
* [ ] **OC-LIFE-013** event hook 内部异步工作最终可观测，不依赖固定 sleep。
* [ ] **OC-LIFE-014** session.post error 与 event error 两条入口不会重复执行恢复。
* [ ] **OC-LIFE-015** 用户在 idle 时立即发送下一 prompt，不与上一轮清理串线。

---

## R. 并发、隔离和压力

### P0

* [ ] **OC-CONC-001** 同一宿主创建 10 个 session，消息和状态不串线。
* [ ] **OC-CONC-002** 两个 session 同时 write 不污染彼此的 runtime 状态。
* [ ] **OC-CONC-003** 两个 session 同时 nudge，各发送一次。
* [ ] **OC-CONC-004** 两个 session 同时 fallback，continuation ID 独立。
* [ ] **OC-CONC-005** 两个 session 同时运行 child session，结果归属正确。
* [ ] **OC-CONC-006** 同一 session 多 tool call 顺序符合 OpenCode 调度语义。
* [ ] **OC-CONC-007** event 回调乱序注入后最终 fold 一致。
* [ ] **OC-CONC-008** 高频重复 idle/error 不造成 unbounded queue。
* [ ] **OC-CONC-009** 100 回合后 runtime store 不线性保留已结束 session 数据。
* [ ] **OC-CONC-010** 反复创建删除 session 后无 PTY、actor、iterator 和 lock 泄漏。
* [ ] **OC-CONC-011** mock provider 延迟时其他 session 仍可运行。
* [ ] **OC-CONC-012** opencode 重启后旧端口、SSE、provider socket 全部释放。

---

# 六、每个功能的最低覆盖公式

以后任何新功能进入 OpenCode，至少必须有五类 E2E：

```text
1 条 Happy Path
1 条 Boundary
1 条 Failure
1 条 Recovery/Restart
1 条 Isolation/Cleanup
```

例如新增工具 `foo`，禁止只写：

```text
foo appears in tools
foo.execute returns string
```

最低验收应为：

```text
FOO-001 正常执行并验证外部副作用
FOO-002 参数边界
FOO-003 底层依赖失败
FOO-004 执行中宿主重启或 abort
FOO-005 两个 session 隔离并清理资源
```

---

# 七、测试代码模板

```ts
await scenario("OC-FILE-001 writes exact bytes", async (t) => {
  await t.host.start({
    plugin: true,
    project: {
      "AGENTS.md": "- e2e workspace\n"
    }
  });

  const session = await t.client.createSession();

  t.provider.expectToolCall({
    id: "write-call",
    tool: "write",
    args: {
      filePath: "hello.txt",
      content: "你好\n",
      warn_tdd:
        "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated"
    }
  });

  t.provider.expectText({
    id: "final-answer",
    text: "done"
  });

  await t.client.prompt(session.id, "Write hello.txt");

  await t.events.awaitSessionTerminal(session.id);

  t.fs.expectFile("hello.txt", Buffer.from("你好\n", "utf8"));

  const messages = await t.client.messages(session.id);
  t.messages.expectSuccessfulToolResult(messages, "write");

  t.events.expectCount({
    sessionID: session.id,
    type: "session.error",
    count: 0
  });

  t.provider.expectSatisfied();
  await t.cleanup.expectClean();
});
```

注意：成功 oracle 不是 `tool called`，而是：

```text
真实文件字节
+ 成功 tool result
+ 正常 terminal session
+ 无 error event
+ mock expectation 全部消费
+ 无资源泄漏
```

---

# 八、迁移实施顺序

## 阶段 0：冻结和改名

* [ ] 当前测试全部保留，记录现有通过基线。
* [ ] 将 `opencode-harness.js` 相关套件改名为 integration/contract。
* [ ] 禁止它们进入 E2E 覆盖统计。
* [ ] 建立 `behavior-coverage.ts`，每项功能标明 unit、integration、real-e2e。

## 阶段 1：重写真实 harness

按顺序完成：

1. 去 singleton。
2. 真正 dispose。
3. HOME/XDG 全隔离。
4. 捕获 stderr。
5. strict mock provider。
6. EventProbe。
7. 正确 terminal 判定。
8. failure diagnostics。
9. restart API。
10. process/PTY leak probe。

在这一步完成前，不批量增加业务测试。

## 阶段 2：先打通 15 条 P0 金丝雀

首批只做：

```text
OC-BOOT-001
OC-SCHEMA-001
OC-FILE-001
OC-FILE-006
OC-EXEC-001
OC-PTY-001
OC-PTY-005
OC-FUZZY-003
OC-WEB-001
OC-SUB-001
OC-SUB-005
OC-REV-001
OC-NUDGE-001
OC-FB-001
OC-CB-005
```

这些能验证 harness 的每一种 oracle。

## 阶段 3：补齐主要功能域

建议顺序：

```text
文件与 schema
→ executor/PTY
→ 子代理/continue
→ review/nudge
→ fallback
→ context/compaction
→ event sourcing/restart
```

## 阶段 4：故障与竞态

集中实现：

* provider disconnect；
* API 429/500；
* append failure；
* kill -9；
* abort；
* duplicate event；
* event reorder；
* concurrent sessions；
* session delete；
* child crash。

## 阶段 5：CI 分层

### PR 必跑

```text
unit
integration
opencode-e2e-p0
```

### 合并后必跑

```text
opencode-e2e-full
restart-recovery
concurrency
```

### Nightly

```text
repeat-e2e-50
stress-100-sessions
latest-supported-opencode
optional-live-provider-smoke
```

---

# 九、硬性质量门禁

以下规则应写入测试审查规范：

1. 名称含 E2E 的测试必须启动真实 `opencode serve`。
2. 禁止 E2E 直接调用插件 hook。
3. 禁止 E2E 直接调用工具 `execute`。
4. 禁止单独以 `containsTool` 判成功。
5. 禁止队列为空时 mock 自动返回成功。
6. 禁止 mock 调整 expectation 顺序。
7. 禁止把 status 缺失当 idle。
8. 禁止固定 `sleep 200` 作为状态同步手段。
9. 禁止跨 scenario 复用工作区和配置。
10. 每条测试必须有 deadline。
11. 每条测试必须经过 `finally` 清理。
12. cleanup 失败必须计为测试失败。
13. 每个 durable 功能必须有 restart 测试。
14. 每个 session runtime 功能必须有双 session 隔离测试。
15. 每个子进程功能必须有 abort 和泄漏测试。
16. NDJSON 必须解析结构，不得只查 substring。
17. 测试失败必须自动输出完整诊断包。
18. 新功能没有 E2E manifest 条目不得报批。

---

# 十、最终验收标准

OpenCode E2E 重建完成的判据不是“新增了多少条用例”，而是：

* 所有真实 E2E 都经过 `opencode serve`；
* 所有伪 E2E 已正确降级命名；
* 所有工具均有真实执行 happy path；
* PTY、subagent、continue、review、fallback、compaction 均有真实宿主链路；
* 所有持久化状态均经过 restart 验证；
* 所有 session runtime 均经过多 session 隔离验证；
* 所有长期资源均经过 cleanup 验证；
* mock provider 对意外调用严格失败；
* 无固定 sleep 驱动的关键断言；
* 单套完整测试连续运行 50 次无 flaky；
* 打乱测试顺序后结果不变；
* 单独运行任一测试与全量运行结果一致；
* 强制制造失败时，诊断信息足以直接定位到 HTTP、event、message、NDJSON、provider 或进程层。

当前文档对测试分层的定义本身是正确的：E2E 应是 harness 加 mock LLM 的宿主行为验证；但目前实现没有严格贯彻“断言公共输入、输出、事件或状态事实”的原则。重建后应把这条原则变成自动门禁，而不是依赖开发者自觉。
