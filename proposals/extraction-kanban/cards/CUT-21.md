# CUT-21：显式时间与因果等待（含 D-03 护栏）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-21) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`READY`（由 `CUT-00` 完成准备核对后转入，待认领）
- **所属泳道 / 波次**：物理 / W1 独立卡
- **切换前置**：`CUT-00`（已完成，见 [CUT-00](CUT-00.md)）
- **关联护栏（D-03 & 公共编辑面避让）**：
  - **D-03**：基础 `ProcessSession` 的 pipe 留尾与不杀进程 wait 超时，不等于主仓的强类型 `Deadline` 硬期限、`Spool` 全量输出外存与 PTY；本卡负责把显式时钟/定时器能力、强类型 `Deadline`、虚拟时基与因果等待（`CausalWait` / `CompletionMailbox`）独立抽清；
  - **公共编辑面避让**：`src/Wanxiangshu/Process/{ProcessRunner,NodeProcessHost,NodeProcessWait,Surface}.fs(.fsi)` 当前由 `CUT-02`（`eng-cut02`）预约持有，`Spool` 与 PTY 的物理进程接管归 `CUT-20`。本卡在施工时**只修改时间与因果等待所属模块**，不触碰 `CUT-02` 已预约的 4 个文件；若需改动 `Process/Surface.fs`，须待 `CUT-02` 释放或由当轮集成者串行合入。
- **零阻塞结论**：`Foundation/Temporal.fs`、`Process/{Deadline,DeadlineSurface,NodeTiming,VirtualTiming}.fs` 与 `Execution/Session/Wait/{CausalWait,CompletionMailbox,Registry,Await,Bridge,Surface}.fs` 在主仓中已有独立 `.fsproj` 分片（`time-capability-008`、`causal-wait-009`），与 `CUT-02` 的进程 spawn 实现无编辑面重叠，可零阻塞独立抽出至 `wanxiangli`（对应 `src/time` 草稿）。

## 2. 草稿概念名/路径与主仓真实模块/`.fsproj` 映射

委托说明及 `wanxiangli/src/time` 草稿中提及了 `Process/Deadline.fs`、`Spool.fs`、测试虚拟时钟与因果等待。在主仓当前基线（`b322c180b`）中，各文件的真实路径、`.fsproj` 归属及与 `CUT-02`/`CUT-20` 的精确分界如下：

| 职责分片 | 主仓真实源码路径（含 `.fsi`） | 所属编译单元（`.fsproj`） | 去向判定 |
|---|---|---|---|
| 时钟与定时器纯能力契约 | `src/Wanxiangshu/Foundation/Temporal.fs(.fsi)` | `Wanxiangshu.Owner.time-capability.foundation-temporal-contract.fsproj` | **抽出核心（A1）**：`IDeadlineHandle`、`ITimerPort`、`IClockPort` |
| 强类型 `Deadline` 纯计算 | `src/Wanxiangshu/Process/Deadline.fs(.fsi)`<br>`src/Wanxiangshu/Process/DeadlineSurface.fs(.fsi)` | `Wanxiangshu.Owner.time-capability.process-deadline-contract.fsproj`<br>`Wanxiangshu.Owner.time-capability.foundation-temporal.fsproj` | **抽出核心（A1）**：`Deadline.ofBudget / remaining / isExpired / nextWaitMs`（零溢出、零时区漂移） |
| 物理时钟/定时器适配器 | `src/Wanxiangshu/Process/NodeTiming.fs(.fsi)` | `Wanxiangshu.Owner.time-capability.process-node-timing-adapter.fsproj` | **抽出物理适配器（A2）**：唯一封装 Node `setTimeout` / `clearTimeout` 与系统时钟的实例 |
| 确定性虚拟时钟与定时器（测试辅助） | `src/Wanxiangshu/Process/VirtualTiming.fs(.fsi)` | `Wanxiangshu.Owner.time-capability.process-virtual-timing.fsproj` | **抽出验证时基（A3）**：`VirtualClock`、`VirtualTimer`（支持 `Advance`、`Set`、确定性到期队列与取消） |
| 因果等待纯契约与前沿分析 | `src/Wanxiangshu/Execution/Session/Wait/CausalWait.fs(.fsi)` | `Wanxiangshu.Owner.causal-wait.execution-session-wait-contract.fsproj` | **抽出核心（B1）**：`DiagnosticWait`、`WaitEscape`、`IWaitObserver`、`IWaitSnapshotReader`、`CausalFrontier.ofSnapshot`、`ICompletionMailbox` 契约 |
| 因果等待注册表、复合等待与完成邮箱 | `src/Wanxiangshu/Execution/Session/Wait/Registry.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/Wait/Await.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/Wait/Bridge.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/Wait/Surface.fs(.fsi)` | `Wanxiangshu.Owner.causal-wait.execution-session-wait-runtime.fsproj`<br>`Wanxiangshu.Owner.causal-wait.execution-session-wait-diagnostic-adapter.fsproj`<br>`Wanxiangshu.Owner.causal-wait.execution-session-wait-completion-mailbox.fsproj`<br>`Wanxiangshu.Owner.causal-wait.execution-session-wait-proof-surface.fsproj` | **抽出等待运行时（B2）**：幂等租约 `IWaitLease`、`CausalAwait.untilSignalOrDeadline`、单等待者/多信号 `CompletionMailbox` |
| 会话起点单次采样投影（`HOST-013`） | `src/Wanxiangshu/Execution/Session/SessionStartedAtProjection.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/SessionStartedAtLedger.fs(.fsi)` | `Wanxiangshu.Owner.time-capability.execution-session-sessionstartedatprojection.fsproj`<br>`Wanxiangshu.Owner.time-capability.execution-session-sessionstartedatledger.fsproj` | **留在主仓会话层（C）**：消费注入的 `IClockPort` 采样并持久化 `SessionStartedAt` |
| `Spool.fs`（进程大输出分块外存） | `src/Wanxiangshu/Process/Spool.fs(.fsi)` | 随进程输出管理（与 `NodeProcessWait` 在超时/容量边界配合） | **按 D-03 与 CARDS.md 归属 `CUT-20`**：`Spool` 负责 200 KiB 块外存，不混入纯时间/等待库 |

## 3. 目标

- **用户/维护者当前损失**：`wanxiangli/src/time` 仍为草稿，而主仓的进程执行、委派 join、宿主信号调和与测试套件均依赖显式注入的 `IClockPort`/`ITimerPort`、强类型 `Deadline`、确定性 `VirtualTiming` 以及不具业务权威的 `CausalWait` 诊断前沿；若时间与等待原语未独立成库，其他卡在迁移时极易退回直接调用 `Date.now()` / `setTimeout` 或用盲目轮询替代事件唤醒。
- **这次完成后可少维护什么**：在 `wanxiangli/time`（或 `time` + `causal-wait` 子路径）提供完整的显式时间契约、`Deadline` 代数、物理/虚拟时基以及因果等待原语，主仓直接复用，不再维护重复的定时器包装与等待注册表。
- **明确不做**：
  - 不把时钟或等待观察器做成全局隐式单例塞进领域对象（严格遵守 `time-capability-001/004/008` 与 `causal-wait-004/009`）；
  - 不让等待超时或诊断快照产生任何业务成功/完成/关闭事实（`time-capability-005/006`、`causal-wait-001/003`）；
  - 不在本卡内改动 `CUT-02` 持有的 `NodeProcessHost`/`NodeProcessWait`/`ProcessRunner`，也不迁移 `CUT-20` 负责的 `Spool` 与 `Pty*`。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/time-capability/WHAT.md`：`[001]` 显式注入时钟/定时器与可取消句柄、`[002]` 强类型 `Deadline` 计算、`[003]` 确定性虚拟时间、`[004]` 禁止 Domain/Application/Session 读取 ambient 时间、`[005]` 时间值不是业务 authority、`[006]` `Deadline` 是 `causal-wait` 的可选 escape、`[007]` `SessionStartedAt` 首次 prompt 单次采样、`[008]` temporal 词汇/能力/物理适配器/虚拟实现编译分居。
  - `requirements/causal-wait/WHAT.md`：`[001]` 等待观测无业务权威、`[002]` 五要素因果可诊断（Owner/Wait/Producer/Last causal progress/Termination）、`[003]` 诊断不入持久化与 Prompt、`[004]` 读写权限隔离（业务只拿 `IWaitObserver`，外部诊断才拿 `IWaitSnapshotReader`）、`[005]` 事件驱动与复合等待、`[006]` 终止观测与幂等释放、`[007]` 未满足因果前沿 `CausalFrontier`、`[008]` 进程内生命周期、`[009]` 契约/运行时/诊断/CompletionMailbox 分离。
- **新库草稿差异及裁决**：
  - `wanxiangli/src/time` 草稿须严格遵守 `time-capability-008` 与 `causal-wait-009` 的分居要求：纯契约（`IClockPort`/`ITimerPort`/`Deadline`/`CausalWait`）不得传递导出 `VirtualTiming` 或 `IWaitSnapshotReader` 给普通业务消费者；取消单个等待租约或定时器句柄绝不能偷取消其他并发 waiter。

## 5. 自治个体（A / B / C）

- **A（显式时间个体：`Deadline` + `NodeTiming` + `VirtualTiming`，抽入新库）**：
  - 负责：`Deadline` 的预算构造与剩余切片计算（单次 timer 等待上限 `MaxTimerWaitMs` 防止 32-bit 溢出）；物理 `NodeTiming` 与测试 `VirtualTiming` 的可取消延迟调度。
  - 拥有状态：`Deadline` 无状态；`VirtualClock` / `VirtualTimer` 拥有独立的测试内当前时刻与按 `(DueAt, Sequence)` 排序的待触发队列，不同测试实例互不共享。
  - 永远成立的不变量：时间未推进到 `DueAt` 前绝不触发回调；已 `Cancel()` 的句柄在推进时间后绝不触发；`remaining` 与 `nextWaitMs` 对任意极端 `TimeSpan` 不发生算术溢出。
- **B（因果等待个体：`CausalWait` + `WaitRegistry` + `CausalAwait` + `CompletionMailbox`，抽入新库）**：
  - 负责：记录活跃 `DiagnosticWait`、在 `IWaitLease.Dispose()` / `MarkExit` 时幂等移出活跃集并追加有序 `WaitTransition`；从任意快照纯计算出 `CausalFrontier`（区分 `ExternalProducerFrontier`、`BrokenCausalEdge`、`ProducerRunningWithoutWait`、`CausalWaitCycle`、`Empty`）；提供信号与 `IDeadlineHandle` 竞争的 `CausalAwait.untilSignalOrDeadline` 以及支持先到达信号缓存的 `CompletionMailbox`。
  - 与 A 不共享可变值：B 仅通过接口消费传入的 `IDeadlineHandle`，不持有全局时钟。
- **C（主仓各业务组合层：Delegation Join、Process Wait、SessionStartedAt、Host Quiescence）**：
  - 负责：在组合根将 `NodeTiming`（或测试中的 `VirtualTiming`）与 `IWaitObserver` 注入各消费者；当 `CausalAwait` 返回 `Error WaitTimedOut` 时，由业务规则决定如何返回中断或继续等待，绝不把超时当成业务完成。

## 6. 公开消息与最小完整契约

- **时间契约与 `Deadline`**：
  - `IClockPort.UtcNow : unit -> DateTimeOffset`
  - `ITimerPort.Now : unit -> DateTimeOffset`、`ITimerPort.StartDeadline : dueAt: DateTimeOffset -> onExpired: (unit -> unit) -> IDeadlineHandle`
  - `Deadline.ofBudget : now: DateTimeOffset -> budget: TimeSpan -> Deadline`
  - `Deadline.remaining : clock: (unit -> DateTimeOffset) -> Deadline -> TimeSpan`
  - `Deadline.isExpired : clock: (unit -> DateTimeOffset) -> Deadline -> bool`
  - `Deadline.nextWaitMs : clock: (unit -> DateTimeOffset) -> Deadline -> int`
- **因果等待与前沿分析**：
  - `IWaitObserver.Enter : DiagnosticWait -> IWaitLease`（写端，业务可见）
  - `IWaitSnapshotReader.Snapshot : unit -> DiagnosticWaitSnapshot`（读端，仅诊断可见）
  - `CausalFrontier.ofSnapshot : DiagnosticWaitSnapshot -> CausalFrontier list`
  - `CausalAwait.untilSignalOrDeadline : IWaitObserver -> DiagnosticWait -> IDeadlineHandle -> tryRead: (unit -> 'T option) -> awaitSignal: (unit -> Task<unit>) -> Task<Result<'T, DiagnosticWaitExit>>`
- **拒绝 / 取消 / 幂等保证**：
  - `IDeadlineHandle.Cancel()` 与 `IWaitLease.Dispose()` / `MarkExit` 多次重复调用严格幂等；
  - `tryRead()` 在进入等待前已命中时，立即标记 `WaitResolved`、取消 `deadline` 句柄并返回 `Ok value`，不残留活跃等待或悬空 timer。

## 7. 主仓调用方清单与对应测试套件

### 7.1 主仓调用方清单

1. **时间与截止期消费者**：
   - `src/Wanxiangshu/Process/{NodeProcessWait,ProcessRunner,Pty,PtySupervisor}.fs`
   - `src/Wanxiangshu/Execution/Session/{SessionStartedAtProjection,SessionStartedAtLedger}.fs`
   - `src/Wanxiangshu/Composition/Durable/SessionStartedAtJournalAdapter.fs`
   - `src/Wanxiangshu/OpenCode/Host/{PairProgrammingGuidelineProjection,HostSignalBootstrap,SessionQuiescenceGate}.fs`
   - `src/Wanxiangshu/OpenCode/Plugin/{PluginLoad,PluginTransforms,PluginSessionWiring}.fs`
2. **因果等待与完成邮箱消费者**：
   - `src/Wanxiangshu/Execution/Delegation/{JoinAttemptRegistry,JoinGuardSurface,Runtime,Fork/Runtime,Fork/OpenCode/JoinTool,SyncDelegate/Runtime}.fs`
   - `src/Wanxiangshu/OpenCode/Tools/{PtyTool,ExecutorToolSurface}.fs`
   - `src/Wanxiangshu/Host/Diagnostics/Runtime.fs`

### 7.2 对应测试套件

- `requirements/time-capability/tests/001.test.mjs` 至 `008.test.mjs`（完整覆盖 `WHAT[TIME-CAPABILITY-001..008]`）
- `requirements/causal-wait/tests/001.test.mjs` 至 `009.test.mjs`（完整覆盖 `WHAT[CAUSAL-WAIT-001..009]`）
- 静态门禁脚本（在 `npm run check` 中运行）：检查 Domain / Application / Session 源码零 ambient 时间调用（对应 `time-capability-004`）。

## 8. 必须能红的最小反例与拆除测试设计

1. **反例 1（Domain / Application 偷读 ambient 时钟或定时器）**：在非白名单业务 `.fs` 文件中写入 `DateTimeOffset.UtcNow`、`DateTime.Now`、`Stopwatch` 或 `Task.Delay`，静态门禁与 `time-capability/tests/004.test.mjs` 必须立即变红。
2. **反例 2（虚拟时钟未到期提前触发、取消失灵或偷取消其他 waiter）**：在 `VirtualTiming` 上注册两个相同或不同到期时刻的延迟句柄 A 与 B，先 `Cancel()` A，再推进时间至到期点：若 A 被触发、或 B 未被触发、或在差 1ms 到期时提前触发，测试立即变红（对应 `time-capability-001/003`）。
3. **反例 3（等待超时伪造业务成功或残留租约/timer 泄漏）**：在 `CausalAwait.untilSignalOrDeadline` 中触发 deadline 到期，必须返回 `Error WaitTimedOut` 且活跃等待列表立即清空；若信号先到达，必须返回 `Ok value` 并立即取消 deadline 句柄（推进虚拟时钟越过原定截止点不得再触发过期回调，对应 `time-capability-005/006`、`causal-wait-005/006`）。
4. **反例 4（因果前沿环检测与断边识别）**：构造 `A 等待 B、B 等待 A` 的快照必须算出 `CausalWaitCycle`；构造 `A 等待不存在且无活跃状态的内部 Owner B` 必须算出 `BrokenCausalEdge`；构造 `A 等待外部 Provider` 必须算出 `ExternalProducerFrontier`（对应 `causal-wait-007`）。
5. **独立拆除测试**：在 `wanxiangli` 中独立编译 `time` 子包（零进程 spawn、零 EventStore、零 OpenCode SDK），运行上述全部虚拟时间、`Deadline` 边界与 `CausalWait` 前沿反例全绿。

## 9. 精确写集合与暂留/相邻卡边界

- **库仓（`wanxiangli`）写集合**：
  - `src/time/*` 及对应工程/导出声明（实现 `IClockPort`、`ITimerPort`、`IDeadlineHandle`、`Deadline`、`NodeTiming`、`VirtualTiming`、`CausalWait`、`WaitRegistry`、`CausalAwait`、`CompletionMailbox`）。
- **主仓（`wanxiangshu`）切换期写集合**：
  - `src/Wanxiangshu/Foundation/Temporal.fs(.fsi)`
  - `src/Wanxiangshu/Process/Deadline.fs(.fsi)`
  - `src/Wanxiangshu/Process/DeadlineSurface.fs(.fsi)`
  - `src/Wanxiangshu/Process/NodeTiming.fs(.fsi)`
  - `src/Wanxiangshu/Process/VirtualTiming.fs(.fsi)`
  - `src/Wanxiangshu/Execution/Session/Wait/{CausalWait,Registry,Await,Bridge,CompletionMailbox,Surface}.fs(.fsi)`
  - `src/Wanxiangshu/Wanxiangshu.Owner.time-capability.foundation-temporal-contract.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.time-capability.process-deadline-contract.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.time-capability.process-node-timing-adapter.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.time-capability.process-virtual-timing.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.causal-wait.execution-session-wait-contract.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.causal-wait.execution-session-wait-runtime.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.causal-wait.execution-session-wait-diagnostic-adapter.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.causal-wait.execution-session-wait-completion-mailbox.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.causal-wait.execution-session-wait-proof-surface.fsproj`
- **暂留主仓与相邻卡边界**：
  - **严格不修改** `CUT-02` 当前持有的 `src/Wanxiangshu/Process/{ProcessRunner,NodeProcessHost,NodeProcessWait,Surface}.fs(.fsi)`；
  - `src/Wanxiangshu/Process/Spool.fs(.fsi)` 与 `Pty*.fs(.fsi)` 归 `CUT-20`（遵守 **D-03**）；
  - `src/Wanxiangshu/Execution/Session/{SessionStartedAtProjection,SessionStartedAtLedger}.fs(.fsi)` 留在主仓会话层。

## 10. 认领调查记录（2026-10-15，eng-cut21）

- **认领**：`eng-cut21` 接卡进入 DOING；主仓侧只读调查已完成，全部源文件（§9 写集合所列 11 组 `.fs/.fsi`）与两套 WHAT 测试逐一核对，调查事实与实测调用方清单见 [evidence/CUT-21/README.md](../evidence/CUT-21/README.md)。
- **第一轮（阻塞已解除）**：本机当时不存在 wanxiangli 源码工作树，仅交付实现就绪包：契约冻结（以主仓已测 `ITimerPort.Delay` 毫秒式签名为准，不采用草稿的 `Now/StartDeadline` 形态）、`src/time` + `src/wait` 文件布局、`CompletionMailbox` 泛化裁决（库侧泛型化，`AgentHandleId`/`PtyJoinItem` 留主仓）、`Bridge.fs` 与主仓专用 Surface 部分留主仓的边界、库侧 `NNN.test.mjs` 测试清单与 DevOps 验证命令。
- **第二轮（库侧落盘完成）**：工作树位于 `wanxiangshu-lifecycle-cutover/wanxiangli`（HEAD 46d9c07）。已按蓝图新增 `src/time`（Temporal/Deadline/NodeTiming/VirtualTiming/Time 入口 + fsproj + index.d.ts + tests 001–008）与 `src/wait`（CausalWait/Registry/CompletionMailbox 泛型/Await/Wait 入口 + 同套配置 + tests 001–009，含卡内红反例 2/3/4）。**未修改 package.json、版本号、既有模块**；导出登记（`./time`、`./wait`）与发布留给协调者，建议条目见 evidence §7.3。实际差异（wait 零依赖 time 的本地契约声明、time-008 单 bundle 分居降级、测试走 dist 相对路径）均登记在 evidence §7.2 与两模块 AGENTS.md。
- **未做**：库侧构建与测试未运行（Engineer 无执行权限，验证命令见 evidence §5）；主仓源码切换未做（等 tarball 发布后按 evidence §4.6 清单串行执行；`Process/Surface.fs`、`NodeProcessWait.fs`、`ProcessRunner.fs` 的 import 更新与 CUT-02 已释放面衔接，由当轮集成者串行合入）。
