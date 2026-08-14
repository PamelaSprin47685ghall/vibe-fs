# WHY — time-capability

## 不可替代的存在理由

**时间若从 ambient wall clock / 全局 timer 偷渡业务代码，同一事实在不同运行时刻会产生不同判断，proof 与 replay 都失去确定性。**

产品语义要可重放（replay）、可审计、可测试，前提是**同一个输入永远推出同一个输出**。物理墙钟是最大的隐藏输入：它每秒都在变、随测试机快慢漂移、随时区不同错位。如果业务判断直接读 `UtcNow` / `Date.now` / `setTimeout`：

- 同一段逻辑今天跑绿、明天跑红，没有任何输入变化；
- proof 依赖「这次跑得够快/够慢」的运气，无法重放；
- 失败无法最小复现，因为时间是每次不同的随机源。

因此时间不能是 ambient（环境的、隐式的、全局的），必须作为 **capability** 显式穿过依赖边界：谁需要时间，谁就在构造时声明要一个 `IClockPort` / `ITimerPort`；测试注入虚拟实现，生产注入物理实现。这是本包唯一的、不可替代的 WHY。

## 独立变化测试（Independent Change Test）

从当前 port 形态改为显式 `Instant`/`Deadline` token + scheduler capability，只要不改变 `causal-wait` 的等待语义、不改变 `process-execution` 的进程语义，本包就允许整体重写。反过来，`causal-wait` 或 `process-execution` 的内部重写也不应该要求本包改变——这证明「时间如何进入系统」是一个独立的语义轴，不能并入任何业务包。

## 失败模式考古（历史上为什么发生过）

### 1. `TurnCompletionProgram` 变成事实上的第二运行时（ce-temporal-ownership.md §1.3）

`archive/docs/what/dsl-structured-program.md` 的 DSL 静态门曾有一个机械漏洞，让 `StudentRunCell`（`mutable record` 状态机）整体漏过去；`TurnCompletionProgram` 长期充当「什么都管」的决策器。那时的时序判断散落在长期 `State / Pending / bool` 字段里，业务「走到第几步」与「现在几点」纠缠在一起——这正是 ambient 时间偷渡的温床：下一步靠可变状态 + 隐藏 timer 决定，无法测试、无法重放。`time-capability` 与 `structured-workflow`（无第二程序计数器）互为表里：本包负责时间**以显式能力进入**，`structured-workflow` 负责控制流**以语言结构表达**。

### 2. 轮询与 sleep 充当因果进展（reconciler-event-driven-de-polling.md）

Reconciler 曾按 `[50; 100; 250; 500; 1000; 2000; 3000; 5000]` ms 退避 `setTimeout` 重读 snapshot，`budget = 30_000` ms——用墙钟推进业务探测。教训：**时间不能推进业务判断**。墙钟只能用于「距上次因果进展的静默时长」这类 deadline/watchdog 判据（VERIFY-004），且必须集中、可取消、可注入（`ITimerPort`）。该 change 因此把「等待」分类为四类（A 业务状态探测 / B 事件等待 / C deadline-watchdog / D 跨进程互斥），其中 C 类是唯一允许墙钟的等待，而它的墙钟也必须经 `ITimerPort` 注入——这是 `time-capability` 与 `causal-wait` 的精确分界。

### 3. Node timer 上限与进程生命周期（PtyTiming.fs）

JS `setTimeout` 的 Int32 上限（`0x7FFFFFFF` ms ≈ 24.8 天）意味着「长预算必须分段等待」；长预算（≥1000ms）若 `unref()` 不恰当，干净进程会被 timer 持住不退出。这些物理事实如果散落在各业务调用点，就会各自写出边界错误；集中在 `Deadline.nextWaitMs`（封顶）+ `PtyTiming.timerTask`/`nodeTimerPort`（unref 策略）后，业务只面对一个不会溢出的 typed deadline。

### 4. 时区/裸 Date 陷阱（tests/unit/domain.meta.test.mjs hazard 1）

Fable 的 `DateTimeOffset` 比较依赖 `offset` 字段；测试若用裸 `new Date(iso)` 构造时刻，在非 UTC 时区下 `Deadline.isExpired` 会对未到期的 deadline 返回 true——测试静默错误。教训：时刻值必须带显式 offset（`utcOffset`/`clockAt` facade），deadline 判定必须对进程时区不敏感。这也是「时间值本身不是 authority」的反面教材：值若没有正确语义（offset），连被规则消费的资格都没有。

## 与相邻包的边界（为什么不是它们的子集）

| 相邻语义 | 归属 | 理由 |
|---|---|---|
| 等待的业务因果、诊断观测非权威 | `causal-wait` | 等待「为什么没发生」是另一条语义轴；本包只提供时间能力 |
| 进程有界执行、PTY 物理完成 | `process-execution` | deadline 是 process 语义的**输入**，不是 process 语义本身 |
| 无第二程序计数器 | `structured-workflow` | 控制流结构独立于时间如何注入 |
| proof 分层与可红门禁 | `verification-system` | 虚拟时间测试是证明手段，不是产品 guarantee |

## RED 的样子

- 业务层任何一处直接出现 `DateTimeOffset.UtcNow` / `Date.now` / `setTimeout` / `timerTask`（Domain/Application/Session 层）。
- 两个语义相同的运行，仅因机器快慢/时区/测试顺序给出不同业务结果。
- deadline 无法在测试中推进（没有虚拟时钟或虚拟 timer 可用）。
- 一个 deadline 值被当作「真理」直接比较，而没有领域规则解释它的意义。
