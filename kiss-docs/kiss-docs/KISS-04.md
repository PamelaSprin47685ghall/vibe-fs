# Journal 设计

承 KISS-01。本卷定义持久化：**Per-Runtime 单写 NDJSON + Lifetime Snapshot Isolation + 按 ObservedAt 确定性归并**。

旧 NDJSON 不兼容；正式版前可删档。命名：**Per-Runtime Journal · Lifetime Snapshot Isolation · Chronological Replay**。

## 一、核心语义 [NORMATIVE]

每个 OpenCode Runtime 拥有一份永不被别人写入的进程日志。启动时读取所有日志的稳定前缀，Fold 得 `BootSnapshot`；运行期间只追加自己的事件并提供 read-your-writes。其他 Runtime 在 Frontier 之后的新增，本进程重启前不可见。

|承诺|含义|
|---|---|
|Read Your Writes|本 Runtime append + flush 成功后，事件才进入本进程内存投影|
|Repeatable Foreign Snapshot|生命周期内所见他源历史不变|
|Restart Reconciliation|新进程重新枚举全部日志并归并|
|Chronological Last Effect|时间线顺序交给领域 Fold；Journal 不实现 LWW 冲突引擎|

这不是 eventual consistency、跨进程实时一致性或全局 flush 顺序。系统只承诺按各 Writer 记录的墙钟时间做确定性 best-effort Chronological Replay。

## 二、目录与身份 [NORMATIVE]

```text
.wanxiangshu-next/
  runtimes/
    <runtime-id>.ndjson
```

- `RuntimeId` = 每次进程启动生成的 UUID / UUIDv7 / ULID；禁止 PID 作身份。
- `CreateNew` 排他创建；碰撞则换新 Id 重试。
- 每进程唯一 `FileStream` Writer，append 队列串行化。
- Writer 使用 `FileShare.Read` 或平台等价模式；Reader 使用 `FileShare.ReadWrite` 或平台等价模式，允许活跃 Writer 被 best-effort 读取。
- Reader 永不修复、截断、续写他人文件；无 workspace/session lockfile、无跨进程 append 锁。

`RuntimeStarted` 是日志第一条成功记录。内容须与文件名 `RuntimeId` 一致；PID、启动时间只用于诊断。空文件表示启动未完成，可安全忽略。

## 三、Envelope [NORMATIVE]

```fsharp
type EventId = private EventId of string
type RuntimeId = private RuntimeId of string
type TurnId = private TurnId of string

type StreamId =
    | Workspace
    | Session of SessionId
    | Child of ChildId
    | Squad of SquadId
    | Process of ProcessId

type Envelope =
    { RuntimeId: RuntimeId
      LocalSeq: int64
      ObservedAt: DateTimeOffset
      EventId: EventId
      Stream: StreamId
      TurnId: TurnId option
      Fact: Fact }
```

`ObservedAt = clock.UtcNow()`，记录真实观察到的 Raw Wall Time。系统钟同刻、回拨或跳变均可能发生，因此它不是严格物理时间，也不是全局 commit order。

同一源的权威顺序是严格递增的 `LocalSeq`；跨源采用稳定 k-way merge，比较键为 `ObservedAt → RuntimeId`，并保留每个源的 `LocalSeq` 相对顺序。相同时间由 `RuntimeId` 确定性破平局。不得把不同 Runtime 的 LocalEpoch/数字大小解释成全局先后。

## 四、启动算法 [NORMATIVE]

1. 生成新的 `RuntimeId`，但暂不创建自己的日志。
2. 枚举现有 `*.ndjson`，对每个源捕获当刻 `ByteLength`，形成 `Frontier: Map<RuntimeId, int64>`。
3. 只读各源 `[0, ByteLength)`，按完整合法换行行解析并稳定 k-way merge，Fold 得 `BootSnapshot`。
4. 源在 Frontier 边界的半行属于不可见前缀；首个内部非法行使该源停止读取并记录诊断，其他源继续。Reader 永不改文件；该源的可见前缀截止，不执行文件截断。
5. `CreateNew` 当前 Runtime 文件，写入 `RuntimeStarted` 作为第一条成功记录（`LocalSeq = 1`）。
6. 发布只读 BootSnapshot，再开放 Hook / Driver。

Boot 失败不留下空 Runtime 日志；自己的文件不进入本次 BootSnapshot。

## 五、运行期与 Commit [NORMATIVE]

```fsharp
type JournalFailure =
    | WriteFailed of string
    | FlushFailed of string

type CommitResult =
    | Committed of Envelope
    | CommitUnknown of EventId * JournalFailure
```

`EventId` 必须在写入前生成。Append 顺序：生成 `LocalSeq`/`EventId`/`ObservedAt` → 序列化 → 写完整行 → flush → 成功才 `Committed` 并 `Apply` 本地投影。

任意 write/flush 异常的回执都可能不确定：当前 Runtime **不 Apply**，Journal 进入 `Poisoned`，停止后续 append，返回 `CommitUnknown`，必须重启。不能写“写失败 = 事实未发生”。

重启只接受完整、合法、以换行结束的 Envelope；若完整行实际存在，事实可见；若只有半行，则事实不可见。当前 Runtime 不重试、不截断、不猜测。重启后的历史 Fold 通过稳定身份（`EventId`、`TurnId`、`PromptKey`、Todo snapshot）判断外部动作是否需要 reconcile。

```fsharp
type RuntimeSnapshot =
    { Frontier: Map<RuntimeId, int64>
      Projections: ProjectionSet
      OwnRuntimeId: RuntimeId
      OwnLocalSeq: int64 }
```

盘已成功、投影 `Apply` 失败时，Journal 事实仍然存在；本 Runtime 进入 `ProjectionBroken` / Fail Closed，不回滚、不假装失败。

## 六、Prompt 投影与 Driver 局部化 [NORMATIVE]

Prompt 状态必须拆成两个投影：

```fsharp
type HistoricalPromptIndex = Map<PromptKey, PromptHistory>
type LocalPromptProtocol = Map<SessionId, PendingPrompt option>
```

`HistoricalPromptIndex` 来自 BootSnapshot 中所有 Runtime 的已提交 Prompt 事实；它用于重启后的历史复用与宿主状态 reconcile。`LocalPromptProtocol` 只消费当前 Runtime 启动后的本地事实；外 Runtime 的 Pending 永不变成本地锁。

同一 Runtime 内每个 `(RuntimeId, SessionId)` 最多一个 Driver 和一个本地 Pending Prompt。同 Session 的不同 Runtime 可并发运行，不共享 mutable、不实时互等，下次启动再归并。

`sendOnce` 顺序：

1. 本地 Pending 存在 → await / reconcile，不重发。
2. 历史存在 Terminal 且 payload 相同 → 复用历史结果。
3. 历史有 Submitted/UserMessageId 但无 Terminal → 按 UserMessageId / parentID 查询宿主并 reconcile。
4. 历史只有 Requested，或状态无法确认 → `PromptUncertain`，或执行文档批准的策略；不得静默加本地全局锁，也不得假装未发送。
5. 无历史 → 建立本 Runtime Local Pending 并发送。

## 七、领域 Fact 与 Fold [NORMATIVE]

并发事实全部保留为历史；最终状态是时间线 Fold，不是存储层冲突协议。

```fsharp
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
    | HumanTurnStarted of {| TurnId: TurnId |}
    | SessionSettled of {| Result: SessionResult |}

type TodoFact =
    | TodoChanged of {| Snapshot: TodoSnapshot |}

type ReviewFact =
    | ReviewApplied of
        {| Verdict: ReviewVerdict
           Round: int
           ResultingTodo: TodoSnapshot option |}
```

`TodoChanged A @ t1; TodoChanged B @ t2` 的具体效果由 Fold 定义；两侧 PromptRequested/Submitted/Terminal 全部保留。SessionSettled 后更晚事实也由 Session Fold 明文处理，存储层不拒写。

## 八、与工作区文件 / Git 分层

日志一致性 ≠ 源码文件一致性。

|层|机制|
|---|---|
|日志|Per-Runtime 单写；启动快照|
|工作区文件|读 hash → 写前复核 → 冲突返回|
|Git ref|ref 级短锁（若需要）|

禁止混成一个 workspace 总锁。

## 九、代价与删除对照

1. 运行中 A 不实时见 B；靠重启时间线汇合。
2. 日志数随启动次数增长；启动 O(总事件) 或 O(N log R) 归并。
3. 第一版不做 compaction coordinator；未来仅非权威缓存，坏则从 NDJSON 重建。

|旧|新|
|---|---|
|单文件 workspace Journal + 锁|每 Runtime 一文件，无跨进程锁|
|中间损坏全局拒启|单源可见前缀截止；全局 best effort|
|Reader 修尾/截断他文件|Reader 永不改他文件|
|Previous / Fork / Owner|删除|
|Epoch 方案|Envelope / Fact 使用 TurnId；LocalEpoch 仅内存|
|伪单调时间 / 全局 commit 顺序|Raw ObservedAt + LocalSeq 保序 + 稳定 k-way merge|
|写失败即未发生|CommitUnknown + Poisoned 停写 + 重启前缀扫描|
|全局 Session Driver|每 Runtime 内单 Driver|
|Prompt 单一混合状态|HistoricalPromptIndex + LocalPromptProtocol|

## 十、ProgressStamp

启动后本 Runtime 的 `ProgressStamp` 只由本进程成功 Apply 的事件前进，可用 `OwnLocalSeq` 或进程内单调应用计数；不要求跨 Runtime 全局序号。

---

*KISS-04 终。Per-Runtime Journal + Lifetime Snapshot Isolation + Chronological Replay。*
