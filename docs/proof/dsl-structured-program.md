# DSL 结构化程序规则 — 证明

行为见 `what/dsl-structured-program.md`；边界见 `shape/dsl-structured-program.md`；实现见 `how/dsl-structured-program.md`。

## 静态门禁

| 门 | 要求 |
|---|---|
| `scripts/checks/dsl-ownership.mjs --threshold=0` | 声明式 `DSL-MUTABLE`、跨文件 `dup-cases`、large-DU（≥10 case 无 `/// DSL-class:`）fail、`/// DSL-class: ControlState` 硬判 `program-counter`、Process 不再整目录豁免 `bool-loop`。组合状态结构检测 **Reject**（不实施；存量 `BloggerRuntimeCell` 已删，靠 ControlState 硬判 + review） |
| `npm run lint` | format + spec + architecture + dsl-ownership + p0-recovery-join |
| 架构检查 `architecture.mjs` | 无旧路径、fsproj 完整、无 `.gen.fs`、Domain 不引用上层 |

## 动态证明

| 层 | 证明什么 | 落点 |
|---|---|---|
| 单元 | 新 CE 行为等价于旧状态机 | `tests/unit/execution/process-wait.test.mjs` **已新增**——四行为测试 A–D：A 进程先退出（返回真实 code、不 Kill）；B deadline 先到（Kill 恰好一次、随后 exit、`TimedOut=true`）；C kill 后一直无 exit（有限时间结束、`ExitCode=-1`、`TimedOut=true`）；D **等待中 cancellation**（Kill 恰好一次、以 `OperationCanceledException` 结束、不无限等 `Exit.Task`）。其余：`tests/unit/enforcer/blogger-runtime.test.mjs` 扩展；`tests/unit/context/session-recovery.test.mjs` 扩展；`tests/unit/context/companion-projection.test.mjs` 扩展 |
| 集成 | Journal fold / projection 不变 | `tests/integration/harness/cases.mjs` 中对应 case |
| Canary | Host 真实行为不变 | `tests/e2e/cases/process-stress.test.mjs`；`tests/e2e/cases/companion.test.mjs`；`tests/e2e/cases/blogger-quiet-stop.test.mjs`；`tests/e2e/cases/manager-companion.test.mjs` |
| 门禁自身 | 故意破坏门禁应变红 | `tests/unit/verify/dsl-ownership.test.mjs` 含声明式 mutable / 跨文件 dup-cases / 门禁注册门用例（已落地）。组合状态结构检测 Reject，无对应 fixture；改名/声明类 canary 保留 |
| Blogger 物理所有权 | busy = `HasFlight`；无 State DU | `tests/unit/enforcer/blogger-convergence-gaps.test.mjs`（C0 物理权威）、`blogger-crash-recovery.test.mjs`（C5）、`blogger-runtime` / `blogger-seal-reactivate` / `enforcer-cycle-protocol` 等物理 Slot 断言 |

## 完成定义（CLOSED / 已落地）

> **当前状态（2026-08-07）**：PR 1–9 **全部闭环**。生产路径无 `BloggerRuntimeState` / `BloggerRuntimeCell` / dual-write shadow；busy 权威 = `bloggerFlights`（`HasFlight`）。proposal 标 **CLOSED**，可作为 release evidence。验证：`npm run lint` + `build` + unit 1000 + integration 271 绿（`check:release` / canary 未在本闭环强制）。

已满足：

- 生产路径无重复 `TurnOutcome` / `Role`（已收敛到 `ReconcileProgram.TurnOutcome` 与 `Kernel.Role`）。
- `AgentFact` 已完成阶段 A 分 family 所有权（7 个 bounded-context family + 顶层分派）。
- **`NodeProcessWait` cancellation 三态已落地**：`WaitSignal = ProcessExited | TimerElapsed | Cancelled`；`process-wait.test.mjs` A–D。
- **`RecoveryArming` → TCS waiter**：`Companion.fs` 的 `recoveryWaiter`（`// DSL-MUTABLE: resource`）；`StartRecoveryOpportunity` / `OfferRecoveryMaterial`。
- **`BloggerRuntime` 物理所有权（PR 7）**：删除 `BloggerRuntimeState` DU / `BloggerRuntimeCell` / transition API；`bloggerFlights` + `drainWindows` + `decideMaterial`；测试断言迁物理 Slot。
- **`dsl-ownership` 门禁已收紧**：声明式 `DSL-MUTABLE`；`Process/` 不豁免 `bool-loop`；跨文件 `dup-cases`；large-DU CI fail；`ControlState` 硬判。PR 9 第 3 项 Reject / 第 5 项 Closed-as-forbidden。
