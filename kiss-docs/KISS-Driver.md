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
[FORBIDDEN] 插件 synthetic `chat.message` 升 Epoch 取消自己。

[NORMATIVE]

```
HumanMessage (Origin = Human):
  commit HumanTurnStarted { Epoch = n+1 }  // 写入本 Runtime 日志
  Cancel 本 Runtime 旧 Epoch CTS + 旧 Driver
  建立新 Epoch 基线
  **不**立即 passReview
  待宿主初始 turn 终态 → Gateway 激活本 Runtime 该 Session Driver

PluginGenerated PromptKey:
  不升 Epoch；不 Cancel；绑本地 PromptProtocol

HostInternal:
  不抢占；无法分类 → 明确策略，禁止猜 Human
```

重启：KISS-04 重新 BootSnapshot → 按需新 Driver；sendOnce 仅保证**本 Runtime 历史 + 启动所见历史**上的幂等，不把仍存活他 Runtime 的 Pending 当全局锁。

### 1.2 死锁禁式

Hook 只 TryPost；Driver 独占本 Session 消费。禁 Hook/Driver 互抢阻塞锁等 terminal。

---

## 二、MessageOrigin [NORMATIVE]

```
type MessageOrigin =
    | Human
    | PluginGenerated of PromptKey
    | HostInternal
```

仅 Human 升**本 Runtime 所记录的** Session Epoch 并 Cancel 本侧旧 Driver。

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

相关 terminal：**Host UserMessageId / parentID**，非插件 DispatchId。见 §五。

---

## 四、Hook 分类

同步变换 / 生命周期 TryPost / 有界 Tool+CommandPort / 禁止 Hook 长跑。同 KISS-03。

---

## 五、Prompt 协议 = 进程局部互斥 [NORMATIVE]

### 5.1 范围

```
同一 Runtime 的同一 Session：同时最多一个本地 PendingPrompt。
```

```
type LocalPromptProtocol = Map<SessionId, PendingPrompt option>
```

用于：本 Driver 重入、重复宿主事件、本进程 retry 误重发。  
**不**防止另一 Runtime 向同 Session 发 Prompt（合法，KISS-04）。

### 5.2 宿主身份

OpenCode：User MessageID、Assistant MessageID、parentID、SessionID、Tool callID。  
无可靠自定义 correlation 回传。

```
transport → UserMessageId → PromptSubmitted
terminal.parentID = UserMessageId → 属该 PromptKey
```

### 5.3 类型与键

```
type PromptRequest =
    { Key: PromptKey
      DispatchId: DispatchId          // 仅本地日志
      SessionId: SessionId
      Epoch: int64                    // 本 Runtime 记录的 epoch
      Purpose: PromptPurpose          // 无 Nudge
      PayloadHash: string
      Model: Model option
      Attempt: int
      TriggerMessageId: MessageId option }
```

稳定键：SessionId + Epoch + Purpose + Model + Attempt + TriggerMessageId + TodoVersion/WorkspaceHash 等。  
[FORBIDDEN] 无宿主锚点的 ContinuationRound 伪 Ordinal。

### 5.4 事实（写入本 Runtime 日志）

```
PromptRequested | PromptSubmitted | PromptSubmissionUnknown
PromptAccepted | PromptRejected | PromptAcceptanceUnknown
PromptTerminal { Key; UserMessageId; AssistantMessageId; Outcome }
```

### 5.5 Clear（本地协议）

Accepted 不 Clear 至 Terminal；AcceptanceUnknown 不重发；新 Human Epoch 失效本侧协议；  
Requested 后崩溃：重启后按启动快照 + Host 状态 reconcile，不盲目重发。

### 5.6 sendOnce

Lookup **启动所见历史 + 本 Runtime 新事实** 中的 Key。  
不把其他仍存活 Runtime 未汇合的 in-flight 当全局锁。  
各 Runtime 只保证自己内部不重复发送。

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
Plugin 不升 Epoch
Human 升 Epoch 且 Cancel 本侧
Host MessageId 相关
Accepted 未 Terminal 拒重发
队列满 Fail Closed
本进程 read-your-writes
```

---

## 九、删除对照

|旧|新|
|---|---|
|全局每 Session 一 Driver|每 Runtime 内一 Driver|
|全局 Pending 互斥|本地 Pending|
|workspace/session 锁|无（KISS-04）|
|Previous/Fork|无|
|DispatchId 相关 terminal|parentID / UserMessageId|
|idle / Nudge|删除|

---

*KISS-Driver 终。*
