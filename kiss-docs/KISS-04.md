# Journal 设计

承 KISS-01。本卷定义持久化：**Per-Runtime 单写 NDJSON + Lifetime Snapshot Isolation + 重启按物理时间归并**。

旧 NDJSON 不兼容；物理隔离路径；正式版前可删档。

命名：

> **Per-Runtime Journal · Lifetime Snapshot Isolation · Chronological Replay**

---

## 一、核心语义 [NORMATIVE]

```
每个 OpenCode Runtime 拥有一份永不被别人写入的进程日志。
启动时读取所有日志的稳定前缀 → Fold 得 BootSnapshot。
运行期间只追加自己的事件，并 read-your-writes。
其他 Runtime 启动后的新增，本进程重启前假装看不见。
```

精确承诺：

|承诺|含义|
|---|---|
|Read Your Writes|本 Runtime append+flush 后立即进入本进程内存|
|Repeatable Foreign Snapshot|整个进程生命周期内，所见他源历史不变|
|Restart Reconciliation|新进程启动时重新枚举全部日志并归并|
|Chronological Last Effect|同一状态维度多次更新时，时间序较晚者如何生效由领域 Fold 定义|

**不是** eventual consistency：运行中的 Runtime **不会**自动 eventually 看见别人。  
**不是**跨进程实时一致性；**不**做 watcher / tail / owner 代写 / leader / IPC 投影同步。

目标对齐：

1. 旧进程退出 → 新进程读其完整合法前缀，接续写**新**日志。  
2. 旧进程仍活 → 新进程只拿启动时 byte-length 前沿内的 best-effort 完整行。  
3. 主动放弃实时更新 → 无底洞不存在。

---

## 二、目录与身份 [NORMATIVE]

```
.wanxiangshu-next/
  runtimes/
    <runtime-id>.ndjson
```

- `RuntimeId` = 每次进程启动生成的 UUID / UUIDv7 / ULID。  
- **禁止**用 PID 作身份（会复用）。PID 可写入诊断 Header，不作键。  
- 创建：`CreateNew` 排他；失败 = 碰撞（极稀）→ 换新 Id 重试。  
- 进程内**唯一** `FileStream` Writer + 进程内 append 队列串行化。  
- 其他进程：**只读**稳定前缀；**永不**修、截断、续写他人文件。  
- **无** workspace lockfile、**无** session lockfile、**无**跨进程 append 锁。

首条可选：

```
RuntimeStarted {| RuntimeId; ProcessId; StartedAt |}
```

---

## 三、Envelope [NORMATIVE]

```
type EventId = private EventId of string
type RuntimeId = private RuntimeId of string

type StreamId =
    | Workspace
    | Session of SessionId
    | Child of ChildId
    | Squad of SquadId
    | Process of ProcessId
// 不用绝对 path

type Envelope =
    { RuntimeId: RuntimeId
      LocalSeq: int64              // 本文件内权威顺序；从 1 递增
      CommittedAt: DateTimeOffset  // 本 Writer 保证单调（见 §3.1）
      EventId: EventId
      Stream: StreamId
      Epoch: int64 option          // Session 语义 epoch，可选
      Fact: Fact }
```

全局展示/启动归并比较键：

```
CommittedAt → RuntimeId → LocalSeq
```

### 3.1 单 Writer 内时间单调 [NORMATIVE]

系统钟可能同刻、回拨。每个 Writer：

```
CommittedAt =
    max(clock.UtcNow(), previousCommittedAt + 1 tick)
```

保证同 Runtime：`LocalSeq n` 的时间 < `LocalSeq n+1`。  
跨 Runtime：按公共系统时间交错；同刻用 `RuntimeId` 破平局。  
第一版不需要 HLC / Lamport / Vector Clock（同机 OpenCode 共享系统钟为默认假设）。

### 3.2 明确删除的机制

```
PreviousEventId / ExpectedHead / Forked
SessionAlreadyOwned / Owner Runtime / 代写
Session stream lock / workspace 生命周期锁
按 Session 分片日志的跨进程争用
```

同 Session 多 Runtime **合法**。不事后判定 fork，不按时间戳静默「冲突解决协议」——较晚事件如何覆盖由 **领域 Fold** 定义（§八）。

---

## 四、启动算法 [NORMATIVE]

### 4.1 枚举一次

```
paths = enumerate .wanxiangshu-next/runtimes/*.ndjson
```

只枚举一次。启动后新出现的文件不属于本次快照。

### 4.2 捕获 Frontier

```
type SourcePrefix =
    { RuntimeId: RuntimeId   // 从文件名或首条解析
      Path: string
      ByteLength: int64 }    // 捕获瞬间的长度
```

```
Frontier: Map<RuntimeId, int64>
```

### 4.3 读取固定前缀

对每个源：只读 `[0, ByteLength)`。

|情况|动作|
|---|---|
|完整合法行|接受|
|EOF/前缀末尾半行|忽略|
|前缀内首个非法行|**该源**停读；诊断 RuntimeId/offset/丢弃字节；**不**改文件|
|其他源|继续|

**单源**前缀 Fail Closed（停该源）；**全局**启动 Best Effort（他源仍用）。  
与「单文件权威 Journal 中间损坏全局拒启」不同：每文件是独立贡献源。

Reader **永不**修改 Writer 文件。

### 4.4 归并与 Fold

语义：按时间交错合并。

实现：稳定 k 路归并（最小堆 key = `CommittedAt, RuntimeId, LocalSeq`），**不**打乱单源 `LocalSeq` 顺序。  
禁止「读完全扔进大 list 再乱 sort 导致同源乱序」的错误实现；若内存 sort，比较键须稳定且同源序不变。

业务恢复推荐：

```
按 StreamId 分组 → 各 Stream 独立 Fold → 组成 RuntimeSnapshot
```

时间线交错更适合 diagnostics / 审计 / 跨 Stream 只读视图。  
**避免**一个巨大全局 `WorkspaceState` 复合 Fold 回潮。

### 4.5 创建自己的日志

```
CreateNew <new-runtime-id>.ndjson
可选 RuntimeStarted
之后只 append 本 Runtime
```

---

## 五、运行期 [NORMATIVE]

```
BootState = Fold(Merge(所有源在 Frontier 内的合法前缀))

CurrentState =
    Fold(BootState, 本 Runtime 启动后产生的事件)
```

- 他源在 Frontier 之后的 append：**本进程不看**。  
- 本进程：先盘后内存；写失败 = 事实未发生；Poisoned → 本进程拒写，须重启开新文件。  
- 进程内多 Session Driver → 同一 Journal append 队列 → 唯一 FileStream。  

```
type RuntimeSnapshot =
    { Frontier: Map<RuntimeId, int64>
      Projections: ProjectionSet   // 按 Stream 投影集合
      OwnRuntimeId: RuntimeId
      OwnLocalSeq: int64 }
```

---

## 六、Append 路径 [NORMATIVE]

```
// 已是本文件唯一 Writer；无跨进程锁
let appendAndFlush fact =
    journal {
        let localSeq = nextLocalSeq()
        let committedAt = nextMonotonicTime(clock)
        let envelope = { RuntimeId = me; LocalSeq = localSeq; CommittedAt = committedAt
                         EventId = ...; Stream = ...; Epoch = ...; Fact = fact }
        do! writeLine + flush
        return envelope
    }
```

- **不**在每次 append 上 Repair 他人文件。  
- **不**修自己以外的任何日志。  
- 本文件若本进程崩溃留下半行：下一位 Reader 忽略半行即可。

`commit`（Runtime/Driver）：

```
let! env = journal.AppendAndFlush(fact)
match projections.Apply(env) with
| Ok next -> publish next
| Error e -> Fail Closed 本 Runtime（盘已发生）
```

Journal 只负责盘；Fold 在 Runtime。

---

## 七、Driver / Prompt 局部化（交叉）

|旧全局表述|新|
|---|---|
|每个 Session 恰好一个 Driver|**每个 Runtime 内**每个 Session 0|1 Driver：`(RuntimeId, SessionId)`|
|Session 全局 PendingPrompt 互斥|**本 Runtime 内**该 Session 本地 PendingPrompt|
|跨 Runtime 实时去重 Prompt|不承担；各 Runtime 只保证本地 sendOnce|

同 Session 可同时存在：

```
Runtime A → Session X Driver
Runtime B → Session X Driver
```

不同启动快照、不同日志、不共享 mutable、不实时互等；下次启动统一归并。详见 KISS-Driver。

---

## 八、领域 Fold 与「陈旧视图上的合法操作」[NORMATIVE]

并发事实**全部保留**为历史。最终状态 = 时间线 Fold，不是存储层冲突协议。

推荐事件形态（可直接应用）：

```
TodoChanged(完整 snapshot)
ReviewApplied(verdict, resultingTodo option)
HumanTurnStarted(epoch)
PromptRequested / Submitted / Terminal …
ChildCompleted …
```

谨慎：依赖生成时旧快照的相对操作（`Remove index=3`、`IncrementRound`、`Toggle`）。

示例（Fold 定义，非存储冲突）：

```
TodoChanged A @ t1; TodoChanged B @ t2 → 最终 Todo = B 的 snapshot
Review NeedsChanges @ t1; Passed @ t2 → 最终 Passed
两侧 PromptRequested/Terminal 四条全保留
```

SessionSettled 后更晚事实：由 Session Fold 明文规则（重激活 / late historical / …），存储层不拒写。

---

## 九、嵌套 Fact（权威总和）

```
type Fact =
    | Runtime of RuntimeFact
    | Session of SessionFact
    | Todo of TodoFact
    | Prompt of PromptFact
    | Review of ReviewFact
    | Child of ChildFact
    | Process of ProcessFact
    | Squad of SquadFact

type RuntimeFact =
    | RuntimeStarted of {| RuntimeId: RuntimeId; ProcessId: int; StartedAt: DateTimeOffset |}

type SessionFact =
    | HumanTurnStarted of {| Epoch: int64 |}
    | SessionSettled of {| Result: SessionResult |}

type TodoFact =
    | TodoChanged of {| Snapshot: TodoSnapshot |}

type ReviewFact =
    | ReviewApplied of
        {| Verdict: ReviewVerdict
           Round: int
           ResultingTodo: TodoSnapshot option |}
```

PromptFact 见 KISS-Driver（Host MessageId）。  
Process：字段够清孤儿或第一版不写 durable（二选一）。

不应记：Phase/Stage/Lease/Owner/业务 Ordinal/Generation 等 PC。

---

## 十、与工作区文件 / Git 分层

日志一致性 ≠ 源码文件一致性。

|层|机制|
|---|---|
|日志|Per-Runtime 单写；启动快照|
|工作区文件|乐观并发：读 hash → patch → 写前复核 → 冲突|
|Git ref|ref 级短锁（若需要）|

**禁止**混成一个 workspace 总锁。

---

## 十一、代价（必须写明）

1. A 不实时见 B 的 Todo/Review/Session 新事实。  
2. 跨 Session 查询 = 启动快照 + 本进程变化。  
3. 同 Session 多进程 = 允许陈旧视图上操作；靠重启时间线汇合，不靠实时串行化。  
4. 日志数随启动次数增长；启动 O(总事件) 或 O(N log R) 归并。  
5. 第一版不做 compaction coordinator；未来仅**非权威**缓存，坏则从 NDJSON 重建。

---

## 十二、删除对照

|旧|新|
|---|---|
|单文件 workspace Journal + 锁|每 Runtime 一文件，无跨进程锁|
|中间损坏全局拒启|单源截断；全局 best effort|
|Reader 修尾/截断他文件|永不改他文件|
|实时 tail / 多写者短锁|启动 Frontier 固定|
|Previous / Fork / Owner|删除|
|全局单 Session Driver|每 Runtime 内单 Driver|
|ProgressStamp = 全局 Seq|见 KISS-02：本 Runtime 已应用序号 + 启动快照基底|

---

## 十三、ProgressStamp 关系

启动后本 Runtime 的 `ProgressStamp` 前进来自**本进程**成功 Apply 的事件（可用 `OwnLocalSeq` 或进程内单调应用计数）。  
While 进展检查仍防本 Driver 空转；不要求跨 Runtime 全局序号。

---

*KISS-04 终。Lifetime：Per-Runtime Journal + Lifetime Snapshot Isolation + Chronological Replay。*
