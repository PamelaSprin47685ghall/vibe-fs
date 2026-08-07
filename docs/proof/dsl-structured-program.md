# DSL 结构化程序规则 — 证明

行为见 `what/dsl-structured-program.md`；边界见 `shape/dsl-structured-program.md`；实现见 `how/dsl-structured-program.md`。

## 静态门禁

| 门 | 要求 |
|---|---|
| `scripts/checks/dsl-ownership.mjs --threshold=0` | 语义状态机、组合状态、多布尔循环、大 DU 分类、重复 case 集 |
| `npm run lint` | format + spec + architecture + dsl-ownership + p0-recovery-join |
| 架构检查 `architecture.mjs` | 无旧路径、fsproj 完整、无 `.gen.fs`、Domain 不引用上层 |

## 动态证明

| 层 | 证明什么 | 落点 |
|---|---|---|
| 单元 | 新 CE 行为等价于旧状态机 | `tests/unit/execution/process-wait.test.mjs` **已新增**——四行为测试 A–D：A 进程先退出（返回真实 code、不 Kill）；B deadline 先到（Kill 恰好一次、随后 exit、`TimedOut=true`）；C kill 后一直无 exit（有限时间结束、`ExitCode=-1`、`TimedOut=true`）；D **等待中 cancellation**（Kill 恰好一次、以 `OperationCanceledException` 结束、不无限等 `Exit.Task`）。其余：`tests/unit/enforcer/blogger-runtime.test.mjs` 扩展；`tests/unit/context/session-recovery.test.mjs` 扩展；`tests/unit/context/companion-projection.test.mjs` 扩展 |
| 集成 | Journal fold / projection 不变 | `tests/integration/harness/cases.mjs` 中对应 case |
| Canary | Host 真实行为不变 | `tests/e2e/cases/process-stress.test.mjs`；`tests/e2e/cases/companion.test.mjs`；`tests/e2e/cases/blogger-quiet-stop.test.mjs`；`tests/e2e/cases/manager-companion.test.mjs` |
| 门禁自身 | 故意破坏门禁应变红 | 在 `tests/unit/verify/dsl-ownership.test.mjs` 增加改名 canary 与组合状态 fixture |

## 完成定义（PARTIAL — gap 仍开）

> **当前状态（2026-08-07）**：本项为 **PARTIAL**，仍有阻断项未落地，不得作为 release evidence。下列完成标准中仅一部分满足。

已满足：

- 生产路径无重复 `TurnOutcome` / `Role`（已收敛到 `ReconcileProgram.TurnOutcome` 与 `Kernel.Role`）。
- `AgentFact` 已完成阶段 A 分 family 所有权（7 个 bounded-context family + 顶层分派）。
- **`NodeProcessWait` cancellation 三态已落地**：`waitForSignal` 以 `WaitSignal = ProcessExited | TimerElapsed | Cancelled` 三态区分三事件；`awaitExitOrDeadline` 把 `Cancelled` 解释为 `WaitCancelled` 而非 `ProcessExited`；顶层 `waitForExit` 对 `WaitCancelled` 显式 `child.Kill()` 并传播 `OperationCanceledException`。`tests/unit/execution/process-wait.test.mjs` 已新增 A–D 四行为测试（含 mid-wait cancellation）。
- **`RecoveryArming` 程序计数器已落地为 TCS waiter**：`Session/Companion.fs` 不再有 `RecoveryArming`（`NotArmed`/`Armed`）DU 与 `let mutable arming`，改为 `recoveryWaiter: TaskCompletionSource<unit> option`（`// DSL-MUTABLE: resource`）；`StartRecoveryOpportunity` 注册一次性物理 waiter，`OfferRecoveryMaterial` 唤醒一次，重启留 `None`。`ArmRecoverySlot`/`IsRecoveryArmed`/`DisarmRecoverySlot` 已删除。机会存在性由 Task 存活与否承载，不再写业务状态。
- **`dsl-ownership` 门禁已收紧（2026-08-07）**：`mutable` 由目录整体豁免改为声明式豁免（前 1–2 行须 `// DSL-MUTABLE: <category>`，Domain/Session/Application/Process/`Kernel/Parallel.fs`；Agent/其余 Kernel fail-closed）；`Process/` 不再整体豁免 `bool-loop`；`dup-cases` 改为跨文件全局比对；`scanLargeDus` 由 report-only 改为 CI fail（≥10 case 无 `/// DSL-class:` 即红）；`/// DSL-class: ControlState` 判 `program-counter`。

仍阻断 / 未满足：

- **生产路径仍有 `BloggerRuntimeState` 状态 DU**（`Idle`/`InFlight` 仍在 `Session/BloggerRuntimeState.fs`，经 `BloggerRuntimeCell` 由 `Dictionary<string, BloggerRuntimeCell>` 持有）。已引入 `bloggerFlights` 物理 flight ownership（`// DSL-MUTABLE: single-flight`，`HasFlight`/`TryGetFlight` 优先作 busy 判据），但 `BloggerRuntimeState.InFlight` 仍作双写 shadow 保留以维持 transition-cell 兼容。完成定义要求「生产路径无 `BloggerRuntimeState` 状态 DU」— **未满足 / PARTIAL**。

未核验：

- `npm run check:release` 全绿 — 本任务未运行，不作为完成证据。
