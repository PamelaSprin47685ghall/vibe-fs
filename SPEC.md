# Fallback Continuation 保姆式重写指南

## 一、结论先行

当前问题不是 `U+200B` 选得不好，而是整个实现把：

> “向宿主发送一次具有身份、可取消、可重放、可结算的 continuation”

偷换成了：

> “向 session 塞入一个看不见的普通用户文本，然后猜后续事件是不是它造成的”。

现有 OpenCode 适配器明确用零宽字符构造普通 `session.prompt`：

```fsharp
let private zwsChar = "​"

createPromptBodyWithModelAndNonce
    agent
    (Some modelStr)
    zwsChar
    (Some continuationID)
```

但 OpenCode 的插件 prompt 会创建正常 user message，重新经过 `chat.message`、消息持久化、模型执行、assistant 事件和 `session.idle`。它并不是 synthetic continuation，也没有官方 continuation provenance。

更严重的是，文档一方面要求消费端不得嗅探零宽字符，必须依赖持久化 continuation 投影；另一方面仍把 `SendContinue` 定义成“注入 U+200B”。这说明文档和实现都处于两代架构混杂状态。

正确方向只有一个：

> **Continuation 是领域对象；宿主 prompt 只是它的一次副作用。必须把 ContinuationId 持久绑定到 OpenCode 实际创建的 User MessageId，再通过 Assistant.parentID 判断后续事件归属。**

---

# 二、立即冻结的错误做法

重写期间先立以下红线。

## 2.1 禁止继续修补零宽方案

不得再增加：

* 更多不可见 Unicode 字符；
* `"continue"`、中文“继续”、英文恢复提示的文本识别；
* 根据消息时间戳推断是不是 fallback；
* 根据“最近一次 injected model”推断事件归属；
* 空 `continuationId` 自动匹配当前 lease；
* 任意 `session.busy` 都算 continuation 已启动；
* 任意 `session.idle` 都算 continuation 已结束。

零宽字符只能作为普通用户可能输入的合法文本，不能承担协议身份。

## 2.2 禁止把 `synthetic` 当作解决方案

OpenCode 自己可以创建 synthetic compaction continuation，但插件通过公开 prompt API 创建的是普通 user message。插件不能稳定地请求 OpenCode 创建官方 synthetic message；`metadata.compaction_continue` 也是 OpenCode 内部标志，而非稳定插件契约。

因此本项目不应伪装成 OpenCode synthetic message。正确策略是：

* 承认这是一个插件控制消息；
* 给它明确语义；
* 用项目自己的 metadata 携带临时 correlation；
* 最终将宿主 MessageId 持久化为正式 receipt。

## 2.3 禁止无归属地调用 `AbortRun(sessionID)`

Session 级 abort 会终止当前 session 正在运行的任务。它无法天然区分：

* 旧 fallback continuation；
* 新用户消息；
* nudge；
* compaction；
* 新一代 fallback。

旧 continuation 失效后直接执行 `AbortRun(sessionID)`，可能把更新的用户轮次一并杀掉。

以后只能经过：

```text
TryAbortOwnedContinuation
```

并同时验证：

1. 当前 active continuation 仍是该 ContinuationId；
2. generation 与 cancelGeneration 未变化；
3. 当前宿主 user message id 等于该 continuation 的 HostUserMessageId；
4. 当前 owner 仍是 Fallback；
5. 没有新 human turn；
6. 没有 compaction 或 nudge 接管。

任一条件不满足，只将旧 continuation 标记为 `Superseded`，不得 abort session。

---

# 三、当前实现为什么必然产生 Bug

## 3.1 ContinuationId 被发送了，但后续事件找不到它

当前 prompt part 中放的是：

```json
{
  "metadata": {
    "nonce": "<continuation-id>"
  }
}
```

`ChatHooks` 能从 part metadata 读取 nonce，并避免把该消息当成人类新轮次。

但 OpenCode `EventTranslator.ExtractContinuationIdentity` 查找的是事件顶层：

```text
properties.continuationId
properties.continuationID
rawEvent.continuationId
rawEvent.continuationID
```

它并不从已创建 user message 的 part metadata 或 assistant `parentID` 建立正式关联。

结果是：

```text
发送时有 continuationId
        ↓
chat.message 局部能看到 nonce
        ↓
session.busy / assistant / idle 事件通常没有 continuationId
        ↓
桥接器只能靠“当前正好有 lease”猜测归属
```

这不是 correlation，只是时间窗口猜测。

## 3.2 空身份被当成匹配

当前 lease 匹配逻辑允许：

```text
事件没有 continuationId
+
当前 generation/cancelGeneration 一致
=
认为事件属于当前 continuation
```

这会造成：

* unrelated busy 清除 `AwaitingStart`；
* nudge 的 busy 把 fallback lease 标成 Running；
* compaction idle 结算 fallback；
* 新用户轮次的 assistant/idle 误结算旧 fallback；
* 延迟到达的旧事件作用于新 lease；
* 重复 idle 导致重复 settlement。

必须彻底删除“空 ID 匹配当前活动项”的逻辑。

正确原则是：

> **没有 HostUserMessageId、Assistant.parentID、HostRunId 或明确 receipt 的事件，只能作为 session 级观察，不能推进 continuation 状态。**

## 3.3 文本嗅探与项目自己的架构原则冲突

当前 `isSyntheticText` 不仅识别 U+200B，还识别若干英文提示文本。这意味着文案、语言、空格或模型输出稍有变化，协议就失效；真实用户恰好输入相同文本时又可能被吞掉。

而文档已经明确要求消费端不得嗅探消息文本，注入事实应来自事件投影。

因此应整体删除：

```text
zwsChar
isSyntheticText
通过英文提示识别系统消息
通过 Contains/StartsWith 识别 continuation
通过 InjectedAt 时间窗口识别消息来源
```

`InjectedModel` 可以保留为历史路由信息，但不得继续承担身份判断。

## 3.4 内存状态可能早于持久化事实

当前 continuation 建立过程中，可以看到类似顺序：

```text
设置 owner
设置 awaiting gate
设置 active generation
设置 PendingLease
然后 append continuation_requested
```

这违反项目文档自己的“先盘后内存”纪律。

一旦 append 失败：

* 内存中已经存在 lease；
* NDJSON 中没有请求事实；
* gate 可能永久不释放；
* 重启后状态消失；
* 当前进程和重放状态不一致。

正确顺序必须是：

```text
Decide
→ 持久化一个原子 commit envelope
→ append 成功
→ 更新内存 projection
→ 唤醒 effect supervisor
```

## 3.5 “派发事实”的事件含义不严格

必须区分以下四件完全不同的事实：

1. 决定要派发；
2. 某个 worker 获得派发权；
3. 已调用宿主 API；
4. 宿主已经创建 user message。

现在的 `dispatch_started`、`dispatched` 和内存 CAS 交叉排列，可能出现：

* 事件写入成功，但 CAS 失败；
* API 成功，但 receipt 没有持久化；
* `dispatched` 已写入，但宿主消息并不存在；
* 重启后不知道该重试还是等待。

文档其实已经提出 durable outbox、稳定 ContinuationId 和幂等宿主操作的方向，应直接落实，而不是继续给 lease 打补丁。

## 3.6 同一编排逻辑出现重复实现

仓库同时存在：

```text
FallbackEventBridge.fs
FallbackEventBridgeCoordinator.fs
```

二者包含高度重复的 session queue、事件处理和 continuation 执行逻辑，但实际引用关系并不对称。

重写时只保留一个入口。不要在两个文件中同步修 Bug。

---

# 四、重写后的领域模型

## 4.1 Continuation 不再等于 prompt text

新建：

```text
Kernel/Fallback/Continuation.fs
```

建议类型如下：

```fsharp
type ContinuationMode =
    | ResumeInterruptedTurn
    | RecoverToolCallText of prompt: string

type ContinuationStatus =
    | Committed
    | DispatchClaimed
    | HostMessageAccepted
    | Running
    | Settled
    | Cancelled
    | Failed
    | Superseded

type HostDispatchIdentity =
    | PendingHostIdentity
    | UserMessageIdentity of userMessageId: string
    | RunIdentity of runId: string
    | OpaqueIdentity of receiptId: string

type ContinuationRequest =
    { ContinuationId: string
      ContinuationOrdinal: int
      Attempt: int
      SessionId: string
      HumanTurnId: string
      SourceHumanMessageId: string option
      ContextGeneration: int
      CancelGeneration: int
      Model: FallbackModel
      Agent: string
      Mode: ContinuationMode }

type ContinuationState =
    { Request: ContinuationRequest
      Status: ContinuationStatus
      HostIdentity: HostDispatchIdentity
      HostAssistantMessageId: string option
      Failure: string option }
```

必须保证：

* model 和 agent 在决策时冻结；
* executor 不得在派发时重新读取“最新 session model/agent”；
* prompt 内容只是 request 的一个字段；
* ContinuationId 不从文本解析；
* Host MessageId 是 continuation 状态的一部分。

## 4.2 宿主适配器返回 receipt

把现有接口：

```fsharp
abstract SendContinue:
    sessionID: string *
    model: FallbackModel *
    continuationID: string ->
        JS.Promise<unit>
```

改成：

```fsharp
type HostDispatchReceipt =
    | UserMessageAccepted of userMessageId: string
    | RunAccepted of runId: string
    | OpaqueAccepted of receiptId: string

type IContinuationHost =
    abstract Dispatch:
        request: ContinuationRequest ->
            JS.Promise<HostDispatchReceipt>

    abstract TryAbortOwned:
        request: ContinuationRequest *
        receipt: HostDispatchReceipt ->
            JS.Promise<bool>

    abstract Reconcile:
        request: ContinuationRequest ->
            JS.Promise<HostDispatchReceipt option>
```

宿主差异由 receipt 联合类型吸收：

| 宿主       | 首选身份                               |
| -------- | ---------------------------------- |
| OpenCode | 实际 User MessageId                  |
| Mux      | workspace/run/continuation receipt |
| OMP      | message id 或 run id                |
| 无身份宿主    | 插件自产 opaque receipt，但不能靠空 ID 匹配    |

Kernel 不得知道 OpenCode 的 `parentID`、Mux workspace 或 OMP event shape。

---

# 五、事件模型怎么改

## 5.1 保留六阶段思想，但重新定义事实

建议事件如下：

| 事件                              | 精确定义                                    |
| ------------------------------- | --------------------------------------- |
| `continuation_requested`        | 领域决策已提交，完整 request 已持久化                 |
| `continuation_dispatch_claimed` | 某个 supervisor 获得本次 attempt 的执行权         |
| `continuation_host_accepted`    | 宿主确认创建消息或 run，带 HostDispatchReceipt     |
| `continuation_run_started`      | 已观察到属于该 receipt 的 assistant/run 活动      |
| `continuation_settled`          | 该 continuation 对应的执行正常终结                |
| `continuation_failed`           | 派发或执行确定失败                               |
| `continuation_cancelled`        | 在仍拥有该执行时被明确取消                           |
| `continuation_superseded`       | 已被新 human turn、新 generation 或新 owner 取代 |

现有 `continuation_dispatch_started` 可以兼容读取，但新写入统一使用 `continuation_dispatch_claimed`。

## 5.2 一个 NDJSON commit envelope 同时包含领域事实与 effect

不要先写 domain event，再靠内存通知副作用。

使用单行原子 envelope：

```json
{
  "kind": "continuation_decision_committed",
  "session": "ses-...",
  "payload": {
    "events": [
      {
        "kind": "continuation_requested",
        "continuationId": "cont-...",
        "attempt": 1
      }
    ],
    "outbox": [
      {
        "effectId": "continuation:cont-...:attempt:1",
        "effectKind": "dispatch_continuation",
        "continuationId": "cont-...",
        "attempt": 1
      }
    ]
  }
}
```

这样一行 append 成功，领域事实和 effect 意图同时成立；append 失败则什么都不成立。

不需要为此引入数据库事务。NDJSON 的一行就是最小提交屏障。

## 5.3 Projection 成为唯一运行时真相

新增或重写：

```text
Kernel/Fallback/ContinuationProjection.fs
```

投影至少包含：

```fsharp
type ContinuationProjection =
    { ActiveBySession: Map<string, ContinuationState>
      ByContinuationId: Map<string, ContinuationState>
      ProcessedEffectIds: Set<string> }
```

所有 gate 都从 projection 派生：

```text
AwaitingStart =
    status = HostMessageAccepted

FallbackActive =
    status in
      Committed
      DispatchClaimed
      HostMessageAccepted
      Running
```

删除 `MainContinuationAwaitingStart` 这种可以和 lease 状态互相矛盾的独立布尔 SSOT。

---

# 六、OpenCode 适配器的正确实现

## 6.1 发送显式控制消息，而不是空白消息

OpenCode 没有公开的插件 continuation API，所以仍需创建一个普通 user turn。但应明确表达它是什么。

建议内容：

```text
<wanxiangshu-control kind="fallback-continuation">
Resume the interrupted assistant task from the latest valid state.
Do not treat this control turn as a new user requirement.
Do not repeat completed work.
</wanxiangshu-control>
```

它可以在 UI 展示层按 metadata 隐藏，但不能靠“文本看不见”伪装成不存在。

文本只负责给模型语义，**不负责 correlation**。

## 6.2 使用命名空间 metadata

把：

```json
{
  "metadata": {
    "nonce": "..."
  }
}
```

改成：

```json
{
  "metadata": {
    "wanxiangshu": {
      "kind": "fallback_continuation",
      "schema": 2,
      "continuationId": "cont-...",
      "continuationOrdinal": 3,
      "attempt": 1,
      "humanTurnId": "turn-...",
      "contextGeneration": 7,
      "cancelGeneration": 2
    }
  }
}
```

注意：

* metadata 是 OpenCode 适配层的 transport marker；
* NDJSON continuation projection 才是 SSOT；
* 不允许其他模块直接靠 metadata 判断领域状态；
* metadata 主要用于接收 messageID 和崩溃后 reconciliation。

OpenCode 研究表明，插件创建 prompt 时没有官方稳定 provenance 字段，但 message/part metadata 可用于项目自建映射；实际 assistant 还提供 `parentID`，可以绑定到触发它的 user message。

## 6.3 在 `chat.message` 中记录真实 User MessageId

OpenCode 创建 user message 时，`chat.message` hook 能看到：

* sessionID；
* messageID；
* model；
* agent；
* parts。

流程应改为：

```text
Effect Supervisor 调用 session.prompt
    ↓
OpenCode 创建 user message
    ↓
chat.message hook 读取 namespaced metadata
    ↓
取得 OpenCode 实际 messageID
    ↓
append continuation_host_accepted
    {
      continuationId,
      userMessageId
    }
    ↓
projection 将 HostIdentity 设为 UserMessageIdentity
```

`chat.message` 不得直接修改内存 lease。它只提交一个领域 command：

```fsharp
HostUserMessageObserved
    (continuationId, userMessageId)
```

随后由 session actor 串行决定并持久化事件。

## 6.4 用 assistant.parentID 做严格匹配

收到 assistant `message.updated` 时：

```fsharp
match active.HostIdentity with
| UserMessageIdentity userMessageId
    when assistant.ParentId = Some userMessageId ->
        // 属于这个 continuation
| _ ->
        // 与 continuation 无关
```

这是 OpenCode 主路径最重要的改动。

判断优先级：

```text
1. assistant.parentID == HostUserMessageId
2. 明确 HostRunId 相等
3. 明确 namespaced continuation metadata 相等
4. 其他一律不匹配
```

严禁：

```text
continuationId 为空
→ generation 恰好一样
→ 当作匹配
```

## 6.5 busy 和 idle 只作为补充证据

`session.busy` 和 `session.idle` 没有足够的来源身份。

因此：

### Busy

只有在已经持久化 `HostUserMessageId` 后，busy 才可以表示：

```text
宿主可能开始执行
```

它不能单独把状态推进为 Running。

更可靠的 Running 证据是：

* assistant message 的 `parentID` 命中；
* 明确 run id 命中；
* 对应 user message 出现 assistant part。

### Idle

Idle 只能触发一次 reconciliation：

```text
读取当前 continuation
→ 查询对应 user message 是否存在
→ 查询是否存在 parentID 命中的 assistant
→ 判断 assistant finish/error
→ 决定 Settled/Failed/仍等待
```

不得执行：

```text
当前有 pending lease + 收到 idle = settled
```

---

# 七、每个文件具体怎么改

## 7.1 `Hosts/OpenCode/Fallback/ActionExecutor.fs`

删除：

```fsharp
let private zwsChar = "​"
```

删除：

```fsharp
resolveModelAndAgent
```

原因是 model、agent 已经在 `ContinuationRequest` 中冻结，不应派发时重新读取 live session。

将：

```fsharp
sendContinueImpl runtime client sessionID model continuationID
```

改成：

```fsharp
dispatchContinuationImpl
    (client: obj)
    (request: ContinuationRequest)
    : JS.Promise<HostDispatchReceipt>
```

该函数只做：

1. 根据 request 构造显式控制 prompt；
2. 写入 namespaced metadata；
3. 使用 request.Model；
4. 使用 request.Agent；
5. 调用 OpenCode prompt；
6. 返回能直接取得的 receipt；
7. 若返回值暂时没有 messageID，则等待 `chat.message` command 完成映射，而不是返回伪造成功。

## 7.2 `Runtime/OpencodeSessionEventCodec.fs`

删除泛化的：

```fsharp
createPromptBodyWithModelAndNonce
```

新增：

```fsharp
createContinuationPromptBody
    (request: ContinuationRequest)
    : obj
```

普通 nudge、subsession、fallback 不应继续共用模糊的 `nonce` 协议。

分别使用：

```text
createFallbackContinuationPromptBody
createNudgePromptBody
createSubsessionPromptBody
```

共同结构可以放在内部 helper，但 provenance 必须是明确联合类型，不能靠 nonce 猜用途。

## 7.3 `Hosts/OpenCode/ChatHooks.fs`

将当前：

```text
nonce 是否命中活动 lease
```

升级为：

```text
解析 wanxiangshu metadata
→ 得到 ControlMessageProvenance
→ 提交相应领域 Command
```

例如：

```fsharp
type ControlMessageProvenance =
    | FallbackContinuation of ContinuationIdentity
    | NudgeMessage of NudgeIdentity
    | SubsessionDispatch of SubsessionIdentity
```

ChatHooks 只负责宿主解码和 command 投递，不负责直接推进状态。

## 7.4 `Hosts/OpenCode/Fallback/EventTranslator.fs`

删除：

```text
isSyntheticText
U+200B 判断
英文提示文本判断
```

将接口从：

```fsharp
ExtractContinuationIdentity: obj -> (string * int) option
```

改为更中立的：

```fsharp
type HostEventIdentity =
    { SessionId: string
      UserMessageId: string option
      AssistantMessageId: string option
      AssistantParentId: string option
      HostRunId: string option
      DurableEventSeq: int64 option
      Provenance: ControlMessageProvenance option }

abstract ExtractIdentity:
    rawEvent: obj -> HostEventIdentity
```

OpenCode translator 只提取宿主事实，不替 Kernel 猜测 continuation。

## 7.5 `Runtime/Fallback/FallbackBridgeLease.fs`

整体删除“空 continuation ID 兜底匹配”。

旧函数：

```fsharp
checkContinuationMatches
```

应替换为纯函数：

```fsharp
matchEvidence
    (continuation: ContinuationState)
    (identity: HostEventIdentity)
    : EvidenceMatch
```

返回：

```fsharp
type EvidenceMatch =
    | ExactMatch
    | SessionOnly
    | DifferentExecution
    | StaleExecution
```

只有 `ExactMatch` 能推进状态。

`SessionOnly` 只能触发 reconciliation。

## 7.6 `Runtime/Fallback/FallbackBridgeContinuation.fs`

该文件不再直接同时承担：

* 领域决策；
* lease 内存修改；
* event append；
* API 调用；
* API 补偿；
* owner/gate 清理。

拆成：

```text
Kernel/Fallback/ContinuationDecision.fs
Runtime/Fallback/ContinuationCommandProcessor.fs
Runtime/Fallback/ContinuationSupervisor.fs
Runtime/Fallback/ContinuationReconcile.fs
```

### CommandProcessor

固定顺序：

```text
读取 projection
→ 纯 decide
→ append commit envelope
→ 更新 projection cache
→ 通知 supervisor
```

### Supervisor

固定顺序：

```text
读取 durable outbox
→ 验证 effect 仍有效
→ claim effect
→ 调宿主
→ 记录 receipt 或 failure
```

副作用完成后不得直接递归调用 processor；只能 enqueue 新 command。

## 7.7 `Runtime/Fallback/FallbackEventBridge.fs`

保留为宿主事件入口，但只做：

```text
decode event
→ deduplicate
→ enqueue command
→ 返回消费结果
```

不得在 queue 外直接执行 continuation intent。

当前“queue 内决定，queue 外执行 intent”留下了新 human turn 插入的竞态窗口。重写后，effect supervisor 自己消费 durable outbox，不依赖调用栈携带 `intentOpt`。

## 7.8 `FallbackEventBridgeCoordinator.fs`

删除。

若其中存在唯一逻辑，先迁回唯一的：

```text
ContinuationCommandProcessor
```

最终仓库不得保留两份 fallback coordinator。

## 7.9 `Runtime/Fallback/RuntimeStore.fs`

删除或降级以下独立可变字段的 SSOT 地位：

```text
PendingLease
MainContinuationAwaitingStart
ActiveContinuationGen
ActiveContinuationCancelGen
InjectedAt
```

RuntimeStore 只允许缓存 projection。缓存丢失时必须能从 NDJSON 完整恢复。

## 7.10 `Kernel/Fallback/FallbackInjectionFold.fs`

将旧 `fallback_continue_injected` 设为：

```text
legacy read-only event
```

新写入停止产生它。

新的 SSOT 是：

```text
continuation_requested
continuation_host_accepted
continuation_run_started
continuation_settled / failed / cancelled / superseded
```

旧 `InjectedModel` 可由新 continuation 事件派生。

---

# 八、状态机必须写死的合法转移

| 当前状态                                | 允许转移                | 触发                          |
| ----------------------------------- | ------------------- | --------------------------- |
| Committed                           | DispatchClaimed     | supervisor 成功 claim         |
| Committed                           | Cancelled           | 新 human turn 或显式 abort，尚未派发 |
| DispatchClaimed                     | HostMessageAccepted | 宿主 receipt 已持久化             |
| DispatchClaimed                     | Failed              | API 明确失败                    |
| DispatchClaimed                     | Superseded          | generation 已变化              |
| HostMessageAccepted                 | Running             | parentID/runID 精确命中         |
| HostMessageAccepted                 | Cancelled           | 仍确认拥有该宿主执行并成功 abort         |
| HostMessageAccepted                 | Superseded          | 已失去 owner，不再干预宿主            |
| Running                             | Settled             | 对应 assistant/run 正常终结       |
| Running                             | Failed              | 对应 assistant/run 明确失败       |
| Running                             | Cancelled           | 对应执行被确认取消                   |
| Running                             | Superseded          | 新轮次已接管，忽略迟到事件               |
| Settled/Failed/Cancelled/Superseded | —                   | 终态                          |

特别规定：

```text
SessionBusy
SessionIdle
无 parentID 的 assistant
无 receipt 的 error
```

都不是合法状态转移触发器，只是 reconciliation hint。

---

# 九、取消语义重写

## 9.1 新用户消息到达

必须在同一个 session actor 中执行：

```text
1. append human_turn_started
2. 增加 cancelGeneration
3. 将尚未派发的 continuation 标记 Cancelled
4. 将已派发但不再拥有的 continuation 标记 Superseded
5. 清除 owner 派生状态
6. 不盲目调用 session abort
```

## 9.2 用户 Esc

OpenCode 的稳定信号应结合：

* `MessageAbortedError`；
* interrupted part metadata；
* 当前 user message/assistant parent 关系；
* 本项目 cancelGeneration。

OpenCode 没有替本项目提供 continuation generation，因此自建 generation 仍然必要。

## 9.3 旧 continuation 迟到

例如：

```text
cont-A 派发
→ 用户发新消息
→ cont-A assistant 事件迟到
```

处理结果必须是：

```text
parentID 命中 cont-A
但 cont-A 已 Superseded
→ 记录 stale evidence
→ 不改变当前 owner
→ 不结算 cont-B
→ 不 abort session
```

---

# 十、重启与兼容迁移

## 10.1 新增 schema version

所有新 continuation payload 写入：

```json
{
  "schema": 2
}
```

Fold 同时支持：

* legacy `fallback_continue_injected`；
* legacy continuation 六阶段事件；
* v2 continuation receipt 事件。

## 10.2 重启时不要重新发送零宽字符

发现 legacy pending lease 时：

```text
1. 扫描宿主消息；
2. 尝试寻找 metadata.nonce 或旧 continuation marker；
3. 找到 user message id：
   append continuation_host_accepted_v2；
4. 找不到：
   标记 LegacyUnknown/Superseded；
5. 不自动再次发送 U+200B；
6. 等下一次真实错误重新创建 v2 continuation。
```

宁可放弃一次无法证明归属的旧 continuation，也不能重复向未知 session 注入消息。

## 10.3 Metadata 仅用于 reconciliation

重启扫描可以按：

```text
metadata.wanxiangshu.continuationId
```

寻找宿主消息。

一旦找到，立即持久化：

```text
ContinuationId → HostUserMessageId
```

之后所有运行时关联只使用 MessageId，不再反复扫描 metadata。

---

# 十一、测试必须先于删除旧代码

## 11.1 Kernel 单元测试

必须覆盖：

1. 空 event identity 不能命中 active continuation；
2. unrelated busy 不改变状态；
3. unrelated idle 不结算；
4. assistant.parentID 精确命中才能进入 Running；
5. 旧 generation 事件不能作用于新 continuation；
6. duplicate host receipt 幂等；
7. duplicate settled 幂等；
8. 新 human turn 取消未派发 continuation；
9. 新 human turn supersede 已派发 continuation；
10. append 失败时 projection 不变化；
11. model/agent 从 request 到 dispatch 完全不变；
12. 用户真实输入 U+200B 被当作普通用户文本；
13. 用户输入 `"Continue if you have next steps."` 不被当成控制消息；
14. compaction idle 不结算 fallback；
15. nudge assistant 不结算 fallback。

## 11.2 Actor 与 outbox 测试

模拟以下崩溃点：

```text
A. requested commit 后、dispatch 前崩溃
B. API 调用成功后、receipt append 前崩溃
C. receipt append 后、assistant 事件前崩溃
D. assistant 开始后、settled append 前崩溃
```

预期：

| 崩溃点 | 恢复行为                        |
| --- | --------------------------- |
| A   | supervisor 重放 outbox        |
| B   | reconcile 宿主消息，不盲目重复创建      |
| C   | 等待或扫描 parentID 对应 assistant |
| D   | 根据宿主消息终态补写 settlement       |

## 11.3 事件乱序测试

必须主动注入：

```text
idle → busy
assistant completed → assistant delta
duplicate message.updated
旧 error 晚于新 human turn
compaction idle 与 fallback idle 交错
nudge busy 与 fallback dispatch 交错
```

测试不得依赖 sleep 固定毫秒数；以队列 drain、事件 barrier 或确定性 projection 收敛为准。

## 11.4 OpenCode v1.17.13 E2E

至少完成：

1. 429 后切换模型，只创建一次 continuation；
2. fallback 控制消息拥有真实 MessageId；
3. assistant.parentID 指向该 MessageId；
4. session 历史中不存在空白 user bubble；
5. 控制消息不会产生 `human_turn_started`；
6. continuation 完成后不会触发普通 nudge；
7. Esc 后旧 continuation 不再复活；
8. Esc 后的新用户消息不会被旧 compensation abort；
9. compaction 与 fallback 相邻发生时互不结算；
10. 插件重启后能恢复 pending continuation；
11. API 成功但事件迟到时不会重复 prompt；
12. 同一事件重复投递不会重复 settlement。

---

# 十二、推荐的 PR 拆分顺序

## PR 1：止血与特征测试

只做：

* 增加 `fallback.legacyZeroWidthContinue` 开关；
* 默认关闭自动零宽续命；
* 添加现状 characterization tests；
* 添加日志，记录所有空 identity 匹配；
* 不大改状态机。

目的：先停止制造新隐患。

## PR 2：Continuation v2 领域模型

新增：

```text
ContinuationRequest
ContinuationState
HostDispatchReceipt
ContinuationDecision
ContinuationProjection
v2 event codec
```

同时支持 legacy replay，但暂不切换 OpenCode 发送路径。

## PR 3：OpenCode MessageId receipt

完成：

* namespaced metadata；
* `chat.message` 记录 user message id；
* assistant.parentID correlation；
* EventTranslator 删除文本嗅探；
* 显式控制 prompt。

这是最关键的功能 PR。

## PR 4：Durable outbox 与 supervisor

完成：

* commit envelope；
* append 后更新 projection；
* supervisor 重放；
* reconciliation；
* 取消与 supersede；
* 删除 queue 外 `intentOpt` 执行。

## PR 5：删除旧架构和修正文档

删除：

```text
zwsChar
isSyntheticText
空 ID 自动匹配
InjectedAt 身份判断
旧 PendingLease SSOT
重复 Coordinator
无条件 AbortRun
```

更新：

```text
05-event-sourcing.md
12-fallback.md
14-host-opencode.md
15-host-mux.md
16-host-omp.md
18-glossary-and-ssot-map.md
```

每个 PR 必须独立编译、独立通过测试，不允许一次性推翻全部代码后再统一修编译。

---

# 十三、文档应如何改写

## `12-fallback.md`

删除：

```text
SendContinue 注入零宽 U+200B 文本
```

替换为：

```text
SendContinue 提交 ContinuationRequest。
Effect Supervisor 将请求派发到宿主。
宿主适配器创建显式控制消息，并返回 HostDispatchReceipt。
OpenCode 以 User MessageId 和 Assistant.parentID 建立归属。
```

## `05-event-sourcing.md`

明确：

* continuation request 与 outbox 同一提交屏障；
* host accepted 必须带 receipt；
* settled 必须有精确归属；
* legacy event 只读兼容；
* append 前不得修改内存 projection。

现有文档已经列出 continuation 六阶段事件，应在此基础上补齐宿主 receipt，而不是另造旁路状态。

## `14-host-opencode.md`

明确写出限制：

1. 插件 prompt 是普通 user message；
2. 插件不能依赖官方 synthetic provenance；
3. `session.idle` 没有来源；
4. `session.busy` 没有 continuation identity；
5. 主关联键是 User MessageId；
6. assistant 通过 parentID 关联；
7. metadata 是适配层 transport marker；
8. NDJSON projection 是 SSOT。

## `18-glossary-and-ssot-map.md`

将：

```text
FallbackInjectionFold / fallback_continue_injected
```

改为：

```text
ContinuationProjection / continuation_* v2 events
```

并将以下内容列入明确废弃项：

```text
零宽文本嗅探
英文提示嗅探
时间戳近似关联
空 continuation identity 匹配
无归属 session abort
```

---

# 十四、完成标准

执行以下检查时必须全部通过。

```bash
rg -n 'zwsChar|\\u200[bB]|isSyntheticText' src
```

结果应为空。

```bash
rg -n 'There are still incomplete todos|Continue if you have next steps' \
  src/Hosts src/Runtime
```

不得出现在来源识别逻辑中；显式控制 prompt 常量除外。

```bash
rg -n 'AbortRun.*sessionID' src/Runtime/Fallback src/Hosts
```

所有调用都必须经过 owner、generation 和 HostUserMessageId 验证。

代码审查还必须确认：

* [ ] 不存在空 identity 自动命中；
* [ ] 不存在 append 前写 PendingLease；
* [ ] 不存在两个 fallback coordinator；
* [ ] 每次宿主派发都有 durable ContinuationId；
* [ ] 每个已接受的 OpenCode prompt 都有 HostUserMessageId；
* [ ] assistant 通过 parentID 归属；
* [ ] generic busy/idle 不直接推进状态；
* [ ] model 和 agent 不在派发时重新解析；
* [ ] 新用户轮次不会被旧 continuation abort；
* [ ] 重启后不重新注入零宽字符；
* [ ] legacy 事件只读兼容；
* [ ] 文档中不再把 U+200B 描述成正式协议。

最终应能用一句话解释整个机制：

> Fallback 先把 continuation 决策和副作用意图持久化，再由 supervisor 向宿主派发；宿主返回真实消息身份，后续事件只有在身份精确匹配时才能推进或结算该 continuation。

说不清这句话，或者实现中任何一步仍然依赖“最近发生”“当前正好有 lease”“文本看起来像系统提示”，就说明重写还没有完成。

---
对。**那我上一版要求删除零宽字符、改用 XML 控制消息，是错误的。**它把两个不同问题混在了一起：

* **给 LLM 看什么**：实测 `U+200B` 最干净，不污染上下文，应当保留。
* **系统如何识别这次 continuation**：不能靠 `U+200B` 文本判断，必须靠 metadata、MessageId、parentID 和持久化状态。

当前代码注释本身也说明了零宽字符的设计目标：它能触发非空 prompt，又不会在模型历史中留下可见的 “continue” 痕迹。 文档也把“不完整转录时 ContinueNormally 使用零宽字符”视为正式行为。

## 修正后的总原则

> **保留 zero-width payload，删除 zero-width sniffing。**

正确架构是：

```text
模型侧载荷：U+200B
协议身份：ContinuationId
宿主身份：UserMessageId / RunId
后续归属：Assistant.parentID
持久化真相：continuation_* 事件
```

零宽字符只做一件事：

```text
让 LLM 在没有新增语义指令的情况下继续生成
```

绝不能再让它承担这些职责：

```text
判断消息是不是 fallback
判断是不是新用户轮次
判断 busy/idle 属于哪个 continuation
判断旧 continuation 是否已经完成
判断是否应该取消 session
```

---

# 对上一版指南的精确修正

## 一、不要删除 `zwsChar`，而要规范化它

当前源码使用肉眼不可见的字面字符：

```fsharp
let private zwsChar = "​"
```

应改成显式转义，避免代码审查、复制和格式化工具破坏：

```fsharp
/// Model-facing payload for a semantic-free continuation.
///
/// This value is deliberately minimal. It is not an identity marker and
/// must never be inspected to correlate host events with a continuation.
let private continuationPayload = "\u200B"
```

名字也要从 `zwsChar` 改成 `continuationPayload`，强调它是**模型输入策略**，不是协议字段。

建议集中定义：

```text
Kernel 不定义 U+200B
Runtime 通用层不定义 U+200B
各 Host adapter 决定如何触发 continuation
```

例如：

```fsharp
module Wanxiangshu.Hosts.Opencode.Fallback.ContinuationPrompt

[<Literal>]
let Payload = "\u200B"
```

因为零宽字符是否有效属于宿主和模型适配经验，不属于 Fallback Kernel 公理。

---

## 二、删除的应当是 `isSyntheticText`

当前 OpenCode translator 明确这样判断：

```fsharp
let private isSyntheticText (text: string) : bool =
    let t = text.Trim()

    t = "\u200b"
    || t.Contains("There are still incomplete todos")
    || ...
```

这才是架构错误。

应整体删除：

```fsharp
isSyntheticText
```

以及所有类似逻辑：

```fsharp
text = "\u200B"
text.Contains(...)
text.StartsWith(...)
最近几秒注入过 fallback
消息文本为空或不可见
```

因为真实用户完全可能粘贴零宽字符；其他插件也可能使用它；宿主也可能规范化文本。**payload 相同不等于 provenance 相同。**

---

## 三、OpenCode 仍然发送零宽 prompt，但附带正式身份

保留当前核心发送行为：

```fsharp
createPromptBodyWithModelAndNonce
    agent
    (Some modelStr)
    continuationPayload
    (Some continuationID)
```

当前实现已经把 `continuationID` 作为 nonce 传入，这个方向是对的。

但需要把模糊的 `nonce` 升级为明确 metadata。推荐消息结构：

```json
{
  "parts": [
    {
      "type": "text",
      "text": "\u200B",
      "metadata": {
        "wanxiangshu": {
          "kind": "fallback_continuation",
          "schema": 2,
          "continuationId": "cont-...",
          "continuationOrdinal": 3,
          "attempt": 1,
          "contextGeneration": 8,
          "cancelGeneration": 2
        }
      }
    }
  ]
}
```

这里必须明确：

* `text` 是给模型的；
* `metadata` 是给适配器的；
* `ContinuationId` 是领域身份；
* 宿主创建后的 `MessageId` 是运行身份。

不能用 `text` 反向推导 metadata。

---

## 四、必须补上的链路仍然不变

我的上一版里最关键的修复仍然成立：

```text
ContinuationId
    ↓
发送 U+200B + metadata
    ↓
OpenCode 创建真实 User Message
    ↓
chat.message 捕获 UserMessageId
    ↓
持久化 ContinuationId → UserMessageId
    ↓
assistant.parentID == UserMessageId
    ↓
确认 assistant 属于该 continuation
```

即：

```fsharp
type ContinuationHostIdentity =
    | AwaitingUserMessage
    | UserMessageAccepted of userMessageId: string
```

观察 assistant 时只允许精确匹配：

```fsharp
let belongsToContinuation
    (state: ContinuationState)
    (assistantParentId: string option)
    =
    match state.HostIdentity, assistantParentId with
    | UserMessageAccepted expected, Some actual ->
        expected = actual
    | _ ->
        false
```

零宽字符完全不参与这里的判断。

---

# `ActionExecutor.fs` 应当怎样改

不是删除 zero-width，而是把职责收窄。

## 当前问题

当前函数同时做了：

1. 重新获取 live agent；
2. 重新读取最新消息；
3. 重新决定 agent；
4. 构造 U+200B；
5. 附加 continuation nonce；
6. 调用 prompt；
7. 丢弃返回结果。

其中第 4 步是正确的；真正危险的是身份和 receipt 没有闭环。

## 推荐改写

```fsharp
let private continuationPayload = "\u200B"

let private dispatchContinuationImpl
    (client: obj)
    (request: ContinuationRequest)
    : JS.Promise<HostDispatchReceipt> =
    promise {
        let modelStr = formatFallbackModel request.Model

        let body =
            createFallbackContinuationPromptBody
                request.Agent
                modelStr
                continuationPayload
                request.Identity

        let arg =
            box
                {| path = box {| id = request.SessionId |}
                   body = body |}

        let! response = invokeClient client "prompt" arg

        match tryDecodeCreatedUserMessageId response with
        | Some messageId ->
            return UserMessageAccepted messageId

        | None ->
            // chat.message hook will resolve the metadata to a real MessageId.
            return AwaitingMessageObservation
    }
```

`ContinuationRequest` 中冻结：

```fsharp
type ContinuationRequest =
    { ContinuationId: string
      ContinuationOrdinal: int
      Attempt: int
      SessionId: string
      Model: FallbackModel
      Agent: string
      ContextGeneration: int
      CancelGeneration: int
      Mode: ContinuationMode }
```

派发时不要再读取“最新 agent/model”覆盖 request。否则错误发生时决定切换到模型 B，真正派发时可能又读取到模型 C。

---

# `ChatHooks.fs` 应当怎样改

不要问：

```text
这条消息文本是不是 U+200B？
```

要问：

```text
这条消息是否携带合法的 fallback continuation metadata？
```

伪代码：

```fsharp
match tryDecodeWanxiangshuProvenance parts with
| Some(FallbackContinuation identity) ->
    commandQueue.Enqueue(
        HostUserMessageObserved
            { ContinuationId = identity.ContinuationId
              UserMessageId = messageId
              SessionId = sessionId }
    )

    // 这是插件控制消息，不是 human turn。
    false

| None ->
    // 正常的人类消息分类。
    classifyAsHumanMessage input
```

关键是：

```text
metadata 命中 → 插件消息
没有 metadata → 普通用户消息
```

而不是：

```text
文本是 U+200B → 插件消息
```

---

# EventTranslator 怎样改

## 应删除

```fsharp
isSyntheticText
```

## 应保留提取

```fsharp
AssistantMessageId
AssistantParentId
UserMessageId
HostRunId
SessionId
```

## 应返回宿主事实，而不是猜测

```fsharp
type HostEventIdentity =
    { SessionId: string
      MessageId: string option
      ParentMessageId: string option
      RunId: string option
      Provenance: ControlMessageProvenance option }
```

然后由 continuation projection 做匹配：

```fsharp
match state.HostUserMessageId, event.ParentMessageId with
| Some expected, Some actual when expected = actual ->
    ExactContinuationMatch
| _ ->
    NotContinuationEvidence
```

---

# 文档应当怎样修正

文档不应写：

> 零宽字符是 fallback continuation 的识别标记。

也不应写：

> 应改用 XML 控制消息。

应写成：

> `ContinueNormally` 的 OpenCode/OMP 模型侧载荷采用单个 `U+200B ZERO WIDTH SPACE`。这是经实测选择的最小非空 continuation prompt，用于避免 “continue”、XML 或自然语言控制文本污染模型上下文。
>
> `U+200B` 不是 provenance、correlation ID 或安全边界。消息来源必须由 `ContinuationId` metadata、宿主 `UserMessageId` 以及 assistant `parentID` 确认。任何消费端不得通过检查消息文本是否等于 `U+200B` 判断 continuation 身份。

恢复工具调用文本时仍使用真实恢复 prompt：

```text
ContinueNormally    → U+200B
RecoverWithPrompt   → 明确的恢复提示
```

两者不能混为一谈。

---

# 应增加的零宽专项测试

## Payload 保真

```fsharp
testCase "continuation payload is exactly U+200B" <| fun _ ->
    Expect.equal continuationPayload "\u200B" "payload"
    Expect.equal continuationPayload.Length 1 "UTF-16 code unit count"
    Expect.equal (int continuationPayload[0]) 0x200B "code point"
```

还应断言 UTF-8：

```text
E2 80 8B
```

## 不允许文本承担身份

必须覆盖：

```text
用户手工输入 U+200B
→ human turn
```

```text
插件发送 U+200B 但没有 continuation metadata
→ 不得命中任何 continuation
```

```text
插件发送普通可见文本但带合法 continuation metadata
→ provenance 可以识别
```

第三个测试很重要，它能证明身份来自 metadata，而不是 payload。

## 多 continuation 隔离

```text
cont-A: U+200B, messageId=msg-A
cont-B: U+200B, messageId=msg-B
assistant.parentID=msg-A
```

只能推进 cont-A。

这直接证明两个完全相同的零宽 prompt 也不会串线。

## 文本规范化

测试宿主可能返回：

```text
U+200B
空字符串
缺失 text part
合并后的 part
```

只要 `MessageId/parentID` 能关联，状态机都应正常工作；不能因为宿主读取时丢失 U+200B 而失去归属。

---

# 最终重写口径

因此，上一版中以下两项撤回：

```text
❌ 删除 zwsChar
❌ 用 XML 显式控制消息替代
```

改为：

```text
✅ 保留单个 U+200B 作为 SendContinue 的模型侧 payload
✅ 源码中使用 "\u200B" 转义，不使用不可见字面量
✅ 删除 isSyntheticText 和所有文本嗅探
✅ continuationId 通过 metadata 传递
✅ 捕获真实 UserMessageId
✅ assistant 通过 parentID 精确归属
✅ busy/idle 不能仅凭当前 lease 推进状态
✅ U+200B 不进入 Kernel，不成为 SSOT
```

一句话定稿：

> **零宽字符是优秀的 LLM continuation prompt，但它是糟糕的分布式系统 correlation key。保留前一种用途，彻底禁止后一种用途。**
