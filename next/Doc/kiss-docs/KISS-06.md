# Session Runtime

承 KISS-02、KISS-Driver、**KISS-04**。

---

## 一、范围 [NORMATIVE]

Runtime = 一个 OpenCode 进程内的执行体。  
Session 投影 = **BootSnapshot 中该 Session 的历史 + 本 Runtime 后续事实**。

```
type SessionMeta =
    { SessionId: SessionId
      CurrentTurn: TurnId option   // 当前激活的 Turn 标识（若有）
      ProgressStamp: int64         // 本 Runtime 已 Apply 进展（如 Own 应用计数 / LocalSeq 映射）
      CreatedAt: DateTimeOffset }
```

不含 Nudge*/Owner/Lease/Stage/多 Generation。  
`LocalPromptProtocol` = 本地进程内存 Prompt 协议派生；`HistoricalPromptIndex` = 历史与本地成功 Prompt 索引。

```
type SessionRuntime =
    { Meta: SessionMeta
      Inbox: SessionInbox
      Driver: DriverSlot                      // 本 Runtime 内 0|1
      Todo: TodoView
      Review: ReviewView
      HistoricalPrompt: HistoricalPromptIndex // 历史与本侧成功提交的 Prompt 索引
      LocalPrompt: LocalPromptProtocol        // 本进程待发/运行中 Prompt 协议
      LocalEpoch: int64                       // 内存 LocalEpoch（仅防本侧迟到结果，不持久化）
      … }
```

`HistoricalPromptIndex` 汇总 BootSnapshot 与本进程已提交的事实，用于 `sendOnce` 历史去重；`LocalPromptProtocol` 为本进程内存中的 PendingPrompt 互斥锁。  
**外部 Runtime 的 pending/in-flight Prompt 不会形成本地锁**；其他进程的 pending 对本进程不可见且不阻塞本进程 Prompt 发送，各 Runtime 仅保证自身 sendOnce。
Writer：本 Runtime 的 Driver + Journal 队列。  
他 Runtime 运行期更新：不可见直至本进程重启。

### 1.1 Snapshot 与本地变化

```
BootSnapshot = 所有 Runtime 源在启动 Frontier 内的合法历史
OwnFacts     = 当前 Runtime 启动后成功 flush 的事实
SessionView  = Fold(BootSnapshot.SessionFacts + OwnFacts.SessionFacts)
```


`SessionRuntime` 可以有多个 Session，但每个 `(RuntimeId, SessionId)` 只有一个 Driver。SessionRuntime 不缓存他 Runtime 的文件 offset，也不注册 watcher；其他源的新增不是“待处理消息”，而是下一次启动的输入。

---

## 二、LocalEpoch [NORMATIVE]

`LocalEpoch` 是本进程 `SessionRuntime` 内存中保存的递增计数，**仅在内存生效且不写入持久化日志**。所有跨 Runtime 持久化身份不得使用裸 Epoch。

仅本侧 `MessageOrigin = Human`：`LocalEpoch` +1 → Cancel 本侧旧 Driver scope → 待初始 Host turn 终态后按需激活。

`LocalEpoch` 只防本 Runtime 内的迟到结果：

```
本侧 Human → LocalEpoch n+1 → cancel 本侧 n → 丢弃本侧旧 terminal
另一个 Runtime 的 Human → 当前 Runtime 不立即看见
下一次启动 → 两侧 HumanTurnStarted 按时间进入 Fold
```
它不是跨进程 fencing token，也不能阻止另一个 Runtime 在陈旧快照上产生合法历史。

---

## 三、ProgressStamp

随本 Runtime 成功 Apply 前进。禁手增。用于本侧 While 防空转。

---

## 四、Inbox + Driver

FIFO Receive。SessionCommand（Tool）在等待循环中处理。  
异 Session 可并行；同 Session 跨进程并行见 KISS-04。

等待 terminal 时，Driver 仍按 FIFO 处理本侧 Human、Cancel、ToolAfter、SessionCommand、AssistantTerminal 和迟到事件；不能用 predicate Wait 把控制事件留在队尾饿死。

Driver 负责把 Inbox 事件转成领域动作，但不把所有事件都当成“触发新主程序”：

|事件|等待 terminal 时的处理|
|---|---|
|本侧 Human|取消旧 scope，LocalEpoch +1，按启动时机重新激活|
|Cancel/Close|终止当前 Flow，进入清理|
|ToolAfter|Apply 工具结果或经 CommandPort 提交|
|AssistantTerminal|按 UserMessageId/parentID 归属当前 Prompt|
|其他/迟到|忽略或写诊断，不改变本侧投影|

这保证控制事件不会因某个领域动词等待 terminal 而饿死。

---

## 五、Todo / Review

由启动归并后的 Stream 投影 + 本侧 `TodoChanged` / `ReviewApplied` 更新。  

```
TodoChanged { Items = [...] }
ReviewApplied { Verdict = NeedsChanges; ResultingTodo = Some snapshot }
```

在两个 Runtime 同时操作时，所有事实都保留；较晚事件的具体效果由领域 Fold 定义，Journal 不做 LWW 冲突引擎。

---

## 六、生命周期

进程启动：KISS-04 Boot → 按需建 SessionRuntime。  
关闭：本侧 Settled / Cancel Driver；**不**接管他人 ndjson。

进程退出不需要把 Session 标成“全局 owner 已释放”。新进程会在启动时看到旧文件的合法前缀，并写自己的新文件；旧 Runtime 若仍活着，两个 Runtime 继续各自运行，直到未来某次启动重新归并。

---

## 七、对照

|旧|新|
|---|---|
|全局复合 State|本 Runtime 投影集|
|workspace 单写锁|Per-Runtime 日志|
|全局 Session Owner|本地 Driver|
|实时见他进程|仅启动快照|

---

*KISS-06 终。*
