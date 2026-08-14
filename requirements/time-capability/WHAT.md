# WHAT — time-capability（唯一 normative 合同）

> 本文件是 `time-capability` 的唯一 normative 语义合同。每条命题必须同时为真；测试落点见 `PROOF.md`。
> 术语首次出现给定义；引用精确到 `src/...fs` 与测试文件。

---

## TIME-001：时钟与定时器是显式注入的 capability

**规范陈述**：业务代码（Domain / Application / Session 层）不得依赖 ambient 时间原语；凡需要读取当前时刻或安排延时，必须使用显式注入的 capability —— 读取时刻用 `IClockPort.UtcNow()`（`src/Wanxiangshu/Kernel/Temporal.fs`），安排延时用 `ITimerPort.Delay(milliseconds)`（返回 `IDeadlineHandle`）。

**含义/动机**：时间是最危险的隐藏输入。注入使每个消费者在构造时声明「我要时间」，测试就能替换成虚拟实现，生产替换成物理实现（`PtyTiming.nodeClockPort` / `nodeTimerPort`）。不注入，任何一次测试运行都受宿主机器时钟摆布。

**边界**：`Process/` 与 `Infrastructure/` 是物理适配器层，允许直接接触 Node timer / `DateTimeOffset.UtcNow`（`Process/ProcessRunner.fs` 第 92 行 `let clock = fun () -> DateTimeOffset.UtcNow` 是物理层合法形态）；业务层禁止。`ITimerPort` / `IClockPort` 的名字本身是 HOW（`Temporal.fs` 头注释：Contract only，无 Node/setTimeout/Fable JS）。

**证据指针**：→ `PROOF.md` TIME-001 行。

---

## TIME-002：deadline 与 elapsed 有 typed 表达，不散落为裸时刻比较

**规范陈述**：截止时间必须由 `Process/Deadline.fs` 的 `Deadline` 类型表达（私有构造 `Deadline of expiresAt`，只能经 `Deadline.ofBudget now budget` 构造），通过 `Deadline.remaining` / `Deadline.isExpired` / `Deadline.nextWaitMs` 消费；已耗时间经注入时钟采样（如 `CompletionMailbox` 的 join 等待用 `clock.UtcNow()` 采样 `PtyJoinItem.toRunCompletion`）。业务代码不得持有裸 `DateTimeOffset` 手写「现在是否超时」。

**含义/动机**：typed deadline 把「有界」写进类型：无法从外部读出 `expiresAt` 再自行解释；所有判定必须经注入时钟的纯函数。这同时消灭溢出（`ofBudget` 对 `DateTimeOffset.MaxValue` 截断）与 JS timer Int32 上限问题（`nextWaitMs` 封顶 `MaxTimerWaitMs = 0x7FFFFFFF`，长预算分段等待）。

**边界**：具体超时预算数值（DevOps 10s、process hard limit）是消费方 HOW；`Deadline` 类型与函数是时间能力本身，归本包。`ProcessEstimate.effectiveDeadline`（`Process/ProcessRequest.fs`，min(3×estimate, HardLimit)）是 process-execution 对 deadline 的应用，不重复归本包。

**证据指针**：→ `PROOF.md` TIME-002 行。

---

## TIME-003：时间可虚拟化；测试用虚拟实现替换物理时钟与 timer

**规范陈述**：`Process/PtyTiming.fs` 必须提供虚拟实现：`createVirtualTimerPort()`（返回 `VirtualTimerPort { Port; Advance; NowMs }`）与 `createVirtualClockPort()`（返回 `VirtualClockPort { Port; AdvanceMs; Set }`）。虚拟 timer 的 `Advance` 只在越过 handle 的截止点时精确触发回调；`Cancel` 后零回调；`Dispose` 后清除全部 pending。虚拟时钟从固定起点（`2000-01-01T00:00:00Z`）开始，可 `AdvanceMs` / `Set`。

**含义/动机**：proof 必须确定、可重放、与墙钟无关（VERIFY-004「Temporal Tests Use Virtual Time」）。虚拟实现让测试在毫秒级精确推进时间，无需真实 sleep；race 以显式 trace 枚举，不靠调度器运气。

**边界**：虚拟实现的**测试适配**（`requirements/verification-system/tests/support/domain/host.mjs` 的 `timerPort` / `clockPort` facade）属于 test support，不归本包；虚拟实现的**生产类型**（`PtyTiming.fs`）是本包证据。`PtyTiming.timerTask`（无取消面的 fire-and-forget 延时）是物理适配器内部机制，归 HOW。

**证据指针**：→ `PROOF.md` TIME-003 行。

---

## TIME-004：Domain / Application / Session 禁止直接读 ambient 时间

**规范陈述**：`src/Wanxiangshu/` 下 Domain / Application / Session 三层的 `.fs` 文件不得出现 raw time token：`DateTimeOffset.UtcNow`、`DateTime.Now`、`DateTime.UtcNow`、`Date.now`、`setTimeout`、`timerTask`（物理适配器例外由静态扫描的 allowlist 显式声明）。

**含义/动机**：这是 TIME-001 的可执行版本。静态扫描比 code review 可靠：即使程序员想「这次偷偷读一下墙钟」，gate 会红（`scripts/checks/g4r-ce-vocabulary.mjs` 的 `RAW_TIME_SCAN_LAYERS = ['Domain','Application','Session']` + `RAW_TIME_TOKENS`）。

**边界**：该静态扫描**机制**归 `structured-workflow`（`g4r-ce-vocabulary` gate 的 CE vocabulary 扫描）与 `verification-system`（可红门禁）；本包只拥有「业务层不读 ambient 时间」这条产品事实，并消费该 gate 作为 enforcement。`HOST-013` 的 `SessionStartedAt` 经 `IClockPort` 计量不碰 ambient `UtcNow` 是本条在 Host 场景的实例。

**证据指针**：→ `PROOF.md` TIME-004 行。

---

## TIME-005：时间值本身不是 authority；只有消费它的领域规则 + 注入时钟决定意义

**规范陈述**：一个时刻值/一个 deadline 值不携带业务意义。同样的值，由不同规则消费得到不同判断；判定必须由「领域规则 + 注入时钟」共同给出，不由值自身、更不由 ambient 时钟给出。`Deadline` 的私有构造保证业务代码无法脱离规则函数单独解释该值。

**含义/动机**：G4R 原则「Time is input, never authority」：race 是代数不是调度器彩票——`fold(A;B) == fold(B;A)`。若时间值自身有 authority，同一个 deadline 在不同时钟/不同规则下会「自己决定」结果，proof 与 replay 同时失效。把时间当输入（input）而非权威（authority），是全部 temporal proof 的基石。

**边界**：具体业务规则（如「超过 10s 结束 join 等待」）归消费方（`process-execution` / `delegation`）；「值不等于判断」这条元规则归本包。`requirements/verification-system/tests/support/temporal-harness.mjs` 的「One World / Pure Time」是这条原则的 proof 侧证据（REUSE）。

**证据指针**：→ `PROOF.md` TIME-005 行。

---

## TIME-006：deadline 是 causal-wait 的可选 escape（消费关系，非依赖）

**规范陈述**：`causal-wait` 的 `WaitEscape.DeadlineAt of DateTimeOffset`（`src/Wanxiangshu/Kernel/CausalWait.fs`）引用本包提供的 typed 时刻能力作为等待的显式终止路径之一。本包**不**依赖 `causal-wait`：deadline 能力独立成立；`causal-wait` 需要 deadline 时才消费本包（Phase E 已审计：`time-capability → causal-wait` 条件依赖不是 hard edge）。

**含义/动机**：deadline 是「如果 producer 永远不满足，谁负责结束这个等待」（CCE-005）的合法答案之一。把 deadline 表达为 typed escape，等待的诊断可显示「deadline in Ns」而业务无需解释该值。

**边界**：`WaitEscape` 类型与等待语义归 `causal-wait`；本包只提供被引用的时刻值来源（`Deadline`/`IClockPort`）。

**证据指针**：→ `PROOF.md` TIME-006 行。

---

## 反向覆盖（源 Clause → 本包命题）

| 源 Clause（COVERAGE.md 归属） | 落点 |
|---|---|
| EXEC-011 deadline 有界（时间部分） | TIME-002 / TIME-003 |
| EXEC-025 / EXEC-004 DevOps 10s → DeadlineExpired（机制部分；10s 数值=HOW） | TIME-002 |
| HOST-013 `SessionStartedAt` 经 `IClockPort`、禁 ambient `UtcNow` | TIME-004 |
| `reconciler-event-driven-de-polling.md` C 类 deadline 允许墙钟但须注入 | TIME-001 / TIME-003 |
| G4R「Time is input, never authority」 | TIME-005 |
| `causal-wait` 的 `DeadlineAt` escape | TIME-006 |
| `g4r-ce-vocabulary` RAW_TIME 扫描（机制归 structured-workflow） | TIME-004（消费） |
| `loop.md` / `orchestrator.md` | 无本包内容（loop 的 `LoopKillArmed` 进程内事实归 degeneration-guard；orchestrator 无时间条款）→ 弃权记录见 `HOW.md` |
