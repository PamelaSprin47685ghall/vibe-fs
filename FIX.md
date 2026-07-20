# 判断

这是 **P0 级的数据安全 + 系统活性故障**。

最符合“NDJSON 一坏，某些操作永远挂起；删除后恢复”的真实链路是：

```text
NDJSON 某行损坏或被截断
  ↓
读取器在第一条坏行处静默停止，但不修文件
  ↓
坏行之后的 RunFinished / Settled 等终态事件全部不可见
  ↓
启动恢复误判某些 subsession 仍是 ActiveRun
  ↓
reconcileUnfinishedRuns 尝试关闭“僵尸 Session”
  ↓
ClosePhysicalSession / eventStore.Append / actor queue 某个 Promise 不返回
  ↓
RuntimeScope.initPromise 永久 pending
  ↓
工具、hook、subsession、事件追加共同等待初始化或串行队列
  ↓
整个工作区表现为 hang forever
```

删除 NDJSON 后，错误的 `ActiveRun` 投影消失，启动恢复不再进入那条无界关闭路径，所以系统恢复。

这不是单一“坏 JSON”问题，而是三项设计缺陷相乘：

> **坏日志不自愈 + 恢复过程等待外部副作用 + 队列允许一个 Promise 永久劫持所有后续操作。**

---

# 一、第一根因：当前只是“内存截尾”，不是“物理截尾”

当前 `readEventsFromText` 的行为是：

```fsharp
遇到第一条非空且无法解析的行
    → stop <- true
    → 返回此前成功解析的事件
```

它不会：

* 报告坏行位置；
* 报告损坏原因；
* 截断物理文件；
* 隔离损坏尾部；
* 校验后续事件；
* 追加修复记录。

也就是说，当前实现是：

```text
在内存里假装文件到此结束
```

而不是：

```text
把文件原子修复到最后一个完整事件边界
```



这会产生一个永久的“因果断崖”。

例如原文件为：

```text
RunStarted
TurnStarted
{ "V": 1, "Session": ...       ← 崩溃留下的半行
RunFinished                     ← 后续合法事件
HumanTurnStarted
```

每次重启只能看到：

```text
RunStarted
TurnStarted
```

`RunFinished` 永远不可见。

更危险的是，应用仍会继续向坏文件末尾 append。坏行如果没有换行，新事件会直接接在半截 JSON 后面：

```text
{"V":1,"Sessio{"V":1,"Session":"...","Kind":"run_finished"...}
```

于是新事件也永久不可重放。

---

# 二、第二根因：缓存把“物理 EOF”误当成“有效日志 EOF”

初始化读取完成后，当前代码会执行：

```fsharp
lastKnownSize <- physicalFileSize
lastReadByteOffset <- lastKnownSize
partialLineBuffer <- ""
```

问题是，解析器可能只接受了文件前半部分，但 offset 却直接推进到**整个物理文件末尾**。

于是出现两个真相：

```text
内存投影：坏行前的事件 + 本进程后来追加的部分事件
磁盘重放：永远只到第一条坏行
```

当前进程甚至可能看起来正常；一旦重启，所有坏行之后的事件再次消失。

增量同步也有同样的问题：

```fsharp
ProcessLines completeLines
partialLineBuffer <- ...
lastReadByteOffset <- physicalSize
```

`ProcessLines` 遇到坏行只是 `stop <- true`，但调用方仍然把整块数据标记为已消费。坏行后同一个 chunk 中的合法事件被跳过，却再也不会重新读取。

因此必须建立强不变式：

> **LastReadByteOffset 永远只能等于最后一个已验证事件边界，不能等于未经验证的物理文件大小。**

---

# 三、第三根因：损坏日志会制造“幽灵 ActiveRun”

启动时：

```text
SubsessionReconcile.reconcileUnfinishedRuns
    → store.ReadAllEvents()
    → projectFromWanEvents
    → 找 RunStarted 但没有 RunFinished
    → 认定为 ActiveRun
```

然后对每个 ActiveRun 顺序执行：

```fsharp
let! stopStatus = host.ClosePhysicalSession sessionId
...
do! eventStore.Append(...)
...
do! actor.MarkUnknownAfterRestart()
```

这些调用没有局部 deadline。

只要一个物理 Session 的 delete、close、dispatcher queue 或 NDJSON append 不收敛，整个 reconcile 就停在那里，后面的 Session 以及插件初始化全部无法完成。

这也解释了为什么问题和 NDJSON 强相关：

* 坏行可能把真实的 `RunFinished` 隐藏；
* 恢复层误认为旧 Session 仍运行；
* 开始调用宿主清理 API；
* 宿主清理 Promise 没有上界；
* 初始化永久 pending。

---

# 四、第四根因：一个 pending Promise 可以劫持四层队列

## 1. 文件级 `fileQueues`

`withWorkspaceLock` 会先等待同一文件的前一个 Promise：

```fsharp
let! _ = prev
```

它只处理：

* 前一个 Promise 成功；
* 前一个 Promise reject。

但如果前一个 Promise **永远 pending**，后续所有文件操作永远等不到。

下面这些环节全部没有硬截止时间：

```text
ensureFileExists
acquireFileLock
action
release
```



所以一个挂住的：

* `readFile`;
* `stat`;
* `proper-lockfile.lock`;
* `appendFile`;
* `release`;
* action 内宿主调用；

就可以劫持整个工作区的 NDJSON。

---

## 2. `EventLogStore.SerialQueue`

所有 append、批量 append、nudge claim 共用一个 `SerialQueue`。

队列核心是：

```fsharp
do! task()
do! processQueue()
```

只要 `task()` 不 settle，队列不会处理下一项。

reject 不会永久毒死队列；**pending 才会**。

现有 `EventLogRuntimeRobustnessTests` 主要模拟的是：

```fsharp
Promise.reject (exn "mock disk full")
```

这只能证明“明确失败能传播”，没有覆盖：

```fsharp
Promise.create(fun _ _ -> ()) // 永远 pending
```

所以测试恰好漏掉了真正导致 hang forever 的类别。

---

## 3. `RuntimeScope.initPromise`

初始化 Promise 只创建一次：

```fsharp
if Option.isNone initPromise then
    initPromise <- Some(f workspaceRoot)
```

后续所有调用都等待同一个 Promise。

它没有：

* 初始化 deadline；
* pending watchdog；
* 失败后重置；
* degraded 状态；
* 重试状态机；
* 强制 settle。

一旦初始化 Promise pending，插件实例生命周期内就永久 pending。

---

## 4. `SessionReaderWriterLock`

这里还存在一个独立的确定性死锁。

读锁执行：

```fsharp
activeReaders <- activeReaders + 1
let p = work ()
```

如果 `work()` **同步抛异常**，不会进入后面的 Promise catch，`releaseReader()` 永远不执行。

于是：

```text
activeReaders = 1
所有 writer → awaitNoReaders()
永远等待
```

如果 `work()` 返回一个永远 pending 的 Promise，也有同样结果。

这意味着即使 NDJSON 修复了，某些同步异常或未收敛读取仍然可以永久封死 Session writer。

---

# 五、正确目标：建立四条不可破坏的不变式

## 不变式 A：Valid Prefix

任意时刻，持久化文件只能是：

```text
零个或多个完整、可解码的 NDJSON 行
并且文件以换行符结尾
```

崩溃可以留下半行，但**下一次打开文件时必须先修复，再做任何读取或追加**。

---

## 不变式 B：Projection Equivalence

任意公开操作完成后：

```text
当前内存 Projection
=
从当前物理 NDJSON 文件头开始完整重放得到的 Projection
```

当前实现已经违反它：运行中缓存可能包含重启后无法重放的事件。

---

## 不变式 C：Bounded Completion

每个公开 Promise 必须在明确期限内进入：

```text
Succeeded
Failed
TimedOut
Cancelled
Degraded
```

不得存在“继续等待”的第六种结果。

---

## 不变式 D：No Queue Hostage

一个 queue item 不得无限阻止后续 queue item。

超时后只能二选一：

```text
安全取消并继续队列
```

或者：

```text
把队列标为 Poisoned，后续操作立即失败
```

绝不能继续 pending。

---

# 六、NDJSON 自动原子截尾协议

## 1. 新增纯扫描器

不要让 `readEventsFromText` 同时承担解析、容错和恢复决策。

新增纯函数，例如：

```text
scanEventLog(buffer) -> ScanResult
```

返回：

```text
Clean
  validEndOffset
  events

ValidFinalLineMissingNewline
  validEndOffset
  events

CorruptTail
  validEndOffset
  badOffset
  badLineNumber
  reason
  removedBytes

CorruptMiddle
  validEndOffset
  badOffset
  badLineNumber
  reason
  removedBytes
```

`validEndOffset` 必须是**字节偏移**，不能是 .NET/JS string 字符数。日志中可能包含中文，UTF-8 字节数和字符串长度不同。

扫描规则：

1. 按 `\n` 的字节位置切分；
2. 空行可为兼容性而接受；
3. 每个非空完整行必须能解码为 `WanEvent`；
4. 最后一行没有换行但 JSON 完整：保留并补换行；
5. 最后一行不完整：截到上一条完整行结束；
6. 中间任何一行损坏：从该行开始截掉整个后缀；
7. 未知 `Kind` 不属于物理损坏，应保留；
8. payload 领域语义错误应由各投影 poison 对应 Session，不应截掉整个全局日志。

---

## 2. 使用稳定的专用锁文件

项目已经定义了：

```text
.wanxiangshu.ndjson.lock
```

但当前实际锁的是数据文件路径。

应改成：

```text
锁定稳定的 .wanxiangshu.ndjson.lock
操作 .wanxiangshu.ndjson
```

原因是数据文件将来可能：

* 截断；
* 替换；
* 轮转；
* 原子 rename；
* 被外部删除。

锁对象本身必须保持稳定。

---

## 3. 修复操作顺序

在专用排他锁内：

```text
1. open NDJSON 为 r+
2. 从同一 file descriptor 扫描
3. 得到 validEndOffset
4. 如有损坏，先保存损坏尾部用于取证
5. fd.truncate(validEndOffset)
6. fd.sync / fdatasync
7. 如最后一条是完整 JSON 但缺换行，则补 "\n"
8. append event_log_repaired 事件
9. 再次 fd.sync
10. 关闭 fd
11. 释放锁
12. 从修复后的文件头重新构建 Projection
```

使用 `ftruncate` 比“读前缀 → 临时文件 → rename”更适合单纯截尾：

* 文件长度切换是单一元数据操作；
* 不会让持有旧 inode 的调用继续写旧文件；
* 配合 `fsync` 可以形成恢复边界。

损坏尾部可写入：

```text
.wanxiangshu-recovery/
  ndjson-tail-<timestamp>-<hash>.bin
```

权限限制为当前用户，最多保留固定数量，例如 3 份，避免无限增长。

---

## 4. 修复记录

修复后追加一条工作区级事件：

```json
{
  "kind": "event_log_repaired",
  "payload": {
    "badOffset": "...",
    "removedBytes": "...",
    "badLine": "...",
    "reason": "...",
    "tailHash": "...",
    "repairVersion": "1"
  }
}
```

它必须在截尾完成后、同一个锁作用域内追加。

这样不能再出现：

```text
系统悄悄丢掉尾部，但没有任何证据
```

---

# 七、`EventStoreState` 必须重写初始化状态机

当前：

```text
initDone: bool
readCalled: bool
initPromise: Promise option
```

组合出了非法状态：

```text
readCalled = true
initDone = false
initPromise = pending/rejected
```

应替换为显式状态：

```text
Uninitialized
Initializing(operationId, deadline)
Ready(revision, validEndOffset)
Repairing(operationId, fault)
Degraded(error)
Disposed
```

规则：

* 只有 `Ready` 才允许正常读取和追加；
* `Initializing` 超时后必须进入 `Degraded`；
* 不缓存一个永不失效的 pending Promise；
* 初始化失败后可以重新开始新的 operationId；
* 旧初始化晚到的结果通过 operationId fence 丢弃；
* `readCalled` 应删除；
* 修复后必须从头 rebuild cache；
* `LastReadByteOffset` 只能取 scanner 返回的 `validEndOffset`；
* 不再把未验证的物理 EOF 当成消费位置。

---

# 八、增量读取不允许再“遇坏行就 stop”

`ProcessLines` 当前的：

```fsharp
| None -> stop <- true
```

必须删除。

增量同步遇到坏行时应：

```text
返回 CorruptionDetected(badAbsoluteOffset)
    ↓
在当前工作区锁内执行 RepairAndReplay
    ↓
截尾
    ↓
重建整个 Projection
```

因为 corruption 是极少发生的异常路径，允许此时 O(N) 重放。

不要试图在坏行后继续解析，也不要保留无限期 `partialLineBuffer`。

当前所有读取都应在写锁稳定窗口内进行。此时 EOF 的非空半行不是“稍后可能完整”，而是上一进程留下的 torn write，应立即修复。

---

# 九、恢复流程必须“逐 Session 有界，整体有界”

`reconcileUnfinishedRuns` 不应让一个 Session 劫持插件启动。

推荐语义：

```text
对每个 ActiveRun：
    ClosePhysicalSession，最多 5 秒
    append poison events，最多 10 秒
    MarkUnknownAfterRestart，最多 2 秒
    无论成功、失败还是超时，进入下一 Session
```

并设置全局初始化预算，例如 30～60 秒。

超时处理：

```text
ClosePhysicalSession timeout
    → stopStatus = StopUnknown
    → 内存标记 Poisoned
    → 尝试持久化 recovery_timeout
    → 继续恢复其他 Session
```

重要的是：

> 清理旧 Session 失败，可以让该 Session 不可用；不能让整个 OpenCode 永远不可用。

OpenCode、OMP 的：

* session delete；
* dispose；
* close；
* sessionStatus；
* messages；
* abort；

全部需要 deadline。现在若宿主 API 返回永不 settle 的 Promise，外层 `try/with` 没有任何作用，因为 pending 既不是成功也不是异常。

---

# 十、队列不能只简单套 `Promise.race`

这里要特别防止错误修复。

不能这样写：

```text
Promise.race(work, timeout)
timeout 后直接处理下一项
```

因为原来的 work 可能稍后继续完成并产生“幽灵写入”。

例如：

```text
append 超时
调用方认为失败
队列继续 append B
旧 append A 又在后台完成
磁盘顺序变成 B → A
```

正确方案分两类。

## 可取消操作

传入 `AbortSignal`：

```text
deadline 到达
    → abort
    → 等待取消确认
    → fence 旧 operationId
    → 队列继续
```

## 不可取消写操作

只能：

```text
deadline 到达
    → 当前 EventLogStore 标记 Poisoned
    → 所有后续写立即失败
    → 杀掉/隔离执行该 I/O 的 worker
    → 创建新 store generation
    → 自动 repair + replay
```

如果要求严格意义上的“任何时候都不能 hang”，NDJSON I/O 最终应放入**可终止的 worker thread 或 child process**。仅在同一个 Node 进程里给 Promise 加 timeout，不能保证底层文件系统调用不会晚到并修改文件。

这是物理事实，不应假装一个 `Promise.race` 就能解决。

---

# 十一、修复 `SerialQueue`

建议队列项携带：

```text
QueueItem {
    operationId
    name
    deadlineAt
    abortController
    fenceGeneration
    work
}
```

每项必须结束为：

```text
Completed
Failed
TimedOutAndCancelled
TimedOutAndQueuePoisoned
```

队列自身不能等待原始 work，而应等待 supervisor 的**必收敛结果 Promise**。

同时暴露诊断：

```text
queue_wait_ms
queue_run_ms
operation
operation_id
workspace
session
queue_depth
terminal
```

超时后应把调用方 Promise reject 为明确错误，而不是继续转圈：

```text
EventStoreTimeout
WorkspaceLockTimeout
InitializationTimeout
HostCleanupTimeout
QueuePoisoned
```

---

# 十二、修复 `SessionReaderWriterLock`

读锁必须使用真正的 `try/finally` 语义：

```text
activeReaders++
try:
    bounded work
finally:
    activeReaders--
    resolve reader-zero waiters
```

必须覆盖：

* `work()` 同步抛异常；
* work Promise reject；
* work Promise timeout；
* abort；
* dispose。

还要增加断言：

```text
activeReaders >= 0
```

Session 被关闭或锁被 poison 时，`readerZeroPromises` 必须全部 reject/resolve 为终止结果，不能遗留永不触发的 resolver。

---

# 十三、初始化应该“失败关闭，但保持响应”

现在插件入口直接：

```fsharp
TriggerInit
do! WaitInit()
```

这等于把整个插件注册交给 NDJSON 恢复做人质。

更稳妥的行为是：

```text
初始化成功
    → Ready

初始化失败或超时
    → 插件仍完成注册
    → EventStore 状态为 Degraded
    → 依赖 durable state 的操作立即返回明确错误
    → 不依赖 NDJSON 的基础操作仍可用
```

例如：

```text
万象术事件存储暂不可用：
已在 30 秒内终止恢复，而不是继续等待。
原因：HostCleanupTimeout
工作区：...
Session：...
自动修复状态：tail truncated / retryable
```

这叫：

> **fail closed, remain responsive**

而不是：

> **fail closed by hanging the entire program**

---

# 十四、不要再吞恢复错误

当前多处：

```fsharp
with _ ->
    ()
```

需要区分：

```text
Rejected
TimedOut
CorruptTailRepaired
EventStoreUnavailable
HostUnavailable
QueuePoisoned
```

`EventLogRuntimeSync` 可以不阻止插件启动，但必须返回一个聚合结果：

```text
Ready
ReadyAfterRepair
Degraded(errors)
```

pending 永远不是合法结果。

---

# 十五、建议的文件级修改清单

## P0：先根治

### `src/Runtime/EventStore/EventLogCodec.fs`

* 删除静默 `stop` 解析接口；
* 增加 byte-offset scanner；
* 返回结构化 `ScanResult`；
* 支持完整末行缺换行；
* 区分 envelope 损坏与领域 payload 损坏。

### `src/Runtime/EventStore/EventLogIoRaw.fs`

* 改用稳定 `.wanxiangshu.ndjson.lock`；
* 增加 open/read/truncate/sync；
* 加锁、action、release 都必须有 deadline；
* 文件队列必须在所有终态下清理；
* pending 超时后 poison 当前 generation。

### `src/Runtime/EventStore/EventStoreIo.fs`

* 初始化先 repair，再 replay；
* 删除 `readCalled`；
* 使用显式初始化状态机；
* offset 只取 valid prefix；
* 遇增量损坏立即 RepairAndReplay；
* 删除永久 partial-tail 语义。

### `src/Runtime/EventStore/EventStore.fs`

* 所有公开方法保证有界收敛；
* append timeout 不更新 cache；
* append 结果不确定时 poison store；
* 修复后 cache 全量重建；
* 加入投影等价校验的测试 seam。

### `src/Runtime/Subsession/SubsessionReconcile.fs`

* 每个宿主操作有 deadline；
* 每个 Session 独立失败；
* 全局恢复有 deadline；
* 一个 zombie 不能阻塞全部恢复。

### `src/Runtime/Workspace/RuntimeScope.fs`

* `initPromise option` 改为 InitState；
* pending 超时必须 settle；
* 失败后不得永久缓存；
* 插件进入明确 degraded 状态。

### `src/Runtime/Subsession/PromiseQueue.fs`

* 新增 supervised bounded queue；
* queue item 超时后取消或 poison；
* 不能直接等待原始 Promise。

### `src/Runtime/Workspace/SessionReaderWriterLock.fs`

* 同步异常也必须释放 reader；
* pending reader 有 deadline；
* dispose/poison 时释放所有 waiter。

---

## P1：让“不确定写入”也安全

当前 `WanEvent` 没有全局 EventId。

建议新事件增加：

```text
EventId
WriterId
Sequence
Checksum
```

作用：

* timeout 后重试可按 EventId 去重；
* 检测 late completion 形成的重复写；
* 检测行内容被静默篡改；
* 检测顺序异常；
* 更可靠地确认 append 是否实际完成。

旧 V1 事件继续兼容读取，新写入使用 V2。

---

# 十六、必须先写的测试

## A. 原子截尾

1. 合法文件，不修改。
2. 空文件，不修改。
3. 完整末行但缺换行，只补换行。
4. 半截 JSON 尾部，截到上一完整行。
5. 中间坏行、后面还有合法行，截掉坏行及整个后缀。
6. 中文、emoji 等多字节内容，字节 offset 正确。
7. CRLF 兼容。
8. 空白行兼容。
9. 损坏尾部被隔离保存。
10. 修复事件正确追加。
11. 修复后 append，再重启，事件仍存在。
12. 连续启动两次，第二次不得重复修复。

## B. Projection Equivalence

对任意故障注入序列，断言：

```text
liveCache
=
fold(readAndRepair(file))
```

这是最重要的属性测试。

---

## C. Hang 注入

每个位置注入永不 settle 的 Promise：

```text
stat
readFile
lock acquire
lock action
lock release
append
fsync
ClosePhysicalSession
session.delete
actor.MarkUnknownAfterRestart
RuntimeScope.OnInit
SessionReaderWriterLock read
```

断言：

1. 对外调用在 deadline 内返回错误；
2. 后续调用要么继续成功，要么立即收到 QueuePoisoned；
3. 绝不 pending；
4. 不能需要用户删除 NDJSON。

---

## D. 真实崩溃 E2E

不要只在内存里伪造坏字符串。

应：

1. 启动真实子进程；
2. 写 NDJSON 到一半时 `SIGKILL`；
3. 保留半行；
4. 重启插件；
5. 验证自动截尾；
6. 验证插件在限定时间内完成初始化；
7. 验证可继续 append；
8. 再次重启；
9. 验证新事件完整重放。

还要测试：

```text
RunStarted 完整写入
RunFinished 写到一半时被杀
```

这正是最容易触发幽灵 ActiveRun 的场景。

---

# 十七、验收红线

修复完成必须同时满足：

1. **任何 malformed/torn NDJSON 都自动截到第一条坏行之前。**
2. **修复后文件必定以完整换行结束。**
3. **坏行之后的内容不会继续参与重放。**
4. **损坏尾部有取证副本和 repair 诊断。**
5. **运行中投影与重启重放投影完全相等。**
6. **所有初始化、锁、队列、宿主清理和事件追加都有 deadline。**
7. **任何单个 Session 的恢复失败不能阻塞其他 Session。**
8. **任何 pending Promise 都不能阻止插件完成注册。**
9. **超时写入不能以 late completion 偷偷修改新 generation。**
10. **不再需要删除 `.wanxiangshu.ndjson` 才能恢复。**

---

## 最终定性

这里不能只补一个：

```text
遇到坏 JSON 就忽略
```

也不能只给 `ClosePhysicalSession` 套个 timeout。

真正需要一起根治的是：

```text
物理日志自动修复
+ 投影与文件强一致
+ 恢复副作用有界
+ 队列不可被劫持
+ 初始化可降级但必响应
+ 不确定写入有 fence
```

当前代码库已经具备事件溯源、Session poison 和 deadline 状态机的部分基础，但 NDJSON 基础设施自身仍然是无界 Promise 链；只要底层事实存储能卡住，所有上层状态机的严谨性都会失效。完整实现与测试位置均可在本次代码包中定位。

---
对。上一版中“自己维护 `.wanxiangshu.ndjson.lock`”的建议应撤回。**跨进程互斥必须完全交给 `proper-lockfile`，不得手写锁文件、不得自行判断 stale、不得自行删除锁目录。**

而且当前代码已经在使用：

```fsharp
[<Import("lock", "proper-lockfile")>]
let private lockfileLock ...
```

并通过 `proper-lockfile.lock(filePath, options)` 锁定 NDJSON 文件。

正确方向是**修好现有封装**，而不是另造锁协议。

## 修正后的锁模型

分成两层，但只有一层是文件锁：

```text
进程内 fileQueues
    只负责同一进程 FIFO 串行化
    不承担跨进程安全
        ↓
proper-lockfile
    唯一的跨进程锁实现
        ↓
在锁内完成 scan → truncate → fsync → append
```

`fileQueues` 可以保留，因为它不是“徒手文件锁”，只是避免同一 Node 进程内大量竞争 `proper-lockfile`。但它必须有界，不能因为前一个 Promise pending 而永久堵死。

---

## 1. 继续锁定实际 NDJSON 路径

使用：

```fsharp
properLockfile.lock(eventLogPath, options)
```

不要自行创建：

```text
.wanxiangshu.ndjson.lock
```

`proper-lockfile` 本身会管理其锁目录、更新时间、stale 判定和所有权检查。业务代码不应依赖它内部生成的锁路径。

由于修复采用**原文件 `truncate`**，而不是 rename-replace，所以可以安全地继续锁定：

```text
.wanxiangshu.ndjson
```

完整临界区：

```text
proper-lockfile.lock(.wanxiangshu.ndjson)
    ↓
同一个锁内：
    读取
    扫描 valid prefix
    ftruncate
    fsync
    补换行
    追加 repair 事件
    fsync
    重建必要状态
    ↓
release()
```

不要在释放锁后再截尾，也不要 scan 和 truncate 分成两次加锁。

---

## 2. `ensureFileExists` 仍然必要

`proper-lockfile` 默认会解析目标真实路径，因此首次锁定前需要保证文件存在。当前：

```fsharp
do! ensureFileExists filePath
let! release = acquireFileLock filePath
```

总体顺序正确。

但 `ensureFileExists` 和 `lock()` 之间仍有跨进程删除文件的竞态。建议：

* 万象术自身永远不删除 NDJSON；
* 清空只能在锁内 `truncate(0)`；
* 自动修复只做原地截尾；
* 外部删除导致 `ENOENT` 时，重新执行一次 `ensureFileExists → lock`；
* 重试次数有限，失败显式返回。

不要通过 `realpath: false` 掩盖文件生命周期问题，除非经过专门测试确认需要。

---

## 3. proper-lockfile 参数应明确化

当前配置：

```fsharp
stale = 15000
retries = {
    retries = 100
    factor = 1
    minTimeout = 50
    maxTimeout = 100
}
```

意味着锁竞争可能等待接近十秒，但代码没有向上层表达“正在等锁”还是“已经挂死”。

建议将配置集中为一个 SSOT：

```text
stale: 15s
update: 5s
retries: 有界
factor: 1
minTimeout: 50ms
maxTimeout: 100ms
```

关键约束：

```text
update < stale / 2
```

例如 `update=5000ms, stale=15000ms`。

不要把 stale 调得极短。事件日志截尾、fsync 和大文件 replay 在慢盘上可能超过几秒，过短会让另一个进程错误地把活锁视为死锁。

---

## 4. “不能 hang”不能靠绕过 proper-lockfile

应在 `proper-lockfile` 外建立监督期限，但不能手动抢锁：

```text
acquire deadline 到达
    → 当前调用返回 LockAcquireTimeout
    → 不删除 proper-lockfile 的锁目录
    → 不执行临界区
```

特别禁止：

```text
超时
→ rm -rf *.lock
→ 强行进入临界区
```

因为原持锁进程可能仍在正常写入，这会直接造成双写和日志损坏。

stale 回收只能让 `proper-lockfile` 自己完成。

---

## 5. 当前真正危险的是 `release()` 可以永久挂住

现有实现：

```fsharp
try
    do! release ()
with _ ->
    ()
```

只处理 reject，不处理 pending。

因此以下情况会永久占住 `next`：

```text
action 已完成
release Promise 永远 pending
    ↓
next 永远 pending
    ↓
fileQueues[filePath] 永远指向 next
    ↓
后续全部等待 prev
```

修复要求：

```text
release 成功
    → 正常完成

release 明确失败
    → 标记 LockReleaseFailed
    → 当前 EventStore generation 进入 Degraded

release 超时
    → 标记 LockReleaseIndeterminate
    → 不手动删除锁
    → 当前 store 禁止继续写
    → 后续请求立即失败，而不是等待
```

release 结果不确定时，不能假装锁已经释放并继续下一次写入。

---

## 6. `fileQueues` 不能等待永久 pending 的 `prev`

当前代码：

```fsharp
let! _ = prev
```

如果前一个操作永远 pending，后续永远 pending。

正确处理不是删除 `fileQueues`，而是让其中保存的 Promise 必须由 supervisor 保证收敛：

```text
fileQueues[path] =
    Promise<Completed | Failed | TimedOut | Poisoned>
```

队列项超时后：

* 若尚未取得 proper-lockfile：安全失败并推进队列；
* 若已取得锁但 action 状态不确定：poison 当前 EventStore；
* 若 release 状态不确定：poison，不推进新的磁盘写；
* 后续调用立即返回 `EventStorePoisoned`，不得等待。

这仍然使用 `proper-lockfile`，只是防止进程内 Promise 队列被劫持。

---

## 7. 必须用真正的 finally 释放

当前手写流程存在多个出口，建议统一成等价于：

```fsharp
let! release = acquireProperLock filePath

try
    return! boundedAction ()
finally
    do! boundedRelease release
```

Fable promise computation 中若不能直接安全表达异步 finally，就写一个唯一的 `runLocked` 边界，确保覆盖：

* `action()` 同步抛异常；
* action Promise reject；
* action timeout；
* decode exception；
* truncate exception；
* fsync exception；
* append exception。

不能让每个调用方自行管理 release。

---

## 8. 自动截尾协议不变，但必须完整置于 proper-lockfile 内

修正版流程：

```text
ensure NDJSON exists
    ↓
proper-lockfile.lock(ndjsonPath)
    ↓
读取同一个路径
    ↓
按 UTF-8 字节扫描最后有效边界
    ↓
发现 torn/corrupt tail
    ↓
保存取证尾部（可选）
    ↓
对原文件 ftruncate(validEndOffset)
    ↓
fsync
    ↓
必要时补换行
    ↓
追加 event_log_repaired
    ↓
fsync
    ↓
release proper-lockfile
```

不使用：

* 自建 `.lock`；
* `open(..., "wx")` 作为锁；
* PID 文件；
* `flock` 与 proper-lockfile 混搭；
* 超时后删除 lock；
* rename 一个新的 NDJSON 覆盖原文件。

---

## 9. 需要特别测试 proper-lockfile 的真实语义

必须增加多进程 E2E，而不只是 mock Promise：

1. 进程 A 持有锁，进程 B 等待后成功获取。
2. 进程 A 持锁时 `SIGKILL`，超过 stale 后 B 自动恢复。
3. A 持锁期间持续 update，B 不得误判 stale。
4. A 在 truncate 前被杀，B 自动检查并修复。
5. A 在 truncate 后、repair event 前被杀，B 能再次启动并完成恢复。
6. release reject 时不得手工删除锁。
7. acquire 超时后不得进入临界区。
8. 同一进程 100 个并发 append 保持 FIFO。
9. 一个队列项永不 settle，其他调用在 deadline 内得到明确错误。
10. 外部删除 NDJSON 后，系统有限重建并继续，而不是永久等待。

---

## 最终修正版原则

```text
proper-lockfile 是唯一跨进程锁
fileQueues 只是进程内排队
截尾使用原文件 ftruncate
完整修复事务都在 proper-lockfile 临界区内
任何 acquire/action/release 都必须有界
超时不抢锁、不删锁，只失败或 poison
```

因此，应修改我上一版中的“稳定专用锁文件”部分为：

> **不要自行建立或管理锁文件。继续通过 `proper-lockfile` 锁定 `.wanxiangshu.ndjson`；自动截尾采用锁内原地 `ftruncate + fsync`。重点修复现有 `withWorkspaceLock` 的无界 `prev`、无界 action、无界 release 以及异常后的队列清理。**
