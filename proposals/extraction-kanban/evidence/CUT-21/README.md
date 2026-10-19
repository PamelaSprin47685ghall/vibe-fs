# CUT-21 证据：显式时间与因果等待

返回 [卡](../../cards/CUT-21.md) / [看板](../../BOARD.md)。本目录只登记事实与待执行方案，不是第二份业务规则。

## 1. 认领与当前状态

- **认领**：`eng-cut21`，2026-10-15，物理泳道 W1。
- **本轮交付**：主仓侧完整只读调查 + 库侧实现就绪包（契约冻结、文件布局、测试清单、验证命令）；**库侧源码与测试已于 2026-10-15 第二轮落盘**（见 §7），主仓切换仍待 tarball 发布。
- **基线**：主仓工作树（BOARD 记录最近主线集成为 `62dbebc5d`）；库仓 `wanxiangli main@46d9c07 / 0.5.2`。

## 2. 阻塞记录：wanxiangli 源码工作树（已解除）

首轮阻塞为库仓工作树缺失。2026-10-15 第二轮委派确认工作树位于 `/home/kunweiz/Desktop/vibe/wanxiangshu-lifecycle-cutover/wanxiangli`（独立 Git 仓，HEAD 46d9c07），阻塞解除。

## 3. 主仓现状调查（已完成，全部亲眼核对）

### 3.1 时间能力（A1/A2/A3 对象，均已存在且有测试）

| 文件（含 .fsi） | 公开符号 | 事实 |
|---|---|---|
| `src/Wanxiangshu/Foundation/Temporal.fs` | `IDeadlineHandle`（`Delay: Task<unit>`、`Cancel`）、`ITimerPort`（`Delay: milliseconds:int -> IDeadlineHandle`、`Dispose`）、`IClockPort`（`UtcNow`） | 纯契约，20 行，零 Node 依赖 |
| `src/Wanxiangshu/Process/Deadline.fs` | `Deadline`（private DU）、`Deadline.ofBudget/remaining/isExpired/nextWaitMs`、`MaxTimerWaitMs = 2_147_483_647` | 零溢出（clamp 到 `DateTimeOffset.MaxValue`）、零时区漂移；`MaxTimerWaitMs` 为普通 `let`（Fable literal 不发射导出） |
| `src/Wanxiangshu/Process/DeadlineSurface.fs` | `DeadlineSurface.create/remainingMs/isExpired/nextWaitMs/maxTimerWaitMs` | JS 面：ISO 字符串入、number/bool 出，Deadline 不透明 |
| `src/Wanxiangshu/Process/NodeTiming.fs` | `timerTask`、`raceExit`、`nodeTimerPort`、`nodeClockPort` | 唯一封装 `setTimeout`/`clearTimeout`/`unref`（≥1000ms unref）与 `DateTimeOffset.UtcNow` 的物理实例 |
| `src/Wanxiangshu/Process/VirtualTiming.fs` | `VirtualTimerPort`（`Port/Advance/NowMs`）、`VirtualClockPort`（`Port/AdvanceMs/Set`） | 确定性虚拟时基；按注册序触发（`fireAt <= nowMs`），Cancel 立即移除条目 |

### 3.2 因果等待（B1/B2 对象，均已存在且有测试）

| 文件（含 .fsi） | 公开符号 | 事实 |
|---|---|---|
| `Wait/CausalWait.fs` | `DiagnosticWait`、`WaitEscape`、`CausalOwnerRef/CausalProducerRef`、`DiagnosticWaitExit`、`WaitTransition`、`DiagnosticWaitSnapshot`、`IWaitLease`（`MarkExit` + `IDisposable`）、`IWaitObserver`、`IWaitSnapshotReader`、`IWaitDiagnosticSink`、`CausalFrontier.ofSnapshot`、`JoinBatch`、`JoinInterruptReason`、`MailboxWakeReason`、`ICompletionMailbox<'agent,'pty,'interrupt,'wake>` | 纯契约 + 前沿纯算法（317 行）；`CausalFrontier` 区分五类前沿 |
| `Wait/Registry.fs` | `CausalWaitRegistry`、`CausalWaitRuntime`（`BindDiagnosticTarget` 首绑唯一）、`CausalWaitProcess.local()` | 进程内单例；有界历史环（默认 256）；租约释放幂等 |
| `Wait/Await.fs` | `CausalAwait.awaitTask/awaitUnit/race/untilSignalOrDeadline` | `untilSignalOrDeadline`：tryRead 先行 → `Promise.race(signal, deadline.Delay)` → 信号胜则重读（同一 deadline）→ 到期返回 `Error WaitTimedOut`；胜出即 `deadline.Cancel()` |
| `Wait/CompletionMailbox.fs` | `CompletionMailbox(gate)`、`CompletionMailboxRuntime.create` | **绑定主仓类型**：`AgentHandleId`（Foundation.Identity）、`PtyJoinItem`（Execution.Session）——抽库必须泛化，见 §4.3 |
| `Wait/Bridge.fs` | `CausalWaitBridge.writeSnapshot/target` | 诊断文件桥（`node:fs`，写 `.wanxiangshu/diagnostics/causal-waits.json`，best-effort git exclude） |
| `Wait/Surface.fs` | `CausalWaitSurface.*` | JS 面：registry/lease 为不透明句柄，描述符与快照为纯对象 |

### 3.3 主仓调用方实测（grep 全量，比卡内 7.1 更宽）

时间能力消费者（切换 import 来源时须逐一过）：`Verification/TemporalSurface.fs`、`OpenCode/Tools/ToolRuntimeScope.fs`、`OpenCode/Plugin/PluginBoot.fs`、`OpenCode/Host/HostSignalBootstrap.fs`、`OpenCode/Host/MessageVisibility{,Surface}.fs`、`Git/Hook/Sync.fs`、`Git/Gateway.fs`、`Change/Host/Host.fs`、`Strength/OpenCode/Delegate.fs`、`Process/{ProcessRunner,NodeProcessWait,Pty,PtySupervisor,Surface}.fs`、`Execution/Delegation/**`（Fork/Runtime、Fork/Recovery、Fork/ChildRun、Fork/Host/*、ChildRecoveryWorkflow、Handle/JoinDrain）、`Execution/Session/SessionStartedAtLedger.fs`、`OpenCode/Host/PairProgrammingThoughtTransform.fs`。

因果等待消费者：`OpenCode/Plugin/{PluginSessionWiring,PluginHostWiring}.fs`、`OpenCode/Tools/{ToolRuntimeScope,ToolRuntimeScopeSurface,ToolRegistry}.fs`、`OpenCode/Host/PluginHostInterop.fs`、`Change/{Job,Runtime,Host/Types,Host/Host}.fs`、`Execution/Delegation/{Fork/Runtime,Fork/OpenCode/JoinTool,Fork/OpenCode/JoinSurface,Fork/Host/HostForkPtySurface,Fork/Host/Runtime,SyncDelegate/Surface}.fs`。

注意两点：
1. `Process/Surface.fs` 与 `NodeProcessWait.fs`、`ProcessRunner.fs` 属 CUT-02 已释放编辑面，本卡不修改这些文件本身；它们对时间模块的消费在切换时只改 import/构造来源，须由当轮集成者串行合入（与 CUT-02 释放记录的衔接点）。
2. `Sphinx/V2/Runtime/Ports.fs` 有一个同名 `IClockPort`（Sphinx 自有契约），与本卡对象无引用关系，抽取时不得误伤。

### 3.4 现有测试（主仓产品语义回归，已存在）

- `requirements/time-capability/tests/001–008.test.mjs`（WHAT[TIME-CAPABILITY-001..008]）
- `requirements/causal-wait/tests/001–009.test.mjs`（WHAT[CAUSAL-WAIT-001..009]）+ `fixtures/`
- 静态门禁在 `npm run check`（time-capability-004 的 ambient 时间零调用扫描）

## 4. wanxiangli 侧实现蓝图（待工作树就绪后落盘）

### 4.1 文件布局（沿用库仓现有 per-module 模式）

```
wanxiangli/src/time/
  Temporal.fs(.fsi)        # IDeadlineHandle / ITimerPort / IClockPort（自 Foundation/Temporal.fs 原样承接）
  Deadline.fs(.fsi)        # Deadline 代数（自 Process/Deadline.fs 原样承接）
  NodeTiming.fs(.fsi)      # 物理适配器（自 Process/NodeTiming.fs 承接）
  VirtualTiming.fs(.fsi)   # 验证时基（自 Process/VirtualTiming.fs 承接）
  index.d.ts               # JS 面类型（对齐 DeadlineSurface/TemporalSurface 的表示）
  tests/001–008.test.mjs   # WHAT[TIME-CAPABILITY-001..008]
wanxiangli/src/wait/
  CausalWait.fs(.fsi)      # 契约 + CausalFrontier（原样承接，去掉主仓 Session 命名空间）
  Registry.fs(.fsi)        # CausalWaitRegistry / CausalWaitRuntime
  Await.fs(.fsi)           # CausalAwait（untilSignalOrDeadline 等）
  CompletionMailbox.fs(.fsi)  # 泛化版，见 4.3
  index.d.ts
  tests/001–009.test.mjs   # WHAT[CAUSAL-WAIT-001..009]
```

`Bridge.fs`（node:fs 诊断文件桥）与 `Wait/Surface.fs` 的主仓专用部分（PtyJoinItem 视图、bindDiagnosticWorkspace）**留在主仓**：前者绑定主仓 `.wanxiangshu/` 目录约定，后者绑定主仓 PTY 词汇；库只提供纯契约与运行时。

### 4.2 契约冻结（以主仓已测签名为准）

以主仓现有签名为唯一基准（time-capability / causal-wait 测试 001–008 / 001–009 已锚定这些行为）：

- `ITimerPort.Delay : milliseconds:int -> IDeadlineHandle`（**不是**卡内草稿的 `Now/StartDeadline` 形态——草稿概念名与主仓真实模块的差异按"已存在可用小块直接继承"裁决，不改已测契约）；
- `Deadline.ofBudget/remaining/isExpired/nextWaitMs`、`MaxTimerWaitMs`；
- `IWaitObserver.Enter / IWaitLease.MarkExit+Dispose / IWaitSnapshotReader.Snapshot`；
- `CausalAwait.untilSignalOrDeadline : IWaitObserver -> DiagnosticWait -> IDeadlineHandle -> tryRead:(unit -> 'T option) -> awaitSignal:(unit -> Task<unit>) -> Task<Result<'T, DiagnosticWaitExit>>`。

### 4.3 CompletionMailbox 泛化（唯一真实代码差异）

主仓 `CompletionMailbox` 类的具体类型参数是 `AgentHandleId * PtyJoinItem * JoinInterruptReason * MailboxWakeReason`。抽库裁决：

- 库侧实现泛化为 `CompletionMailbox<'agent,'pty>`（`'interrupt = JoinInterruptReason`、`'wake = MailboxWakeReason` 随 causal-wait 契约进库）；
- 主仓组合层以 `CompletionMailbox<AgentHandleId, PtyJoinItem>` 特化并提供 `CompletionMailboxRuntime.create` 兼容构造点；
- `AgentHandleId` / `PtyJoinItem` 类型本体留在主仓（分别归 Foundation.Identity 与 Execution/Process 词汇），库不导入主仓类型。

### 4.4 导出与工程

- `package.json` exports 增补 `"./time"` 与 `"./wait"`（types + import，模式与现有六入口一致）；`files` 白名单补两个 `index.d.ts`；`npm test` 脚本补两组测试路径。
- 新建两个 fsproj（`time`、`wait`），依赖方向：`wait` → `time`（仅 `IDeadlineHandle` 契约）；`time` 内部零依赖。`wait` 不依赖 process/trace/ownership 任何入口。
- 分居要求（time-capability-008 / causal-wait-009）：`./time` 的公开导出不携带 `VirtualTiming`（虚拟时基仅测试可用，物理与虚拟不同入口或以显式子导出隔离）；`./wait` 的公开导出不携带 `IWaitSnapshotReader` 给业务消费者——快照读能力只经诊断专用导出。

### 4.5 库侧测试清单（NNN.test.mjs，WHAT 锚点）

- `src/time/tests/001.test.mjs`（TIME-CAPABILITY-001）：显式注入、句柄可取消、Cancel 后推进不触发。
- `002`：Deadline 零溢出/零时区（极端 TimeSpan、`DateTimeOffset.MaxValue` clamp、`nextWaitMs` 封顶 `MaxTimerWaitMs`）。
- `003`：虚拟时钟确定性（Advance/Set、到期队列、差 1ms 不提前触发）。
- `004`：库内无 ambient 时间（静态扫描 `Date.now`/`setTimeout` 于纯契约面为零）。
- `005`/`006`：时间值非权威；deadline 只作 escape（与 006 的 CausalAwait 联动红反例：到期返回 `Error WaitTimedOut`，不产生业务终态）。
- `007`：单次采样语义的纯计算部分（elapsed 由注入 clock 决定）。
- `008`：分居（导入 `./time` 纯契约面拿不到 VirtualTiming）。
- `src/wait/tests/001–009`（CAUSAL-WAIT-001..009）：核心红反例为卡内 §8 的四条——环检测（A↔B 出 `CausalWaitCycle`）、断边（等不活跃 owner 出 `BrokenCausalEdge`）、外部生产者前沿、`untilSignalOrDeadline` 到期清空活跃集 + 信号胜出后推进虚拟时钟不得再触发过期回调（timer 无残留）、一个取消不偷其他 waiter。
- 拆除测试：删除兄弟模块产物后 `time` 与 `wait` 各自独立编译、独立跑测试（零 EventStore、零进程 spawn、零 OpenCode SDK）。

### 4.6 主仓切换清单（库发布后执行，本轮未做）

1. `DurableWorkHost` 式接线：组合根（PluginBoot / ToolRuntimeScope / Change.Host / Fork.Host / Git.Hook.Sync 等）把 `NodeTiming.nodeTimerPort/nodeClockPort` 的构造来源改为 `wanxiangli/time` 导入；`CausalWaitProcess.local()` 改为库 `wait` 入口（或主仓薄包装）。
2. 删除主仓旧实现文件及其 fsproj（卡内 §9 主仓写集合 11 个文件 + 9 个 fsproj），`compile-order.txt` 与 Surface 登记同步更新。
3. `Process/Surface.fs`、`NodeProcessWait.fs`、`ProcessRunner.fs` 内的时间 import 更新由当轮集成者串行合入（CUT-02 已释放，非本卡编辑面冲突，仅次序协调）。
4. 主仓 `requirements/time-capability` 与 `requirements/causal-wait` 套件原样重跑——它们经主仓 Surface 消费，是切换后行为等价的证明。

## 5. DevOps 验证命令建议（本轮均未运行）

```sh
# wanxiangli 工作树就绪并落盘后：
npm ci --ignore-scripts
npm run build -- time && node --test src/time/tests/*.test.mjs
npm run build -- wait && node --test src/wait/tests/*.test.mjs
npm test
npm pack --json   # 发布负责人记录 SHA-256 与 commit

# 主仓接线后（独立工作树）：
node scripts/build.mjs
node --test requirements/time-capability/tests/ requirements/causal-wait/tests/
npm run format:check
npm run check

# 集成候选：
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node scripts/verify.mjs
node scripts/verify-package.mjs
```

## 6. D-03 遗留边界（本轮确认）

- 基础 pipe 留尾 ≠ 硬期限/完整外存/PTY：`Spool.fs`、`Pty*.fs` 归 CUT-20，本卡蓝图不包含它们。
- `NodeProcessWait` 的硬期限分段等待（消费 `Deadline.nextWaitMs`）语义留在 CUT-02 已释放面内，本卡只提供其依赖的 `Deadline` 纯计算。
- `SessionStartedAtProjection/Ledger` 留主仓会话层（卡内 C 项），仅消费注入 `IClockPort`。
- `Bridge.fs`（诊断文件落盘）留主仓：绑定主仓 `.wanxiangshu/diagnostics` 目录约定，不属可移植库职责。

## 7. 库侧实施记录（2026-10-15 第二轮，eng-cut21）

库仓工作树：`/home/kunweiz/Desktop/vibe/wanxiangshu-lifecycle-cutover/wanxiangli`（HEAD 46d9c07，含其他 Engineer 的 events/identity/fs/grounding/resources 等新模块）。**未修改 package.json、版本号、既有模块与共享文档**；构建脚本按 `src/*/` 自动发现 fsproj，无需登记即可 `npm run build -- time` / `-- wait`。

### 7.1 新增文件

`src/time/`：`Temporal.fs`（契约）、`Deadline.fs`（代数，含 `MaxTimerWaitMs`）、`NodeTiming.fs`（物理适配，本地内联 `trySetResult` 替代主仓 `AsyncSupport`）、`VirtualTiming.fs`（验证时基）、`Time.fs`（入口，导出 `Time`）、`Wanxiangli.Time.fsproj`、`index.d.ts`、`AGENTS.md`（撤回草稿"物理时钟在 host-omp"，先例为 process 模块撤回）、`tests/001–008.test.mjs`。

`src/wait/`：`CausalWait.fs`（契约 + 前沿算法 + 本地 `IDeadlineHandle` 声明）、`Registry.fs`（`CausalWaitRegistry` / `CausalWaitRuntime`，去掉主仓 `CausalWaitProcess` 单例）、`Await.fs`（`CausalAwait` 四函数）、`CompletionMailbox.fs`（泛型 `'agent/'pty'`）、`Wait.fs`（入口，导出 `Wait`，JS 面移植主仓 Surface 泛化子集）、`Wanxiangli.Wait.fsproj`、`index.d.ts`、`AGENTS.md`、`tests/001–009.test.mjs` + `support.mjs`。

### 7.2 相对蓝图的实际差异（均已在模块 AGENTS.md 登记）

1. **wait 零依赖 time**：蓝图原定 `wait → time` 仅契约依赖；实际按库仓惯例（assembly 声明 `Journal` 的先例）在 wait 本地声明结构化 `IDeadlineHandle`，运行时由组合者注入 time 实例（或测试替身），两模块各自独立编译。
2. **time-capability-008 分居降级**（明示范围缩减）：库仓单 bundle 形态下，物理/虚拟/契约的 fsproj 级分居降级为导出面分组（`virtual*` 标注验证专用）。编译级分居由主仓切换时保留主仓分片承担；拆独立验证模块需另行授权。
3. **CompletionMailbox JS 面**：泛型实例化为 `'obj/'obj'`，唤醒原因规范化为 `'CompletionMayBeAvailable' | 'MailboxCancelled' | { LocalInterrupt: reason }`；`WaitForSignal` 暴露为 JS 函数。
4. **测试 import 走 dist 相对路径**（`../../dist/{time,wait}/*.js`）：exports 未登记前 `wanxiangli/time` 不可解析；登记后无需改测试。

### 7.3 待协调者接线（导出登记建议）

`package.json` exports 增补（与现有六入口同模式）：

```json
"./time": { "types": "./src/time/index.d.ts", "import": "./dist/time/Time.js" },
"./wait": { "types": "./src/wait/index.d.ts", "import": "./dist/wait/Wait.js" }
```

`files` 白名单补 `src/time/index.d.ts`、`src/wait/index.d.ts`；`npm test` 脚本补 `src/time/tests/*.test.mjs`、`src/wait/tests/*.test.mjs`。发布与主仓集成按看板串行队列执行。

### 7.4 未做（等待验证与发布）

库侧测试与构建**均未运行**（Engineer 无执行权限）；主仓源码切换未做（等 tarball 发布）。验证命令见 §5。
