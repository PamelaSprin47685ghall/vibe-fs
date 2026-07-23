# 万象术 `next/` 重写终局蓝图

## 0. 总判定

`next/` 当前已经从“架构试验”进入“生产骨架”阶段：最新快照约有 **45 个生产 F# 文件、4,505 行代码**，已经出现 Per-Runtime Journal、SDK Host Port、PluginRuntime、真实 Tool 导出、Session Driver、PromptProtocol、Structured Flow 和真实 `opencode serve` 加载测试。方向正确，而且体量远小于旧实现。

但当前仍然不能运行成完整产品，甚至最新 `Session/Script.fs` 存在明显的模式匹配缩进错误；此外还有以下未闭合问题：

* `todowrite` 实际绑定的是 `dummyPort`；
* Todo 既可能通过 CommandPort 写，又可能在 `tool.execute.after` 再写一次；
* Driver 的历史 Prompt 索引没有从 Gateway BootSnapshot 初始化；
* Initial terminal 仍可能被无关 assistant terminal 误认；
* Prompt terminal 可能由 Driver 和 Script 重复提交；
* `AcceptanceUnknown` 没有宿主对账；
* Review 仍直接伪造 `Passed`；
* Todo 还是简化字符串列表，没有恢复公开契约中的 content/status/priority、方法论和交接信息；
* Process、PTY、Child、Fallback、Web、Fuzzy、Compaction、万象阵都未完成；
* 根 npm 包、根 fsproj 和发布产物仍完全指向旧 Mux/OMP/旧 OpenCode；
* 根目录和 `next/` 还混入了 `.omx/` 运行时垃圾。

所以当前总体完成度按“能够删除旧实现”计算约为 **25%**。

下面的路线以一个明确终局为目标，不允许再改方向。

---

# 第一部分：终局定义

## 1. 产品范围一次冻结

正式新版只保留两个产品入口：

### 万象术

唯一支持宿主：

```text
OpenCode
```

保留用户能力：

```text
多代理
Todo 与进度交接
With-Review
Fallback
文件与代码工具
Fuzzy / Glob / Web
Executor / PTY
方法论
上下文预算与 Compaction
重启恢复
取消、超时、错误诊断
```

### 万象阵

保留为独立 npm 子路径：

```text
wanxiangshu/wanxiangzhen
```

保留：

```text
需求拆分
DAG
并行 worktree
slave OpenCode
验证
ff-only 合并
重启恢复
孤儿清理
/squad
/squad-kill
```

### 正式删除

```text
Mux
OMP / oh-my-pi
Mimocode
MimoTui
多宿主抽象层
旧 .wanxiangshu.ndjson 格式
旧锁文件
Nudge 调度架构
Owner / Lease / Claim
Stage / Phase / Generation
SubsessionActor
Fallback 状态机塔
动态 Tool Registry
动态 MessageTransform Pipeline
跨进程实时投影同步
旧数据迁移器
双格式读取
旧实现 fallback
```

`future/` 中尚未进入公开产品的 Prefetcher、Enforcer 等未来设计，**不作为删除旧实现的阻塞项**。它们必须在旧实现删除后，基于新架构单独实施，不能趁迁移混入主线。

---

## 2. 最终目录

最终仓库中不再存在 `next/` 这个临时名称：

```text
src/
  Kernel/
    Identity.fs
    Fact.fs
    Outcome.fs
    Flow.fs

  Journal/
    Envelope.fs
    Codec.fs
    Reader.fs
    Writer.fs
    Fold.fs
    Gateway.fs

  Session/
    Runtime.fs
    Inbox.fs
    Driver.fs
    PromptKey.fs
    PromptProtocol.fs
    Script.fs
    Todo.fs
    Child.fs
    Review.fs
    Fallback.fs

  Process/
    Deadline.fs
    Command.fs
    Pump.fs
    Handle.fs
    Script.fs
    Pty.fs

  Tools/
    Types.fs
    Catalog.fs
    Permission.fs
    File.fs
    Search.fs
    Web.fs
    Executor.fs
    Pty.fs
    Subagent.fs
    Review.fs
    Methodology.fs
    MessageTransform.fs

  OpenCode/
    Types.fs
    Decode.fs
    ClientPort.fs
    Hooks.fs
    PluginTools.fs
    PluginRuntime.fs
    Plugin.fs

  Wanxiangzhen/
    Types.fs
    Config.fs
    Dag.fs
    Git.fs
    Worktree.fs
    Slave.fs
    Squad.fs
    Plugin.fs

tests/
  Unit/
  Contract/
  Integration/
  E2E/
  Fault/
  Stability/
  Packaging/
  Gates/

docs/
scripts/
wanxiangshu.fsproj
package.json
index.d.ts
```

没有：

```text
src/Hosts/
src/Runtime/
src/Kernel/Nudge/
src/Kernel/SessionControl/
src/Kernel/Subsession/
```

---

## 3. 最终运行时结构

```text
OpenCode
   │
   ▼
OpenCode Adapter
   │ raw obj 只在此出现
   ▼
PluginRuntime
   │
   ├── Gateway
   │     ├── BootSnapshot
   │     ├── 本 Runtime JournalWriter
   │     └── ProjectionSet
   │
   ├── SessionRuntime A
   │     ├── Inbox
   │     ├── Driver worker
   │     ├── Lifetime CTS
   │     ├── Current-turn CTS
   │     ├── Current human TurnId
   │     ├── AwaitingInitialTerminal
   │     ├── HistoricalPromptIndex
   │     ├── LocalPromptProtocol
   │     └── EarlyTerminalBuffer
   │
   └── SessionRuntime B
```

每个 `(RuntimeId, SessionId)` 只有一个 `SessionRuntime` 和一个 Driver worker。

删除当前重复控制结构：

```text
PluginRuntime.sessionRuntimes
+
SessionDrivers.drivers
+
SessionDrivers.localEpochs
```

所有本地 Session 生命周期状态归入唯一的 `SessionRuntime`。

---

## 4. 六条不可破坏的不变量

### 不变量一：先盘后内存

```text
append完整行
→ flush
→ Fold 到本 Runtime 投影
→ 对调用方返回 Committed
```

任何写入结果不确定：

```text
CommitUnknown
→ Writer Poisoned
→ 本 Runtime Fail Closed
```

### 不变量二：每 Runtime 单 Writer

```text
.wanxiangshu/runtimes/<RuntimeId>.ndjson
```

一个 Runtime 只写自己的文件；启动只读取固定 byte frontier；运行期间不 tail 其他 Runtime。

### 不变量三：一个 Session 一个本地 Driver

Hook 只负责：

```text
decode
→ TryPost
→ return
```

Hook 不运行长 Flow，不直接修改 Session 投影。

### 不变量四：Prompt 只按宿主 MessageId 相关

```text
assistant.parentID
→ UserMessageId
→ PromptKey
→ 唤醒唯一 waiter
```

禁止：

```text
任意 terminal 唤醒所有 waiter
按 session.status 猜 Prompt 完成
按时间窗口猜归属
```

### 不变量五：Tool 修改 Session 必须走 CommandPort

```text
todowrite
submit_review
return_reviewer
```

只能：

```text
Tool
→ SessionCommandPort
→ Inbox
→ Driver
→ Journal
→ Projection
→ reply
```

禁止 Tool 和 `tool.execute.after` 各写一遍。

### 不变量六：业务源码保留业务顺序

```text
Todo:    while unfinished → continue
Review:  request → verdict → todo
Child:   create → attach → send → terminal → result
Process: spawn → pump → wait → drain → dispose
Squad:   parallel work → verify → ordered FF
```

这些主路径中不得重新出现：

```text
Manager
Coordinator framework
Stage
Lease
Owner
Generation
callOnce
Wait(predicate)
第二套 EventBus
```

---

# 第二部分：迁移总账

## 5. 旧行为 Oracle

现有 OpenCode behavior manifest 中约有 **237 个行为项**。迁移不能按旧文件数量进行，必须按这些行为进行。

建立：

```text
docs/migration/behavior-ledger.json
docs/migration/behavior-ledger.generated.md
```

每条记录：

```text
id
category
userVisibleBehavior
decision          Keep | Replace | Delete
newOwner
tests
nextStatus        Missing | Skeleton | Implemented | Proven
legacyFiles
notes
```

分类总账：

| 类别     | 数量 | 终局处理                                  |
| ------ | -: | ------------------------------------- |
| BOOT   | 10 | 保留，归 Journal/Gateway/Plugin boot      |
| SCHEMA |  9 | 保留，归静态 Tool Catalog                   |
| PERM   |  5 | 保留，归 Tool Permission                  |
| FILE   | 13 | 保留并重写                                 |
| EXEC   | 13 | 保留并重写                                 |
| WEB    | 12 | 保留并重写                                 |
| COMP   | 12 | 保留，归 MessageTransform/Compaction      |
| CONC   | 12 | 保留用户行为，删除旧并发框架                        |
| PTY    | 11 | 保留并重写                                 |
| MSG    | 10 | 保留，归 OpenCode Codec                   |
| FUZZY  | 10 | 保留并重写                                 |
| CONT   | 10 | 替换为 Session Flow + PromptProtocol     |
| FB     | 24 | 保留行为，删除旧状态机                           |
| REV    | 20 | 保留并重写                                 |
| SUB    | 15 | 保留行为，删除 SubsessionActor               |
| NUDGE  | 15 | 用户可见续跑行为迁入 Flow；Owner/Lease/idle 调度删除 |
| ES     | 15 | 替换为 Per-Runtime Journal               |
| LIFE   | 15 | 保留并重写                                 |
| STAB   |  6 | 保留为最终稳定性门禁                            |

任何开发 PR 必须同时更新 ledger。

不存在以下说法：

```text
“文件写完了，所以行为完成了”
“UT 通过，所以 E2E 行为完成了”
“功能大概一样，所以旧测试不用迁”
```

只有 `Proven` 才表示完成。

---

# 第三部分：唯一施工顺序

```text
P0 可编译基线
   ↓
P1 范围与总账
   ↓
P2 Journal
   ↓
P3 PluginRuntime
   ↓
P4 OpenCode Codec
   ↓
P5 PromptProtocol
   ↓
P6 Driver / Human turn
   ↓
P7 Todo 纵切
   ↓
P8 Tool 平台
   ↓
P9 Process / PTY
   ↓
P10 Child
   ↓
P11 Review
   ↓
P12 Fallback
   ↓
P13 Transform / Config / Compaction
   ↓
P14 Search / Web / Methodology / 完整工具
   ↓
P15 万象阵
   ↓
P16 全行为 E2E / 故障 / 稳定性
   ↓
P17 根发布入口切换
   ↓
P18 删除旧实现
   ↓
P19 next 正名与防回流
```

后续可以拆 PR，但不能改变依赖顺序。

---

# 第四部分：逐阶段完整施工图

## P0：停止扩张，恢复可信基线

### 必做

1. 修复 `Session/Script.fs` 当前分支缩进和控制流错误。
2. 删除仓库中的：

   ```text
   .omx/
   next/.omx/
   ```
3. 加入 `.gitignore`。
4. 确认 `next/Wanxiangshu.Next.fsproj` 包含所有新增文件。
5. 确认 `tests-next` 的所有测试文件真实编译和运行。
6. 增加根命令：

   ```text
   npm run build:next
   npm run test:next:unit
   npm run test:next:integration
   npm run test:next:e2e
   npm run test:next:all
   ```
7. clean checkout 后完整执行。
8. 禁止测试 runner 吞掉编译失败。
9. 禁止通过残留 `build/next` 让测试假绿。
10. 根 CI 同时执行旧测试和 next 测试，但旧代码冻结，不再修新需求。

### 同期修掉的明显接线错误

* 删除 `PluginTools` 中的 `dummyPort`；
* `todowriteTool` 必须在每次 execute 时使用当前 `ToolContext.Session`；
* 删除重复的硬编码 `handleToolDefinition` 工具表；
* 工具 SSOT 只能是 `PluginTools/Catalog`；
* `tool.execute.after` 不再解释并写入 Todo；
* Tool Deadline 不得固定重新分配 30 秒；
* Tool 使用本次调用 Cancellation，而不是整个 PluginRuntime 的 lifetime token。

### 出口

```text
next clean build = green
tests-next all = green
无 .omx tracked files
无 dummy port
无重复 Tool catalog
```

在该阶段通过前，不开发 Child、Review、Fallback、Web 或万象阵。

---

## P1：冻结正式功能契约

### 必做

1. 建立 237 项 behavior ledger。
2. 为每项作出 Keep/Replace/Delete 决定。
3. 重写 next 版 README 草案，明确：

   ```text
   OpenCode only
   no Mux
   no OMP
   no old data migration
   ```
4. Todo 正式 Schema 冻结。

不得继续使用当前简化形式：

```text
todos: string[]
```

应恢复用户契约：

```text
TodoItem =
  { Content: string
    Status: Pending | InProgress | Completed | Cancelled
    Priority: Low | Medium | High | None }

TodoSnapshot =
  { Items: TodoItem list
    SelectedMethodologies: MethodologyId list
    Handoff: WorkReport option }
```

`ProgressStamp` 由成功 Apply 的投影版本产生，不因时间经过或空调用变化。

5. Review Verdict 冻结：

   ```text
   Passed
   NeedsChanges of requests
   Invalid of reason
   WorkInProgress
   ```
6. Tool 权限矩阵冻结。
7. Fallback 配置和模型继承规则冻结。
8. 万象阵的正式对外接口冻结。

### 出口

* behavior ledger 无 `Unknown`；
* 所有公开行为都有新 Owner；
* 所有删除行为已明确写入新版 README；
* 未来功能未被偷偷纳入迁移范围。

---

## P2：Journal 达到可托付级

当前 Journal 已有良好基础，但必须完成以下闭环。

### Writer

1. `openSync(..., "wx")` 直接作为碰撞判断。
2. `EEXIST` 时生成新 RuntimeId 并重试。
3. 其他错误立即 `StorageFailed`。
4. 初始化第一行失败时：

   * close fd；
   * 删除空文件或无完整行文件；
   * 不留下幽灵 Runtime。
5. 实现 `writeAll`：

   ```text
   while offset < buffer.length:
       written = writeSync(...)
       written <= 0 → poison
   ```
6. 每行必须 UTF-8 Buffer 写入。
7. flush 失败：

   ```text
   poisoned = true
   return CommitUnknown
   ```
8. Poison 后拒绝全部后续 Append。
9. Dispose：

   * 最终 flush；
   * close；
   * 幂等；
   * 不吞掉首次关闭错误。

### Reader / Boot

1. 按 Buffer 字节读取，不用字符串字符位置截 frontier。
2. 只接受以换行结束的完整行。
3. EOF 合法 JSON 但无换行仍忽略。
4. 中间非法行：

   * 当前 Runtime 源在该行处停止；
   * 其他 Runtime 不受影响；
   * 记录诊断。
5. 校验：

   ```text
   filename RuntimeId == Envelope.RuntimeId
   LocalSeq 从 1 严格连续
   Stream/Fact 可解码
   ```
6. Reader 永远不改写、不截尾其他 Runtime 文件。
7. Boot frontier 启动后固定。
8. 当前生命周期不读取其他 Runtime 后续追加。

### 时间线

归并规则必须能处理同源时钟回拨：

```text
同 Runtime：LocalSeq 决定顺序
不同 Runtime：ObservedAt，再以 RuntimeId/LocalSeq 稳定破同
```

不能把每个源错误假设为按 wall clock 单调。

### Fold

* 所有投影只由 Fact 推导；
* Review Round 进入投影；
* Todo Revision 进入投影；
* Session 当前 Human Turn 进入投影；
* Prompt History 保存 Requested、Submitted、Uncertain、Terminal；
* Child 保存物理 SessionId、完成状态和是否可 continue；
* Squad 保存任务、验证、FF 状态；
* Version 每次成功 Apply 严格递增。

### 必过故障测试

```text
UTF-8 多字节 frontier
半行
完整 JSON 无换行
中间坏行
空文件
重复 LocalSeq
跳 LocalSeq
RuntimeId 不符
partial write
flush 失败
创建碰撞
初始化失败
Dispose 两次
一个源坏、其他源正常
同 Session 多 Runtime
同源时钟回拨
kill -9 后重启
Reader 零写入
```

### 出口

Journal 相关 behavior ledger 全部 `Proven`。

---

## P3：统一 PluginRuntime 所有权

### 删除

```text
SessionDrivers
DriverSlot
独立 localEpochs Dictionary
GetInboxMap 每次复制 Dictionary
模块级 activeRuntime
```

### 新结构

```text
type SessionRuntime =
  { SessionId
    Inbox
    LifetimeCts
    DriverTask
    mutable TurnCts
    mutable LocalEpoch
    mutable CurrentTurn
    mutable AwaitingInitialTerminal
    mutable HistoricalPrompts
    mutable PendingPrompts
    mutable UserMessageToPrompt
    mutable EarlyTerminals
    mutable IsClosing }
```

### PluginRuntime 负责

* 唯一 SessionRuntime Dictionary；
* `GetOrCreateSession`；
* async `CloseSession`；
* async `DisposeAsync`；
* 禁止 Dispose 后创建新 Session；
* 启动错误直接让插件加载失败；
* 捕获真实 OpenCode SDK client；
* 生产环境无 SDK client 时直接失败，不静默运行；
* Gateway CTS 与 PluginRuntime CTS 是同一个根 scope；
* Session lifetime 与 turn lifetime 分离。

### Dispose 顺序

```text
停止接收新 Hook
→ 标记 closing
→ cancel 所有 turn
→ cancel 所有 driver inbox receive
→ await driver tasks
→ kill child/process/PTY
→ complete所有 waiter
→ flush/close Journal
→ dispose CTS
```

禁止：

```text
DisposeAsync() |> ignore
catch _ -> ()
```

除非错误被汇总成结构化诊断并在最后返回。

### 出口

* 两个 plugin instance 完全隔离；
* 两 workspace 不串线；
* Dispose 后无 Driver、timer、waiter、process；
* 反复 init/dispose 100 次无泄漏。

---

## P4：完成 OpenCode Adapter 和 Codec

Adapter 是唯一允许接触原始 JS `obj` 的地方。

### Human message

必须解出：

```text
SessionId
UserMessageId
TurnId = UserMessageId
Agent
Provider
Model
Variant
ObservedAt
MessageOrigin
```

处理：

```text
Human
→ append HumanTurnStarted
→ LocalEpoch +1
→ cancel旧 turn
→ CurrentTurn = TurnId
→ AwaitingInitialTerminal = UserMessageId
→ 不启动 Flow
```

### Plugin-generated message

必须携带稳定的 Prompt 标识或由 UserMessageId 映射识别。

处理：

```text
不增加 LocalEpoch
不取消自己的 Flow
不重新当作 Human
```

### Assistant terminal

解出：

```text
AssistantMessageId
parentID / UserMessageId
finish reason
error
aborted
ObservedAt
```

Driver 只接收强类型事件。

### Event 去重

OpenCode 可能重复投递更新事件。增加本 Runtime 有界内存去重：

```text
(EventType, MessageId, terminal signature)
```

不需要持久化第二套去重日志。

### Hook 类别

同步 Hook：

```text
chat.transform
tool.definition
tool.execute.before
config
```

异步投递 Hook：

```text
chat.message
event
tool.execute.after
command
```

异步投递 Hook 不运行 Session Flow。

### 出口

Contract tests 使用真实 OpenCode 事件样本证明：

* Human/PluginGenerated/HostInternal 分类正确；
* initial assistant terminal 可准确关联 Human Turn；
* continuation terminal 可准确关联 Plugin Prompt；
* abort/error/stop 全部标准化；
* 重复 terminal 不重复结算。

---

## P5：PromptProtocol 成为唯一 Prompt 外部协议边界

当前 Prompt 逻辑分散在 Driver 和 Script 中。终局必须集中。

### 最终 PromptKey

```text
PromptKey =
  SessionId
  + TurnId
  + Purpose
  + Model
  + Attempt
  + TriggerMessageId
  + RevisionAnchor
```

Purpose：

```text
ContinueTodo
RequestReview
ContinueChild
FallbackRetry
SquadPlanning
```

RevisionAnchor 使用领域版本，例如：

```text
TodoRevision
ReviewRound
ChildRequestId
```

禁止伪造“ContinuationRound”作为持久化程序计数器。

### 持久事实

```text
PromptRequested
PromptSubmitted
PromptSubmissionUncertain
PromptTerminal
```

不能只有 Requested 后在内存里丢掉失败信息。

### 内存状态

```text
PendingByPromptKey
PromptKeyByUserMessageId
EarlyTerminalByUserMessageId
WaiterByPromptKey
```

不要使用：

```text
Map<SessionId, PendingPrompt option>
```

因为 Review、Child、Fallback 或并行 Child 可能存在不同 Purpose。

### 发送算法

```text
1. 从 Gateway Projection 重建 HistoricalPromptIndex
2. evaluateSendOnce
3. Historical terminal → 直接返回历史结果
4. requested/submitted 未闭合 → 先 reconcile，不盲发
5. 创建 PendingPrompt + waiter
6. commit PromptRequested
7. 调用 SDK client.session.prompt
8. Delivered(userMessageId):
      建立 userMessageId → PromptKey
      检查 EarlyTerminalBuffer
      commit PromptSubmitted
9. AcceptanceUnknown:
      commit PromptSubmissionUncertain
      查询宿主 messages/status
      能识别则补 Submitted/Terminal
      仍不确定则 PromptUncertain，Fail Closed
10. terminal 由 Driver 路由到 PromptProtocol
11. PromptProtocol commit PromptTerminal
12. 更新 Historical
13. 清 Pending 与 waiter
14. 返回 SendOutcome
```

### 关键竞态

宿主可能在 `SendPrompt` Promise 返回前发出 terminal。

因此 Driver 遇到未知 `parentID` 时不能立刻丢弃，应暂存于有界：

```text
EarlyTerminalBuffer[parentID]
```

`recordSubmitted` 后立即检查并消费。

### 唯一提交原则

```text
Driver：只路由 terminal
PromptProtocol：唯一写 PromptTerminal
Script：只等待 SendOutcome
```

当前 Driver 和 Script 双写 PromptTerminal 的路径必须删除。

### 重启对账

* Historical Terminal：不重发；
* Historical Submitted：查询宿主消息；
* Historical Requested：查询宿主是否已创建 message；
* Historical Uncertain：查询宿主；
* 无法证明未发送：不盲重发。

### 出口

必须覆盖：

```text
send后立刻terminal
terminal早于SDK返回
重复terminal
迟到terminal
旧turn terminal
错误parentID
cancel
timeout
AcceptanceUnknown
崩溃于Requested后
崩溃于Submitted后
崩溃于Terminal写入前
同Runtime sendOnce
不同Runtime互不加锁
```

---

## P6：Session Driver 与 Human Turn 生命周期闭合

Driver worker 从 PluginRuntime 创建起一直存活，不能因一个 turn cancel 而死亡。

### Token 分层

```text
Plugin lifetime CTS
  └── Session lifetime CTS
        └── Current turn CTS
              └── 具体 Prompt/Child/Process linked CTS
```

`CancelEvent` 默认取消当前 turn，不取消 Driver worker。

只有：

```text
session close
plugin dispose
fatal journal poison
```

才结束 Driver worker。

### 事件处理

#### Human

```text
commit HumanTurnStarted
cancel CurrentTurnCts
LocalEpoch +1
CurrentTurn = new TurnId
AwaitingInitialTerminal = HumanMessageId
不运行 Session Flow
```

#### Assistant terminal

优先级：

```text
1. parentID 命中 PromptKeyByUserMessageId
   → PromptProtocol

2. parentID == AwaitingInitialTerminal
   → 清 awaiting
   → 启动本 Turn Session Flow

3. parentID 命中 EarlyTerminal 可对账项
   → buffer

4. 其他
   → 记录诊断并忽略
```

不能使用：

```text
if awaitingNativeTerminal || flowCts.IsNone then startFlow
```

因为无关 terminal 会启动 Flow。

#### SessionCommand

即使 Flow 正在等 terminal，也必须 FIFO 处理并回复 Tool。

#### ToolAfter

只保留非领域诊断用途。Todo、Review 不在此处二次写入。

#### 新 Human 抢占

* 旧 Flow 收到 cancel；
* 旧 Prompt terminal 迟到时被 epoch/turn 检查拒绝；
* Driver worker 继续；
* 新 Human initial terminal 后启动新 Flow。

### Flow 完成

```text
Completed → SessionSettled Completed
Cancelled by newer Human → 不写全局 Completed
Explicit user cancel → SessionSettled Cancelled
Fatal → SessionSettled Failed
```

每个 Turn 最多一个 Settlement。

### 出口

轨迹测试：

```text
Human → native terminal → no todo → settled
Human → native terminal → todo → continuation → settled
Human A → continuation → Human B抢占
无关assistant terminal不启动
PluginGenerated不增加epoch
Cancel不杀Driver
Tool command等待期间可完成
Session close终止全部资源
```

---

## P7：完成 Todo 端到端纵切

这是第一条必须通过真实宿主 E2E 的业务闭环。

### Todo SSOT

只有：

```text
TodoChanged { Snapshot; Revision }
```

Tool 每次提交完整 Snapshot。

Unfinished：

```text
存在 status = Pending 或 InProgress
```

Completed/Cancelled 不算未完成。

### `todowrite`

输入包括：

```text
todos
select_methodology
handoff/progress reports
```

严格校验：

* content 非空；
* status 枚举；
* priority 枚举；
* 不接受坏 JSON 后静默变成空 Todo；
* 空 Todo 必须是用户显式传入 `[]`；
* 方法论 ID 必须存在；
* 报告字段按最终契约校验。

执行：

```text
decode
→ permission
→ SessionCommandPort.UpsertTodo
→ Driver commit TodoChanged
→ reply
```

### Session Flow

```text
while Todo.Unfinished:
    outcome = ContinueWork(todo revision)
    重新读取投影
```

`ContinueWork` 不替 LLM 猜测“完成第一项”。

删除：

```text
CommitTodoFrom = return Ok()
List.tail
tool.execute.after解析todo
```

NoProgress：

```text
ContinueWork terminal 返回
但 Projection.Version 未增加
且 Todo 仍 Unfinished
→ SessionError.NoProgress
```

### 第一条真实 E2E

```text
启动真实 opencode serve
加载 next plugin
严格 Mock LLM

首轮 Human
→ LLM 调 todowrite，留下 1 个 pending
→ initial assistant terminal
→ Driver 发送恰好 1 次 continuation
→ 第二轮 LLM 调 todowrite，将其 completed 或清空
→ continuation terminal
→ 不发送第三次
→ SessionSettled
→ Journal 事实顺序正确
→ dispose 无泄漏
```

这条测试通过前，其他高级功能不得被声明完成。

---

## P8：静态 Tool 平台完整化

### 唯一 Catalog

```text
ToolCatalog.all : Tool list
```

启动时检查：

```text
名称唯一
Schema 可解析
Description 非空
Permission 条目完整
```

禁止动态注册和反射。

### ToolContext

必须包含：

```text
SessionId
WorkspaceRoot
AgentRole
Cancellation
Absolute Deadline
SessionCommandPort
HostPort
```

### 通用执行边界

每个 Tool 自动获得：

```text
decode failure → structured ToolOutput
permission denial → structured ToolOutput，零副作用
unexpected exception → diagnostic + structured failure
output limit → truncation marker
deadline expired → timeout value
```

### 文件安全

所有路径：

1. 解析为绝对路径；
2. `realpath`；
3. 必须位于 workspace；
4. 防 `../`；
5. 防 symlink escape；
6. 写操作原子；
7. read 有行数、字节和 token 上限；
8. edit 要求唯一匹配或显式 replace-all；
9. 编码错误结构化返回。

### TDD 与方法论契约

旧公开 Schema 中的：

```text
warn_tdd
原则确认
select_methodology
完整报告
```

如决定保留，必须在 ledger 中逐项实现；如决定删除，必须同步改 README 和行为测试，不能悄悄丢失。

### 出口

基础工具：

```text
todowrite
read
write
edit
executor
```

全部通过真实 OpenCode Tool execute E2E，而不是只调用 F# 函数。

---

## P9：Process、Executor、PTY 完成资源闭环

### Process 正确序列

```text
spawn
→ 立即安装 stdout/stderr/error/exit listeners
→ 返回 Handle
→ write stdin
→ await exit/cancel/deadline
→ drain stdout/stderr
→ dispose listeners/streams
```

禁止 15ms 猜测 spawn 是否失败，禁止 polling 等待退出。

### Cancel / Deadline

```text
SIGTERM 或平台等价操作
→ 在剩余 Deadline 内等待
→ 未退出则 SIGKILL
→ 杀整个 process tree
→ await exit
→ await pump drain
→ 返回 Cancelled/Timeout
```

Tool 返回时进程必须已经终结。

### 输出

* stdout/stderr 分开；
* 字节上限；
* 保留尾部或按契约截断；
* 明确 `Truncated`；
* 二进制/非法 UTF-8 不崩溃；
* stdin 错误可见。

### PTY

正式实现：

```text
pty_spawn
pty_write
pty_read
pty_list
pty_kill
pty_resize
```

需要：

* 真 PTY；
* 每 Session 独立 Handle table；
* read 有界；
* kill 幂等；
* session dispose 全清；
* Linux/macOS/Windows 明确支持矩阵。

### 出口

```text
忽略SIGTERM的进程
无限stdout
无限stderr
spawn失败
stdin broken pipe
cancel
timeout
父进程生子进程
PTY退出
插件dispose
```

均无 hang、无遗留 PID。

---

## P10：Child/Subagent 完整化

### 物理生命周期

```text
GetOrCreateChild
→ Host CreateChildSession
→ commit ChildCreated
→ 注册 terminal 相关
→ SendPrompt
→ 等 terminal
→ 读取 child messages
→ 提取结果
→ commit ChildCompleted
```

Child 完成后默认保留物理 Session，以支持：

```text
continue
```

只有：

```text
显式 close
parent abort
plugin dispose
资源策略淘汰
```

才关闭物理 child。

### 稳定身份

```text
ChildId          万象术领域 ID
HostSessionId    OpenCode 物理 Session ID
RequestId        本次调用 ID
PromptKey        本次发送 ID
```

不要混为一个字符串。

### 角色

```text
coder
inspector
browser
meditator
reviewer
```

各角色拥有明确权限矩阵。

### RunParallel

```text
输入顺序稳定
物理执行并行
返回顺序与输入一致
全部 await
部分失败作为值
parent cancel 全部传播
```

### 重启

BootSnapshot 中看到：

* ChildCreated 无 Completed；
* 查询 Host session；
* 已 terminal 则补完成；
* 仍运行则恢复等待；
* Host 不存在则 ChildFailed；
* 不盲目创建第二个 child。

### 出口

包括：

```text
create失败
send失败
AcceptanceUnknown
立即terminal
空输出
多assistant消息
continue
parent abort
并行部分失败
重启恢复
close
dispose
```

---

## P11：Review 完整闭环

### 主流程

```text
finishTodo
→ while Review.Required:
      RequestReview
      AcceptVerdict
      finishTodo
→ Finish
```

### RequestReview

1. 创建或复用 reviewer child；
2. 构造审查上下文；
3. 发送 Prompt；
4. reviewer 调 `return_reviewer`；
5. verdict 经 CommandPort 返回；
6. Driver 提交一个复合事实：

```text
ReviewApplied
  { Verdict
    Round
    ResultingTodo }
```

不能先写 Review 再写 Todo 两个事实，避免中间不一致。

### Verdict

```text
Passed
NeedsChanges
Invalid
WorkInProgress
```

* Passed → Required=false；
* NeedsChanges → ResultingTodo；
* Invalid → 有界重试；
* WIP → 仍 Required；
* Round 从投影读取，不得固定 0 或 1；
* `MaxRound` 和 `MaxInvalidRetries` 是不同配置；
* 达到上限返回 `ReviewExhausted`，不伪造 Passed。

### Tools

```text
submit_review
return_reviewer
```

权限：

* Manager/main 可 submit_review；
* reviewer 可 return_reviewer；
* 其他角色拒绝；
* reviewer 不可递归创建 reviewer。

### 出口

完整轨迹：

```text
Passed
NeedsChanges一次
NeedsChanges多轮
Invalid后Passed
Invalid耗尽
WIP
空报告
reviewer child失败
parent抢占
重启
```

---

## P12：Fallback 尾递归闭环

### 保留行为，删除旧架构

不保留：

```text
FallbackCoordinator
Governor
LeaseValidation
Recovery StateMachine
Episode Owner
```

只保留一个可读尾递归：

```text
for model in chain:
    for attempt in 1..maxRetries:
        result = sendContinue(model, attempt)
        match result:
            Delivered → return
            Retryable → retry
            Fatal → next model 或失败
            AcceptanceUnknown → reconcile
```

### 必须继承

Continuation 应继承原 Human turn：

```text
Agent
Provider
Model/Variant
可见上下文
Todo revision
```

切换模型时只改明确配置字段。

### Journal

每次尝试通过 PromptKey 和 Prompt facts 记录，不额外制造 Fallback PC 状态。

### AcceptanceUnknown

先 Host reconcile，不允许直接换模型，因为原模型可能已被宿主接受。

### 出口

```text
同模型重试
换模型
全部失败
fatal
timeout
空输出
AcceptanceUnknown
重启
Human抢占
成功后Todo只提交一次
无额外LLM调用
```

---

## P13：MessageTransform、Config、Compaction 与上下文预算

### MessageTransform

固定纯函数顺序：

```text
sanitize
→ addCaps
→ addReviewContext
→ addTodoHandoff
→ addMethodology
→ addParallelHint
→ ensureToolIntegrity
```

要求：

* 幂等；
* 不修改输入；
* 无 IO；
* 无 Prompt；
* 无动态 Stage；
* synthetic parts 有稳定 ID 前缀；
* 不把 synthetic read 当作主模型工具调用。

### Config

最终配置只支持 OpenCode：

```text
agents
models
fallback chains
review limits
tool permissions
output limits
context budget
squad options
```

坏配置：

* 明确报错；
* 不静默使用半默认状态；
* 默认值集中一处。

### Model Resolution

统一一个函数：

```text
resolveModel(agent, purpose, config)
```

不要在 Fallback、Review、Child、Plugin 各自解析。

### Compaction

必须恢复：

* Todo；
* Progress/Handoff；
* Review；
* Child 摘要；
* 当前方法论；
* 当前工作阶段的业务信息。

但不恢复旧 Stage/Phase 控制状态。

### Context Budget

* 预算来自宿主上下文；
* 达阈值时引导 LLM 写 Todo/Handoff；
* 不通过 Nudge Manager；
* 不无限发送 prompt；
* 同一个预算事件 sendOnce；
* compaction 后仍可恢复。

### Commands

```text
/loop
/squad
/squad-kill
```

`/loop`：

* 有参数：开始 With-Review；
* 空参数：按产品契约取消；
* 不制造第二 Human Turn；
* 不泄漏内部 nonce。

### 出口

变换 golden、幂等、compaction、context budget、配置错误和 model inheritance 全部有 Contract/E2E。

---

## P14：完整工具与用户能力补齐

按以下顺序迁移：

### 搜索

```text
glob
fuzzy_find
fuzzy_grep
fuzzy_continue
```

要求：

* fuzzy_continue 使用 iterator token；
* token 有 Session/Runtime 作用域；
* 超时清理；
* 不全仓 O(N) 重扫；
* 输出有界；
* 路径安全。

### Web

```text
web_search
web_fetch
```

要求：

* URL 校验；
* SSRF 防护；
* 超时；
* redirect 上限；
* 响应大小上限；
* content type；
* HTML 转文本；
* 错误作为值；
* 无隐式无限重试。

### Question

* 与 OpenCode 宿主交互；
* cancel/timeout；
* 无法交互时明确失败；
* 不阻塞 Driver。

### Methodology

* 54 种方法论静态 Catalog；
* meditator 只使用允许工具；
* Todo 中的 `select_methodology` 与上下文注入一致；
* 不建立动态 Registry 框架。

### Patch / Swap / Tree-sitter

只迁移 ledger 中确认保留的用户行为。

不因旧代码存在就整套搬迁。

### 权限矩阵

角色 × 工具生成 Contract tests：

```text
main
coder
inspector
browser
meditator
reviewer
squad slave
```

### 出口

所有 FILE/EXEC/WEB/FUZZY/PTY/PERM/SCHEMA 行为均 `Proven`。

---

## P15：万象阵完整重写

当前 `SquadFlow` 只是抽象骨架，不能作为完成依据。

### 领域数据

```text
Squad
Wave
Task
Dependency
Worktree
Slave
Verification
FastForward
```

### 正式流程

```text
/squad requirement
→ planner child 生成 DAG
→ 验证 DAG
→ 选择 ready tasks
→ 为每项创建 worktree
→ 启动 slave OpenCode
→ slave 使用万象术 /loop
→ 等任务完成
→ 验证 git 状态与测试
→ 按稳定顺序 ff-only
→ commit TaskVerified
→ commit WaveAccepted
→ 下一 wave
→ 全部完成
```

### Worktree

* 唯一目录；
* 唯一分支；
* 创建失败原子回滚；
* 不删除有未提交工作的现场；
* 默认保留失败现场供诊断；
* 成功后按策略清理。

### Git

* 只允许 ff-only；
* merge base 校验；
* dirty target 拒绝；
* 冲突作为失败值；
* 每项 FF 后重新验证；
* 不依赖 shell 字符串拼接。

### Slave

* process handle 受控；
* pid 不作为永久身份；
* restart 后检查真实进程和 OpenCode session；
* orphan 可发现；
* kill 有界并清子树。

### 持久化

万象阵写同一套 Per-Runtime Journal Fact，但不和万象术共写一个 fd，也不复活旧共享 `.wanxiangshu.ndjson`。

### HTTP

仅提供：

```text
read projection
submit command
health
```

不把 HTTP server 变成第二个状态所有者。

### 出口

```text
单任务
并行任务
依赖DAG
任务失败
验证失败
FF失败
slave死亡
coordinator重启
orphan
/squad-kill
两个Runtime
```

全部真实 E2E。

---

# 第五部分：测试总体系

## P16：从“测试文件很多”升级为“行为已证明”

## 6. 测试分层

### L0：纯单元

```text
Fold
PromptKey
PromptProtocol decision
Todo unfinished
Review required
Fallback recursion
DAG readiness
Permission
Path normalization
Deadline
```

### L1：Contract

```text
OpenCode codec
SDK client调用形状
Tool schema
Tool execute返回形状
MessageTransform
Config decode
Fact codec
```

### L2：Integration

使用 Fake Host Port，但使用真实：

```text
Gateway
Journal
SessionRuntime
Driver
PromptProtocol
Tool
Process
```

不得把直接调用 Plugin Hook 的测试叫 E2E。

### L3：Real OpenCode E2E

必须：

```text
启动真实 opencode serve
由宿主加载打包后的 plugin
严格 Mock LLM provider
通过 HTTP/SSE 操作
检查真实 messages/tools/events/files/journal
```

### L4：Fault Injection

```text
kill -9
坏 Journal
partial write
flush failure
host timeout
AcceptanceUnknown
process不退出
PTY断开
child死亡
网络半断
plugin dispose
```

### L5：Stability

```text
重复100次启动/退出
连续多轮Human
并发多Session
长Todo
多Child
Fallback风暴
Compaction
无固定sleep关键断言
无额外LLM请求
无PID/端口/handle泄漏
```

### L6：Packaging

```text
npm pack
空目录 npm install tarball
只从公开 export 导入
真实 OpenCode 加载
无源码/本地路径依赖
```

---

## 7. 双轨比较方法

旧实现只作为冻结 Oracle，不能与 next 共用状态。

```text
同一个严格 Scenario
├── workspace-old → 旧插件
└── workspace-next → 新插件
```

比较：

```text
用户可见消息
工具集合与Schema
文件副作用
Prompt次数和顺序
模型/agent继承
Todo终态
Review终态
Child结果
取消结果
重启结果
```

不比较：

```text
旧事件名
旧 Stage
旧 Lease
旧 Owner
旧 NDJSON 字段
内部文件数量
```

每个 behavior ledger 项必须链接至少一个 next 测试。

---

## 8. 禁止弱测试

禁止：

```text
只检查文件存在
只检查函数名存在
只检查 /command 有 loop
只检查 Hook 被调用
固定 sleep 后猜完成
异常 catch 后仍通过
“later lifecycle events when present”
```

关键生命周期事件必须是必需断言，不能“有就检查，没有也算通过”。

---

# 第六部分：发布切换

## P17：根入口一次切换到新实现

旧 `src/` 在此之前保持冻结、只读、不再接需求。

### 根 fsproj

将根 `wanxiangshu.fsproj` 替换为 next 的正式工程。

不再编译：

```text
src/Hosts
src/Runtime
旧 src/Kernel
```

### package.json

最终：

```json
{
  "main": "build/src/OpenCode/Plugin.js",
  "exports": {
    ".": {
      "types": "./index.d.ts",
      "import": "./build/src/OpenCode/Plugin.js"
    },
    "./wanxiangzhen": {
      "types": "./index.d.ts",
      "import": "./build/src/Wanxiangzhen/Plugin.js"
    }
  }
}
```

删除：

```text
./omp
Mux default
Mimocode exports
MimoTui exports
```

### build-package.json

与根 package：

* 同版本；
* 同 exports；
* 同 dependencies；
* 不再存在当前根版本 `0.3.0`、build package `0.1.4` 的分裂。

### 数据目录

正式名称：

```text
.wanxiangshu/runtimes/
```

不要永久保留 `.wanxiangshu-next`。

旧：

```text
.wanxiangshu.ndjson
```

生产代码完全不读取、不迁移、不双写。其存在不得影响新版启动。内测发布前按既定策略删档。

### 切换门槛

以下全部通过：

```text
behavior ledger Keep/Replace = Proven
Real OpenCode E2E = green
Wanxiangzhen E2E = green
Fault = green
Stability = green
npm tarball smoke = green
```

切换后连续只运行新实现的全部 Gate，不再提供运行时选择旧实现的开关。

---

# 第七部分：拆除旧实现

## P18：一个拆除 PR 删除全部 legacy production

不要长期留下 `legacy/` 目录，也不要把旧代码改名归档在主分支。

直接删除：

```text
src/Hosts/Mux/
src/Hosts/Omp/
src/Hosts/OpenCode/
src/Kernel/
src/Runtime/
旧 wanxiangshu.fsproj
旧 integration/
旧 tests/
旧 e2e 中只服务旧宿主的部分
旧 build scripts
旧 index.d.ts
旧 README
旧 docs
```

需要复用的 E2E harness：

* 移到新 `tests/E2E/Harness`；
* 删除旧插件特有逻辑；
* 不通过 import 旧生产代码复用。

### 删除旧依赖

按新源码真实 import 审计：

```text
proper-lockfile
旧 TOML/YAML 库
旧 tree-sitter 包
旧动态 registry 依赖
旧 Mux/OMP SDK
```

仍被新功能使用的依赖才保留。

### 删除旧架构测试

以下旧内部实现测试不迁：

```text
Lease transition
Owner claim
Nudge snapshot
Fallback state machine
SubsessionActor
SessionDispatcher
EventStore lock
Stage transition
Generation
```

对应用户行为必须已有新行为测试，否则不得删除。

---

## 9. 最终搜索门禁

生产代码中必须搜索不到：

```text
Nudge
NudgeLease
NudgeDispatchClaim
SessionOwner
LeaseIdentity
Generation
ContinuationStage
FallbackPhase
SubsessionActor
SessionDispatcher
FallbackCoordinator
EventStoreRuntime
workspace lockfile
proper-lockfile
.wanxiangshu.ndjson
Hosts/Mux
Hosts/Omp
PluginMimo
PluginMimoTui
```

仅允许在：

```text
迁移历史文档
明确的 forbidden-symbol 架构测试
release notes
```

中出现。

同时：

```text
git grep "src/Runtime"
git grep "src/Hosts"
git grep "ProjectReference.*next"
git grep "build/next"
git grep "tests-next"
git grep "wanxiangshu-next"
```

必须为零。

---

# 第八部分：正名与防止再次臃肿

## P19：将 next 变成唯一正式代码

重命名：

```text
next/       → src/
tests-next/ → tests/
build/next  → build/src
```

删除：

```text
next/package.json
Wanxiangshu.Next 命名
临时双构建脚本
临时双轨 CI
```

Namespace 统一：

```text
Wanxiangshu
```

而不是永久保留：

```text
Wanxiangshu.Next
```

运行目录统一：

```text
.wanxiangshu/
```

---

## 10. 永久架构门禁

### 依赖方向

```text
Kernel
↑
Journal / Process
↑
Session / Tools
↑
OpenCode / Wanxiangzhen
```

Kernel 不引用：

```text
OpenCode
Node obj
Tools
Wanxiangzhen
```

Session 不引用原始宿主 `obj`。

### 文件边界

文件按语义拆分，不按版本号或修补批次：

禁止：

```text
V39.fs
NewDriver2.fs
HelpersMisc.fs
Manager.fs
Coordinator.fs
Common2.fs
LegacyCompat.fs
```

行数门禁只能作为代码味道提示，不能为了满足 300 行而把一个自然模块机械拆成五个转发壳。

### 控制复杂度

禁止新增：

```text
第二套事件总线
第二套状态投影
第二套 Tool 注册
第二套 Prompt 发送
第二套 Session owner
跨进程实时 watcher
```

### 测试同步

每次新增用户行为必须同时：

```text
更新 behavior ledger
增加对应测试
更新 README
```

否则 CI 失败。

---

# 第九部分：完整提交列车

工程师可以按以下提交序列实施，但不得在中间宣布“重写完成”。

```text
B00  修复当前编译、清理 .omx、建立 next CI
B01  建立 237 项行为总账与正式产品契约
B02  Journal writeAll、原子创建重试、失败清理
B03  Journal Reader/Frontier/归并/Fold 完成
B04  合并 SessionRuntime 所有权，删除 SessionDrivers
B05  OpenCode Codec 与 MessageOrigin 完成
B06  PromptKey/PromptProtocol/early terminal/reconcile 完成
B07  Driver Human→initial terminal→Flow 生命周期完成
B08  Todo 正式 Schema、CommandPort、真实 E2E 完成
B09  静态 Tool Catalog、权限、路径与输出边界完成
B10  Executor/Process 不遗留、不 hang 完成
B11  PTY 全生命周期完成
B12  Child create/continue/parallel/restart 完成
B13  Review 完整闭环完成
B14  Fallback 完整闭环完成
B15  Transform/Config/Compaction/Context Budget 完成
B16  Fuzzy/Glob/Web/Question/Methodology 完成
B17  万象阵 DAG/worktree/slave/verify/FF/restart 完成
B18  237 行为 next 覆盖、故障与稳定性完成
B19  根构建、npm exports、tarball 切换
B20  删除旧 src/tests/integration/e2e/dependencies
B21  next→src、tests-next→tests 正名
B22  forbidden-symbol、依赖方向、防回流门禁
```

---

# 第十部分：最终完成定义

工程师只有同时提供以下证据，才能说“完成了，可以删除旧实现”：

## 构建证据

```text
clean checkout build green
root package 只编译新 src
npm pack 安装成功
真实 OpenCode 加载成功
```

## 行为证据

```text
237 项行为全部 Keep/Replace/Delete 有结论
所有 Keep/Replace = Proven
Delete 已从 README、exports、types、tests 中移除
```

## 正确性证据

```text
Journal fault tests green
Prompt race/restart tests green
Human抢占 tests green
Todo/Review/Fallback/Child tests green
Process/PTY无遗留 tests green
万象阵重启与FF tests green
```

## 稳定性证据

```text
100次 init/dispose
多Session并行
连续Human turn
kill -9恢复
无固定sleep关键断言
无额外LLM请求
无PID、端口、timer、waiter泄漏
```

## 删除证据

```text
无旧 src/
无 tests-next/
无 build/next/
无 Mux/OMP exports
无 legacy data reader
无旧架构符号
无旧生产依赖
```

## 最终代码阅读证据

一名不了解旧项目的工程师，应能从下面五条主路径直接读懂系统：

```text
Session Script
Prompt Protocol
Process Script
Child Script
Squad Script
```

不需要先理解旧 Nudge、Lease、Stage、Actor、Dispatcher 或 EventStore 塔。

这才叫重写完成，而不是“next 目录里已经有同名文件”。
