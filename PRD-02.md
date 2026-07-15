# 问题 1：Esc、Abort 与 fallback auto-continue

## 一、Flow 管线位置

本问题映射到管线上的三个位置：

1. **`scan` 第一分支**：`Cancelled` 和 `TaskComplete` 作为最高优先级终止态，在任何 gate 计算之前拦截。
2. **`flatMapMerge` effect 执行边界**：lease 校验在 Host prompt 调用前的"最后一毫米"执行。
3. **Input normalize 阶段**：per-session serial mailbox 消除 OpenCode event hook 并发竞态。

## 二、当前根因

### 2.1 零宽字符身份判断不可靠

当前 Opencode fallback 使用零宽空格作为 `SendContinue` prompt。UI 看不见，但 host 登记为 `role=user` 消息。系统再通过注入时间判断是否为 fallback 发出。

问题在于：
* 零宽字符只是内容，不是身份。
* 时间戳可能缺失、为 0、单位不同、事件顺序倒置。
* fallback 注入和用户 Esc 是两个异步事件，可能交错。
* 状态机的 `NewUserMessage` 无条件把 lifecycle 恢复为 Active。
* 一条迟到的零宽 user message 可能被误判为真人消息，解除 Cancelled。
* 注入记录是"最近注入过什么"，没有精确绑定到 message ID、continuation ID 和 human turn。
* 请求、发送、观察、失败、取消被混成一个事实。

### 2.2 needFallbackContinue 漏掉 Cancelled（增补稿 I）

当前 `FallbackSubagentGate.needFallbackContinue` 只把 `TaskComplete` 视为终止状态：
* `TaskComplete` 立即返回 `false`；
* `Cancelled` 则继续检查 `EventHandlingActive`、`AwaitingBusy`、`SubsessionPending` 和 `BusyCount`；
* 因此只要任意临时门禁仍为活动状态，已取消的 session 仍被判定为需要 fallback continuation。

`terminalObservation` 和 `isSubagentSettledFromObservation` 也只特别处理了 `TaskComplete`，没有将 `Cancelled` 纳入完整终止语义。

### 2.3 Abort 空输出路径（增补稿 II）

`FallbackEventBridge.handleEvent` 在处理 `session.idle` 时：
1. 若 lifecycle 已为 `Cancelled`，只生成普通 `SessionIdle`；
2. 否则拉取消息历史，调用 `tryGetLastAssistantAbortInfo`；
3. 若识别到 Abort，生成带 abort 信息的 `SessionError`；
4. 只有未识别到 Abort 且最后输出无内容无工具时，才创建 `EmptyOutputError`。

因此"所有 Abort 空输出都直接被误判成 EmptyOutput"并非准确描述。但以下情况仍会丢失 Abort 语义：
* Host 只发送 `session.interrupted`，但不写 assistant metadata；
* stream 在生成 assistant message 之前被中止；
* Host 使用新的错误名称；
* Abort 信息位于 event status、cause 或嵌套 error 中；
* Abort event 先到，idle event 后到，后者无法再恢复原因。

### 2.4 状态机已返回 DoNothing 但旧 SendContinue 晚到（增补稿 II）

典型竞态：
1. 错误事件使状态机决定 `SendContinue`；
2. Action 已离开纯状态机，进入 Promise 或 Host 调用队列；
3. 用户此时按 Esc；
4. lifecycle 被改成 `Cancelled`；
5. 先前排队的 `SendContinue` 仍然调用 Host prompt。

单纯再次读取 `state.Lifecycle` 仍不够，因为：
* 读取后和真正发送之间仍有极短竞态；
* 读取的 state 可能是旧快照；
* 新人类轮次可能已经开始，lifecycle 又恢复为 Active；
* 旧 Action 可能因此误投到新轮次。

## 三、OpenCode v1.17.13 源码定案（增补稿 III）

### 定案一：TUI Esc 是双击取消

目标版本中：
* 第一次按 Esc 只增加 UI interrupt 计数，提示再次按键；
* 五秒内第二次 Esc 才调用 `sdk.client.session.abort`；
* CLI interrupt 则直接调用 abort，没有双击确认。

因此任何 Esc 问题必须先区分用户只按了一次还是完成了两次。如果只按了一次，OpenCode 根本没有发生硬取消，fallback 继续运行在 Host 语义上正常。但只要 Abort API 已被调用，fallback、nudge 和其他 synthetic continuation 就必须停止。

### 定案二：session.idle 绝不等于"自然完成"

官方 `session.idle` 只有 `sessionID`，没有 completion reason、abort reason、current run ID、origin、generation、是否来自 compaction、是否来自错误、是否即将 auto-continue。

它可能由正常回答结束、用户 Abort、provider error、retry 结束、compaction、不可恢复 overflow、session 没有 runner 时调用 abort 产生。

> 禁止仅凭 `session.idle` 触发 todo/review nudge。

### 定案三：Abort 事件不完整也不保证唯一

Abort 通常形成 `MessageAbortedError`，但并不保证一定有 `session.error`。有些取消路径只会更新 assistant message error、设置 completed time、发布 message.updated、发布一个或多个 idle。

* `processor.halt` 可能设置一次 idle；
* `Runner.cancel` 又可能设置一次 idle；
* cleanup 和 interrupted finalizer 可能多次更新同一 assistant message。

不得依赖"必须先收到 session.error，再收到一次 idle"。

### 定案四：plugin event hook 是并发 fire-and-forget

普通 trigger hook（`tool.execute.before`、`tool.execute.after`、`chat.message`、`chat.params`）由 OpenCode 按插件加载顺序串行等待。

但通用 `event` hook 是 fire-and-forget：不等待 Promise，同一插件的前后两个事件 handler 可以并发，同一 session 处理完成顺序不受保证，`message.updated` 本身可能重复发布。

当前依靠多个 bool 标志和异步 handler 自行读写状态的方式天然存在竞态。

### 定案五：OpenCode 没有 cancellation generation

官方 Runner 内部有 run handle，但没有通过插件协议暴露 run ID、cancel generation、continuation ID、当前 human turn generation。万象术不能仅靠 OpenCode session lifecycle 判断迟到事件属于旧轮次还是新轮次，必须自行建立 generation 和 lease。

### 定案六：before Hook 原地修改有效，替换无效

`tool.execute.before` 收到的 `output.args` 与随后传给真实工具的局部 `args` 通常是同一个对象引用。因此 `delete output.args.warn_tdd` 有效；`output.args = newObject` 无法改变真实 execute 使用的旧局部引用。

### 定案七：before Hook 不能覆盖全部内部执行路径

经过 before/after 的有：registry built-in 工具、custom plugin 工具、MCP server 工具、MCP resource 工具、Task/subagent 工具。

不经过或不完全经过的有：StructuredOutput、title agent、compaction agent、部分内部直接调用的 read。after hook 在工具失败或 Abort 时不会执行。

## 四、正确状态模型

### 4.1 fallback continuation 完整生命周期

* Requested
* DispatchClaimed
* Dispatching
* Dispatched
* Running
* ObservedByHost
* BusyObserved
* Settled
* Cancelled
* Failed

每个 continuation 带有：session ID、human turn ID、continuation ID、创建时的 cancel generation、选中的 model/agent、provenance = FallbackContinuation，以及唯一的 `ContinuationLease.owner`。`ContinuationLease` 是 owner 的唯一权威来源；不得另设一个可变的 current-owner 字段与 lease 竞争。

Dispatch 的阶段必须区分为：

* `Requested`：只有计划，没有取得发送资格；
* `DispatchClaimed`：已在 session mailbox 中原子取得 lease，但尚未开始 host 调用；
* `Dispatching`：host 调用已经开始；
* `Dispatched`/`Running`：host 已接受或已开始执行；
* `DispatchUnknown`：调用结果未知（超时、连接断开或进程中止），必须走 `Reconciling`，不能猜测为未发送或已 settle；
* `Settled`、`Cancelled`、`Failed`：终局状态。

`DispatchClaimed` 仍属于“尚未开始物理 dispatch”，可以被 Abort 原子撤销；进入 `Dispatching` 后则属于“已经开始 dispatch”，Abort 只能记录取消并等待/协调 host 结果，不能声称撤回调用。

### 4.2 CancelTombstone

AbortProjection 应综合以下证据：`session.error.name = MessageAbortedError`、assistant message `error.name = MessageAbortedError`、tool part `metadata.interrupted = true`、`session.interrupted`、`stream-abort`、本插件主动调用 abort 的记录、Abort 后出现的 idle、local force-stop 操作。

任何明确 Abort 证据出现，都建立取消墓碑：

```text
CancelTombstone
- sessionID
- humanTurnId
- cancelGeneration
- observedEventID
- observedAt
- reason
```

`session.idle` 本身不能创建取消墓碑，但在已有取消证据后可以帮助确认 Host 正在收尾。

所有 Host translator 应把以下语义归一化为同一种领域事实：用户取消、客户端取消、stream abort、session interrupted、AbortError、MessageAbortedError、SDK cancellation token、主机明确的 stop-by-user。

归一化结果必须包含：`DomainError = MessageAborted` 或明确的 ClientCancellation、`IsRetryable = false`、cancel generation、原始 Host reason code、当前 human turn ID。不能仅依赖错误名字字符串。

## 五、修复方案

### 5.1 P0 止血：统一 Cancelled gate

所有由 lifecycle 派生的 gate 判定，第一分支统一为：

```text
Cancelled    → 终止（false）
TaskComplete → 终止（false）
其他状态     → 继续判定
```

适用范围至少包括：
* `needFallbackContinue`
* `terminalObservation`
* `isSubagentSettledFromObservation`
* recovery wait
* nudge block
* pending prompt dispatch
* fallback action executor

任何 BusyCount、AwaitingBusy、EventHandlingActive、Consumed、Phase 都不得覆盖 Cancelled。

统一不变量：

> Lifecycle 只要是 Cancelled 或 TaskComplete，任何 BusyCount、Consumed、Phase 和 ActiveGates 都无权重新要求 continuation。

### 5.2 P0 止血：Abort 原子清理门禁（CancelEpisode）

Abort 被状态机正式识别为 `Cancelled` 后，在同一 session 串行操作中完成：

* `SetAwaitingBusy false`
* `ClearSubsessionPending`
* `SetEventHandlingActive false`，或确保当前 handler 的 finally 不会重新产生继续需求
* `SetNudgeActive false`
* `SetBusyCount 0`
* `ClearConsumed`
* 清除尚未发送的 continuation request
* 清除旧 injected model 的活跃作用域
* 唤醒等待 gate 状态变化的 listener，使其重新计算并结束等待

需要一个统一的 `CancelEpisode` 操作，不能让不同 host 分别手动清理若干字段。

### 5.3 引入 per-session 串行事件邮箱

由于 event hook 并发执行，所有下列操作必须进入同一个 per-session serial mailbox：event 解析、fallback state transition、lifecycle 更新、BusyCount 更新、injected continuation 记录、nudge snapshot 更新、compaction projection 更新、event log append、action 派生。

状态流：

```text
Host event
→ 快速解码 sessionID/eventID
→ 投递 per-session mailbox
→ 去重
→ 更新 projection
→ 派生 action
→ 记录 action intent
→ mailbox 外执行副作用
→ 副作用结果重新投递 mailbox
```

不得继续依赖多个异步 handler 直接修改同一 runtime map。

### 5.4 所有 SendContinue 改为带 Lease 的动作

状态机输出不应再只是 `SendContinue(model)`，应当包含 `ContinuationLease`（参见 PRD-01）。

真正调用 `session.prompt` 前必须重新进入 session mailbox，验证 lease 全部条件。验证失败则把 lease 标记为 Invalidated，绝不调用 Host。

被取消的 Action 不得产生 EmptyOutput、RetrySame 或下一次 fallback。记录 `continuation_invalidated` 而不是静默发送或重新分类为错误。

Abort 必须按 dispatch 阶段处理，不能把所有旧 Action 统称为“未发送”：

| Abort 时状态 | 必须动作 | 迟到事件 |
|---|---|---|
| `Requested` 或 `DispatchClaimed`（尚未开始物理调用） | 立即撤销 lease、写 `continuation_invalidated`，保证不调用 Host | 按旧 lease/generation 丢弃 |
| `Dispatching`、`Dispatched` 或 `Running`（已开始物理调用） | 标记 `CancelRequested`；若 Host 支持则调用 abort；不得伪造撤回或再次 dispatch | 进入 `DispatchUnknown`/`Reconciling`，只允许补齐该 lease 的事实 |
| `DispatchUnknown` | 保持取消屏障并启动 reconciliation；不得从不完整结果推导 settle | 仅接受匹配 lease、generation 的 reconciliation 事件 |

任何不匹配当前 lease、human turn 或 `cancelGeneration` 的 busy/idle/error/message 事件都是 stale event：必须幂等记录/丢弃，不能恢复 lifecycle、增加 BusyCount、释放新轮次的 owner，不能触发 fallback、nudge 或 synthetic prompt。已开始的 dispatch 即使最终报告成功，也只能结算其原 lease，不能为已取消的人类轮次产生后续 continuation。

### 5.5 注意 session.prompt 可能在 Busy 状态下排队

官方 prompt 路径可以在 Runner 尚未完全释放时等待或排队。user message 可能在真正开始下一次 run 之前已经创建。

应禁止在 event handler 原始调用栈中直接 prompt。先将 continuation 标记为待派发，等待本地 session mailbox 完成当前事件批次，重新查询 Host session status 和最后消息，再次校验 lease，然后调用 prompt。

固定 sleep 只能作为兼容性缓冲，不能承担正确性。

### 5.6 Esc 的处理顺序

Esc 到达时必须在同一 session 串行队列中原子完成：
1. 增加 `cancelGeneration`。
2. 把当前 human turn 标记为 Cancelled。
3. 标记所有旧 generation 的 continuation 为 invalidated。
4. 清除 AwaitingBusy、pending auto-continue、pending nudge（`CancelEpisode`）。
5. 能调用 host abort API 时，主动中止在途 prompt。
6. 记录持久事件 `user_abort_observed`。
7. 后续所有迟到事件先核对 generation，再决定是否处理。

不要仅仅把 fallback state 的 Lifecycle 设为 Cancelled。必须让所有异步回调都能看见"自己已经过期"。

Abort 前先在 mailbox 中读取 continuation lease 的 dispatch 阶段：尚未开始物理 dispatch 的请求必须取消且不得调用 Host；已经开始的 dispatch 必须进入 `CancelRequested`/`DispatchUnknown`（按结果可见性再 `Reconciling`），不得误报为“没有新 dispatch”。旧 lease 的所有迟到事件均按 stale event 处理，并且不得影响新 human turn。

### 5.7 真人新消息如何恢复

`NewUserMessage` 不应再是无参数事件，而应携带来源和消息身份。

只有同时满足以下条件才能开启新轮次：
* 来源为 Human；
* message ID 尚未处理；
* 消息不是 synthetic；
* 创建时间或 host 顺序位于最后一次 Esc 之后；
* 不是旧 continuation 的迟到映射；
* 不是 compaction/title/nudge。

然后创建新 human turn ID，重置 fallback attempt，清除旧 injected model，清除旧 cancellation barrier，保存真人消息携带的 model、agent、variant。

### 5.8 fallback prompt provenance

OpenCode PromptInput 不允许插件直接设置 `synthetic=true`，也没有正式 correlation ID。

短期双重识别：
1. 本地 pending lease；
2. prompt front matter 中的 opaque continuation ID；
3. `chat.message` Hook 捕获新 messageID；
4. 建立 `messageID → FallbackContinuation(continuationID)` 映射。

文本 front matter 只负责把 pending request 与官方 messageID 对上，真正 SSOT 仍是本地 projection。

### 5.9 零宽字符怎么处理

最佳方案是彻底停止用零宽字符承担身份识别职责。优先级：
1. host 支持 metadata：直接附加 provenance、continuation ID。
2. host 返回 message ID：保存请求 ID 和返回 message ID 的映射。
3. host 只能发送文本：使用内部可解析控制封套，在发送给 LLM 的消息变换阶段剥离。
4. 实在无法关联时，时间戳只能作为兼容旧版本的辅助信息，不能作为权威判断。

### 5.10 事件日志调整

把单一的 `fallback_continue_injected` 拆成更准确的事实：
* `fallback_continue_requested`
* `fallback_continue_dispatch_started`
* `fallback_continue_dispatched`
* `fallback_continue_observed`
* `fallback_continue_cancelled`
* `fallback_continue_failed`
* `fallback_episode_settled`

旧事件可以继续读取，但新逻辑不能把"写下 injected"直接等价为"host 已经接收并产生消息"。

### 5.11 事件幂等

使用 OpenCode event ID、messageID、partID、callID、local continuationID 建立去重键。`message.updated` 不能以"每出现一次就代表一次新完成"处理。

MessageID 可以辅助排序，但不能代替 generation。MessageID 是单调 ULID，但真人消息、compaction request、synthetic continuation、插件 nudge、fallback prompt 都会创建新 user ID。

## 六、验收标准

> Esc 之后，在真人再次输入前，不得再出现任何由旧 human turn 产生的 prompt。

补充验收：
* 单次 Esc 不错误标记为 Abort；
* 双次 Esc 后旧 continuation 调用数为零；
* `session.error` 缺失时仍能识别 Abort；
* 重复 idle 不产生重复状态转移；
* 旧 generation 的 message.updated 不恢复 session；
* 已派生但未发送的 action 被取消；
* 已经排队的 prompt 可被识别和失效；
* `Cancelled + EventHandlingActive=true` 必须返回不继续；
* `Cancelled + AwaitingBusy=true` 必须返回不继续；
* `Cancelled + BusyCount>0` 必须返回不继续；
* `Cancelled + Phase=Retrying` 必须返回不继续；
* Cancelled session 必须被视为 settled；
* `DispatchClaimed` 后但未开始 Host 调用时 Abort 不调用 Host；
* 已开始的 dispatch 在 Abort 后不被误报为未发送，且只可结算旧 lease；
* `DispatchUnknown` 必须 reconciliation，不能直接 settle 或重派；
* stale event 不恢复 lifecycle、不增加 BusyCount、不释放新 lease owner、不产生 nudge；
* 旧 continuation 的迟到 busy/idle 不能重新增加 BusyCount；
* 当前 event handler 的 finally 清理标志后不能触发新的 fallback send；
* 新真人消息必须创建新 turn/generation 后才允许恢复 Active；
* Abort 空输出不会产生 EmptyOutputError；
* 缺少 assistant abort metadata 时 translator 仍可识别取消；
* 旧 generation 的 busy/idle 不改变新轮次；
* 已生成但过期的 SendContinue 不会调用 Host；
* Cancelled 后所有旧 continuation lease 失效；
* 同一 Abort 被多个 Host event 重复报告时只取消一次。

## 七、必测竞态场景

* fallback 决策之前 Esc；
* Requested 之后、真正调用 prompt 之前 Esc；
* prompt 调用中 Esc；
* host 已接收但 message.updated 尚未到达；
* message.updated 已到达但 busy 尚未到达；
* busy 到达后、idle 之前；
* idle 到达时；
* session.error 与 Esc 同时到达；
* 进程重启、旧 injected 事件重放后；
* 消息时间戳缺失、为 0、时钟倒退；
* duplicate message.updated；
* fallback prompt 成功但回调晚于新真人消息；
* 状态机返回 `SendContinue` 后、Host prompt 调用前按 Esc；
* Host prompt Promise 已建立但未实际发送时按 Esc；
* Abort event 没有 assistant message；
* Abort metadata 晚于 idle event；
* idle 先被判定为空输出，随后收到 abort；
* 新真人消息已经开启新 turn，但旧 Action 仍在队列；
* lifecycle 恢复 Active，但 continuation generation 已过期；
* 单次 Esc（不调用 abort）后 fallback 继续运行（Host 语义正常）；
* 双次 Esc 后所有旧 continuation 调用数为零。
