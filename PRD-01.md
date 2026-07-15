# 系统约束：Flow 约束 + 领域不变量 + 跨问题统一对象

## 一、Flow 管线不可破坏的约束

只要遵守这些约束，Flow 可以比 Actor 更"明显正确"。

### 约束 1：提交感知状态 fold 只能有一个 collector

不能对冷流 collect 两次，否则可能重复执行整个流程或重复副作用。生产 collector
必须是 `scanCommit`：candidate state/events/effects 先生成并 append，成功后才发布
accumulator、`SessionState` 与 effects。普通 `scan`/`runningFold` 仅可用于重放或
只读 UI 派生，不能作为提交管线。

### 约束 2：领域输入在 `scanCommit` 前不能 unordered merge

多 Host 来源必须先进入有序 per-session mailbox（参见 PRD-00 核心管线），分配 sequence 后再
进入 `scanCommit`。

### 约束 3：`step` 中不能执行慢 Host I/O

否则整个提交流被背压，Abort 也被堵住。慢 I/O 交给并发 Effect 区（`flatMapMerge`）。

### 约束 4：副作用前先持久化 intent

`ContinuationPlanned` append 成功后才允许 dispatch；更具体地，必须先以一次事件写入
原子地 claim lease（`DispatchClaimed`），随后才可进行物理 dispatch。append 失败时
session 进入 poisoned，禁止合成 prompt。

### 约束 5：取消必须既是控制信号，也是领域事件

`CancellationToken` 负责停止本地工作；`UserAbortObserved` 和 generation 负责业务正确性。

### 约束 6：不得对领域事实使用 `conflate`、`debounce` 或 `drop`

领域事实包括：human message、Abort、continuation dispatch、tool result、event log append、lease、settlement。

### 约束 7：`flatMapLatest` 只用于可安全废弃的查询

不能用它表示"Host 一定没收到 prompt"。已开始且必须记录物理结果的 dispatch 不适用。

### 约束 8：所有并发 effect 返回结果都必须回到唯一提交 fold

`EffectRunner` 不修改 SessionState，只把结果写回输入 Channel，由 `scanCommit` 统一积分。

### 约束 9：执行计划只能在 `step` 内产生

每个输入只读取一个 `SessionState` snapshot；所有 policy 在该 `step` 中同步仲裁并
生成 candidate Events/Effects。`stateFlow |> map plan` 只允许作为 UI/诊断预览，不得
取得 lease、发送 prompt 或成为第二事实源。

### 约束 10：workspace 只有一个事实 journal

一个 workspace 使用一个 journal；每个 path 由一个进程内 `JournalWriter` 实例负责，
并以 interprocess lock 保护跨进程 append。不得按 session 或 projection 分裂 journal。

## 二、领域不变量

以下规则必须写进架构设计和测试，否则修完一个还会从另一个入口复发。

### 不变量 1：Esc 是最高优先级、粘性的用户意图

用户按 Esc 后：
* 当前人类轮次立即进入 `Cancelled`。
* 已排队但尚未发送的 fallback、nudge、review continuation 全部失效。
* 已经发送但尚未返回的系统 continuation，其迟到事件不得重新激活会话。
* 只有 Esc 之后一条**明确确认是真人发出的新消息**，才能开启新轮次。

任何零宽消息、nudge、compaction、title、fallback continuation 都不能解除 Cancelled。

### 不变量 2：每条消息必须有来源

消息来源至少要分成：Human、FallbackContinuation、TodoNudge、ReviewNudge、ContextBudgetNudge、Compaction、TitleGeneration、Subagent、UnknownLegacy。

不能再只用 `role=user` 判断"这是用户消息"。

### 不变量 3：每个 session 同一时刻只能有一个 continuation owner

owner 必须使用以下唯一 canonical enum；文档和事件不得另造同义 owner：
`UserAbort | FallbackRecovery | Compaction | ContextBudget | ReviewLoop | TodoNudge |
RunnerReminder | None`。

优先级固定为：
1. UserAbort
2. FallbackRecovery
3. Compaction
4. ContextBudget
5. ReviewLoop
6. TodoNudge
7. RunnerReminder
8. None

| canonical owner | 可创建的 synthetic work |
| :--- | :--- |
| `UserAbort` | 无；只取消并使其他 lease 失效 |
| `FallbackRecovery` | fallback continuation |
| `Compaction` | compaction autocontinue |
| `ContextBudget` | context-budget nudge |
| `ReviewLoop` | review continuation |
| `TodoNudge` | todo 提示 |
| `RunnerReminder` | runner reminder |
| `None` | 无 synthetic work |

高优先级 owner 未释放前，低优先级 owner 不得发送 prompt。

### 不变量 4：所有异步行为必须绑定代次

至少需要：`humanTurnId`、`sessionGeneration`、`cancelGeneration`、`continuationId`、`contextGeneration`。

迟到回调只要 generation 不匹配，就只能记录，不得改变当前状态。

### 不变量 5：model/agent 属于轮次，不属于 session 全局

模型选择必须绑定到当前真人消息或当前 fallback attempt。不能用"session 最近注入过的模型"覆盖未来所有轮次。

### 不变量 6：控制字段在唯一入口软合规、硬门禁

`warn`、`warn_tdd`、`warn_reuse` 必须在 schema 中强调声明，并由唯一执行入口提取到
ControlEnvelope、从下游业务参数中删除、下游只拿净化后新对象。schema 缺失或长度
不足属于软合规：工具不得因此硬拒绝；工具返回时追加一次 stern compliance event
（违反时在参数规范化后恰好一次），批评模型。硬门禁只保留 malformed business args、security/permission denial、parse
failure，以及净化后仍泄漏控制字段。`amend` 字段已在代码中移除，不作为控制字段。

### 不变量 7：context budget 不允许静默失效

即使拿不到 provider limit 或精确 tokenizer，也必须进入明确的降级模式，而不是让 `MaxInputTokens=0` 后什么都不做。

### 不变量 8：review loop 活跃时，task 不能为空

只要系统判定 review loop 是 Active，生成任何 review continuation 时都必须携带完整原始任务。缺失任务属于状态损坏，应当阻止 nudge。

### 不变量 9：compaction/fallback 结束不等于人类轮次自然结束

只有真正的人类轮次完成事件，才有资格触发 todo/review nudge。

### 不变量 10：默认运行不得污染 stdout

正常 capability 缺失、降级、重试、缓存未命中都不应直接打印 `DEBUG:`。

### 不变量 11：BUGS 行为必须可观测且不改变硬门禁

investigator agent 必须接收 CAPS；单工具成功的回合只生成一个“并行调用工具”提示。
conversation start 在上下文尚未充分观测时不得生成 emergency todo prompt；已 Settled 的
fallback lease 不得再生成 fallback nudge。`warn`/`warn_tdd`/`warn_reuse` 的缺失和
todo 长度不足按不变量 6 的软合规规则处理，在工具返回时记录一次批评事件，不拒绝
本次合法工具调用。

## 三、跨问题统一对象

以下对象正式引入，作为 Flow 管线中传递的强类型值。

### 3.0 SessionEpoch 与 CausalityContext

`SessionEpoch` 是会话身份层，至少包含 `sessionID`、`sessionGeneration` 与
lifecycle。session 创建或真正开始新会话时递增 `sessionGeneration`；compaction 不得
改变它。

`CausalityContext` 是事件因果层，包含 `sessionEpoch`、可选 `humanTurnID`、
`cancelGeneration`、`contextGeneration`、可选 `continuationID`、`requestID` 及
canonical owner。session-start 或无 owner 的观察只有在
`ObservationOptions(allowUnowned = true, allowSessionStart = true)` 下才合法，且
必须显式记录 `owner = None` 与缺失的 turn；不得伪造身份。普通 observation 缺少所需
身份必须进入 parse/validation hard gate。

compaction 递增 `contextGeneration`，不递增 `sessionGeneration`；新真人轮次递增
相应 turn/cancel 代次。任何异步回调都必须携带 CausalityContext，过期结果只能写入
`StaleEffectIgnored`。

### 3.1 TurnIdentity

包含：session ID、human turn ID、session generation、cancel generation、user message ID。

用于阻止旧 fallback、旧 nudge 和旧 model observation 污染新轮次。所有进入
`scanCommit` 的 Input 和所有离开 `flatMapMerge` 的 EffectResult 都必须携带
TurnIdentity；仅 session-start/unowned observation 按 3.0 的显式 options 使用
`SessionEpoch + CausalityContext`，不伪造 TurnIdentity。

### 3.2 ContinuationLease

包含：continuation ID、canonical owner、turn identity、route、status、issuedAt、
invalidation reason、claim sequence。状态必须遵循：
`Pending → DispatchClaimed → Dispatching → Dispatched → Running → Settled`，并允许
`Invalidated` 作为不可执行的终态。
若在 claim 后无法判定 Host 是否收到请求，状态进入 `DispatchUnknown`，并只能通过
`Reconciling`（带同一 continuation ID 的查询/确认事件）收敛到 `Dispatched`、`Settled`
或 `Invalidated`。`DispatchUnknown` 不得直接重发。

任何 synthetic prompt 发送前都必须在 session mailbox 中原子 claim lease。只有
`DispatchClaimed` 已成功 append，才允许进入物理 dispatch；claim 与 owner/turn/
generation 验证不能拆成两次写。`flatMapMerge` 执行 effect 前仍须重新读取最新
`cancelGeneration`，与 lease 创建时的代次比较。只要发现当前代次已大于租约创建时的
代次，此租约必须被宣布为 `Invalidated`，直接拦截调用。

真正调用 `session.prompt` 前必须重新进入 session mailbox，验证：
* lifecycle 仍为 Active；
* cancelGeneration 未变化；
* humanTurnID 仍相同；
* continuation owner 仍为 `FallbackRecovery`；
* lease 已由同一事务从 `Pending` 原子变为 `DispatchClaimed`；
* 没有 compaction owner；
* 没有新真人消息；
* 没有 TaskComplete；
* 没有 force-stop。

claim 事务成功后，物理调用前必须再次确认 lease 仍为 `DispatchClaimed`，然后追加
`Dispatching`；Host 返回确认后追加 `Dispatched`/`Running`，最终追加 `Settled`。
验证失败则把 lease 标记为 `Invalidated`，绝不调用 Host。每个 dispatch/result 事件
都必须带 continuation ID、claim sequence 与 CausalityContext；result 只可作用于仍
匹配的 lease，迟到或重复 result 记录为 stale，绝不能复活旧 lease 或覆盖新轮次。

### 3.3 ControlEnvelope

包含：warn、warn_tdd、warn_reuse、已移除的 `amend`（仅作历史兼容说明）、tool capability、validation result、audit fields。

它与净化后的业务参数彻底分离。在执行网关（PRD-03）中由 before hook 提取，在自有 execute wrapper 中再次验证，在 after hook 中用于审计。

### 3.4 ContextBudgetObservation

包含：effective tokens、observation source、**count-based cache semantics**（revision/
counter、已应用事件数）、model limit、limit source、context generation、confidence、
degraded reason。缓存不得以内容 hash 或 fingerprint 作为 key/正确性依据；canonical
byte/prefix equality 只可作为可观察断言。

防止把一个裸整数误认为绝对真实值。

### 3.5 ReviewPromptContext

包含：original task、loop ID、round、feedback、todos、prompt origin。

所有 review prompt 统一由它生成。

## 四、SessionGateDemand 统一优先级

Compaction、fallback、review、todo、runner 之间不应分别在自己的 hook 中判断"现在能不能发"。

统一 gate 优先级：

1. `UserAbort`
2. TaskComplete gate (not an owner)
3. `FallbackRecovery`
4. `Compaction`
5. `ContextBudget`
6. `ReviewLoop`
7. `TodoNudge`
8. `RunnerReminder`

每个 session 同时只能有一个能发送 synthetic prompt 的 owner。owner 必须通过 settle 事件释放，不能只看 session 当前是否 idle。

在 Flow 管线中，gate 判定作为 `step` 内的纯 policy 函数实现，输入是当前
`SessionState` 的原子 snapshot，输出是 `NudgeBlockReason` 枚举。`stateFlow |> map
plan` 只能呈现同一 policy 的 UI/诊断预览，不执行计划：

* Allowed
* UserCancelled
* FallbackActive
* CompactionActive
* SyntheticTurn
* PendingDelivery
* RunnerOwnsTurn
* DuplicateAnchor
* UnknownTerminalOrigin

纯 `deriveAction` 继续只根据 snapshot 判定，不反向依赖 Shell 可变 runtime。Shell/host adapter 读取 fallback observation 后转换为 `NudgeBlockReason`，构造 snapshot，传入纯函数。

## 五、不变量到 Flow 管线的映射

| 不变量 | Flow 管线位置 |
| :--- | :--- |
| 1. Esc 粘性取消 | `scanCommit` 第一分支 Cancelled/TaskComplete → 终止；lease 在 effect 边界校验 |
| 2. 消息来源 | Input 携带 provenance，在 normalize 阶段分配 |
| 3. 单一 owner | `step` 内以一个 snapshot 做纯 policy 仲裁 |
| 4. 代次绑定 | TurnIdentity 在 Input 和 EffectResult 中贯穿 |
| 5. model 属于轮次 | HumanTurnRoute 从 `chat.message` Input 派生，进入 projection |
| 6. 控制字段唯一入口 | effect 执行前净化 + clean assertion |
| 7. budget 不静默失效 | ContextBudgetObservation 在 `step` 内计算，degraded 不返回 None |
| 8. review task 非空 | ReviewProjection 从 Events fold，snapshot 派生 activeTask |
| 9. 终止来源分类 | terminalOrigin 在 Step 中，由 `scanCommit` 输出携带 |
| 10. stdout 纯净 | 所有日志作为 effect 在 `scanCommit` 外执行，不污染管线 |
