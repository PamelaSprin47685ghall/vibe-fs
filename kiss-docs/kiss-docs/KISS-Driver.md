# Session Driver 与 Prompt 协议

本卷闭合 Structured Flow 与 OpenCode 宿主之间的**本进程内**执行语义。持久化跨进程模型见 **KISS-04**（Lifetime Snapshot Isolation）。

[NORMATIVE] 全文。与 KISS-04 冲突的「全局单 Writer / Session 锁 / Previous 链」以 KISS-04 为准并已删除。

---

## 一、单 Driver 范围 [NORMATIVE]

```
每个 Runtime 内，每个 Session 恰好 0 或 1 个 Driver。
键：(RuntimeId, SessionId)
```

|允许|不允许（本 Runtime 内）|
|---|---|
|Runtime A 与 B 同时对 Session X 各跑 Driver|同 Runtime 同 Session 双 Driver|
|各写各的 ndjson|同 Runtime 内 Hook 与 Driver 抢写锁跑长流程|

Driver 仍是**本进程**内：唯一执行该 Session 主程序、唯一 Fold **本进程可见投影**、唯一经本 Runtime Journal commit 该 Session 事实。

```
OpenCode Hook
    │ decode + MessageOrigin
    ├─ SessionInbox.TryPost
    └─ return

Session Driver (本 Runtime)
    │ run s / FIFO Receive / commit 本日志
    └─ 不见他 Runtime 运行期新增（KISS-04）
```

### 1.1 生命周期与启动时机 [NORMATIVE]

[FORBIDDEN] Human 到达瞬间跑完 `passReview >> Finish`（首轮宿主 assistant 未完成）。  
[FORBIDDEN] 插件 synthetic `chat.message` 升 LocalEpoch 取消自己。

[NORMATIVE]

```
HumanMessage (Origin = Human of TurnId):
  commit HumanTurnStarted { TurnId = userMessageId }  // 写入本 Runtime 日志
  Cancel 本 Runtime 旧 LocalEpoch CTS + 旧 Driver
  建立本 Runtime 内存 LocalEpoch 新基线
  **不**立即 passReview
  待宿主初始 turn 终态 → Gateway 激活本 Runtime 该 Session Driver

PluginGenerated PromptKey:
  不升 LocalEpoch；不 Cancel；绑 LocalPromptProtocol

HostInternal:
  不抢占；无法分类 → 明确策略，禁止猜 Human
```

重启：KISS-04 重新 BootSnapshot → 按需新 Driver；`sendOnce` 仅保证基于 **HistoricalPromptIndex（启动快照全 Runtime 历史）与 LocalPromptProtocol（仅本 Runtime 启动后事实）** 的幂等，不把仍存活他 Runtime 的 Pending 当全局锁。

### 1.2 死锁禁式

Hook 只 TryPost；Driver 独占本 Session 消费。禁 Hook/Driver 互抢阻塞锁等 terminal。

---

## 二、MessageOrigin [NORMATIVE]

```
type MessageOrigin =
    | Human of TurnId
    | PluginGenerated of PromptKey
    | HostInternal
```

`TurnId` 即宿主 `UserMessageId`（跨 Runtime 可靠持久化锚点）。  
仅 Human 带有 `TurnId` 并提交 `HumanTurnStarted { TurnId }` 到日志，同时提升**本 Runtime 内存独占的** `LocalEpoch` 并 Cancel 本侧旧 Driver。

---

## 三、Inbox：严格 FIFO [NORMATIVE]

```
type SessionInbox =
    abstract TryPost: SessionInboxEvent -> Result<unit, InboxFull>
    abstract Receive: CancellationToken -> Task<SessionInboxEvent>
```

[FORBIDDEN] 谓词 Wait 跳过控制事件。  
满：诊断 + 本 Session/Runtime Fail Closed；不静默 drop；不无限堵 Hook。

事件集同前：Human / Plugin / AssistantTerminal / PromptTransport / ToolAfter / SessionCommand / Lifecycle / Cancel。

领域等待 = 顺序 `Receive` 循环（Human 抢占、Cancel、Tool、Command、匹配 terminal…）。

相关 terminal：**Host UserMessageId / parentID**（即 `TurnId`），非插件 DispatchId。见 §五。

---

## 四、Hook 分类

同步变换 / 生命周期 TryPost / 有界 Tool+CommandPort / 禁止 Hook 长跑。同 KISS-03。

---

## 五、Prompt 协议 = 进程局部互斥与分立投影 [NORMATIVE]

### 5.1 范围与投影分立

```
同一 Runtime 的同一 Session：同时最多一个本地 PendingPrompt。
```

`HistoricalPromptIndex`（历史 Prompt 索引）与 `LocalPromptProtocol`（本地 Pending 协议）**严格分立投影**：

- **HistoricalPromptIndex**：启动时基于 `BootSnapshot` 建立的全 Runtime 历史 Prompt 索引，启动后为只读快照。
- **LocalPromptProtocol**：仅本 Runtime 启动后在内存维护的 Prompt 协议映射。

```
type LocalPromptProtocol = Map<SessionId, PendingPrompt option>
```

用于：本 Driver 重入、重复宿主事件、本进程 retry 误重发。  
**不**防止另一 Runtime 向同 Session 发 Prompt（合法，KISS-04），亦**不**将他 Runtime 运行期 Pending 视为本地锁。

### 5.2 宿主身份与 TurnId

OpenCode 宿主标志：User MessageID、Assistant MessageID、parentID、SessionID、Tool callID。  
无可靠自定义 correlation 回传。

```
type TurnId = MessageId

transport → UserMessageId → PromptSubmitted
terminal.parentID = UserMessageId → 属该 PromptKey (TurnId)
```

### 5.3 类型与键

```
type TurnId = MessageId

type LocalEpoch = int64   // 仅本 Runtime 进程内存递增，用于 CTS 取消，严禁写入持久化 Journal

type PromptRequest =
    { Key: PromptKey
      DispatchId: DispatchId          // 仅本地日志
      SessionId: SessionId
      TurnId: TurnId                  // 宿主消息 ID 绑定的 TurnId（取代裸 Epoch）
      Purpose: PromptPurpose          // 无 Nudge
      PayloadHash: string
      Model: Model option
      Attempt: int
      TriggerMessageId: MessageId option }
```

稳定键组成：`PromptKey = SessionId + TurnId + Purpose + Model + Attempt + TriggerMessageId + PayloadHash/WorkspaceAnchor`。  
[FORBIDDEN] 在 PromptKey 或持久化日志中使用裸 Epoch 作为身份。  

### 5.4 事实（写入本 Runtime 日志）

```
PromptRequested | PromptSubmitted | PromptSubmissionUnknown
PromptAccepted | PromptRejected | PromptAcceptanceUnknown
PromptTerminal { Key: PromptKey; UserMessageId: MessageId; AssistantMessageId: MessageId; Outcome: PromptOutcome }
```

*注：Journal 必须显式支持 `CommitUnknown` 与 `Poisoned` 状态处理；若时间记录使用 raw observed wall time，不得声称伪单调物理时间。*

### 5.5 Terminal 与 History Reconciliation（四分支）

当宿主 Terminal 事件到达或进行历史与协议对齐时，按以下四分支处理，不得将外部 Runtime 的 Pending 视为本地锁：

1. **分支一：Local Pending Match（本地 Pending 命中）**  
   宿主 Terminal 的 `parentID` 匹配 `LocalPromptProtocol` 中的本地 Pending 请求（`UserMessageId`）。  
   → 更新 `LocalPromptProtocol` 清除 Pending，提交 `PromptTerminal` 事实至本日志。

2. **分支二：Historical Match（启动快照历史已存在）**  
   `PromptKey` 或对应 `TurnId` 已记录在 `HistoricalPromptIndex`（启动前其他或本 Runtime 已终态的 Prompt）。  
   → 幂等忽略，不触发本地 Driver 重复动作，记录诊断日志。

3. **分支三：External Runtime Fact（外部 Runtime 或宿主原生事件）**  
   事件 `parentID` 既不匹配 `LocalPromptProtocol` 的本地 Pending，也不属于 `HistoricalPromptIndex`。  
   → 作为外部宿主/Runtime 事实写入本日志 stream；**不得**将外部 Pending 当作本地锁，**不得**阻塞本地 Driver。

4. **分支四：Unmatched / Stale Terminal（无法匹配或过时 Terminal）**  
   Terminal 事件 `parentID` 缺失或匹配已取消/过时的本地 Turn。  
   → 输出诊断日志，清理陈旧的本地协议状态（若存在），不中断 Driver 的 FIFO 队列处理。

### 5.6 sendOnce 规则与拆分

`sendOnce` 检查分为两步：
1. 查询 **HistoricalPromptIndex**：该 `PromptKey` 是否已存在于启动前历史全集？若存在 → 阻止发送（历史幂等）。
2. 查询 **LocalPromptProtocol**：该 `PromptKey` 是否在本 Runtime 启动后已处于 Pending 或 Completed 状态？若存在 → 阻止发送（本地幂等）。

只有两者皆无记录时方可发送。各 Runtime 仅保证内部不重复发送，不进行跨 Runtime 运行期 Blocking/Locking。
---

## 六、与主程序

无 idleProposals。Todo/Review while；Fallback 尾递归在 ContinueWork 内。

---

## 七、重启

见 KISS-04：枚举全部 runtime 日志 → Frontier → 归并 → 新文件。  
不持久化 Flow continuation。

---

## 八、不变量测试（Driver 侧）

```
同 Runtime 双 Driver 失败
跨 Runtime 同 Session 双 Driver 允许（集成测见 KISS-04/13）
FIFO 控制事件不饿死
Plugin 不升 LocalEpoch
Human 提交 HumanTurnStarted { TurnId }、升 LocalEpoch 且 Cancel 本侧旧 Driver Scope
PromptKey 绑定 TurnId（无裸 Epoch）
Host MessageId 相关
Accepted 未 Terminal 拒重发
sendOnce 分拆 HistoricalPromptIndex 与 LocalPromptProtocol 查询
Terminal Reconciliation 四分支正确收敛，外 Runtime Pending 不当本地锁
队列满 Fail Closed
本进程 read-your-writes
```

---

## 九、删除对照

|旧|新|
|---|---|
|全局每 Session 一 Driver|每 Runtime 内一 Driver|
|全局 Pending 互斥|本地 Pending（LocalPromptProtocol）|
|持久化 裸 Epoch 身份|持久化 TurnId (MessageId) 锚点|
|全局/持久化 Epoch 取消|内存 LocalEpoch（仅本 Runtime CTS 取消）|
|PromptKey 含 Epoch|PromptKey 含 TurnId（无裸 Epoch）|
|单系 sendOnce|HistoricalPromptIndex + LocalPromptProtocol 分立|
|workspace/session 锁|无（KISS-04）|
|Previous/Fork|无|
|DispatchId 相关 terminal|parentID / UserMessageId|
|idle / Nudge|删除|

---

*KISS-Driver 终。*
