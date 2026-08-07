# DSL 结构化程序规则 — 边界

行为不变量见 `what/dsl-structured-program.md`。

## DSL-008：分层所有权

```text
Domain       纯规则 / Evidence / Decision / Projection
Application  CE workflow / 直接调用端口
Session      运行时 single-flight 所有权 / 物理 TCS / Dictionary
Process      进程/PTY 生命周期，F# CE 直接表达等待与超时
Infrastructure Host hooks / codec / resource — 不解释业务命令
```

`Domain` 不得引用 `Fable.Core.JsInterop`，不得 `open Wanxiangshu.Infrastructure`、`Wanxiangshu.OpenCode`、`Wanxiangshu.Process`。

## DSL-009：模块与职责

- `NodeProcessWait`：进程等待生命周期拆分为 `awaitExitOrDeadline`/`awaitKillAcknowledgement`/`waitForExit`；`waitForSignal` 以三态 `WaitSignal = ProcessExited | TimerElapsed | Cancelled` 区分自然退出/业务超时/取消，`Cancelled` 绝不解释为退出。
- `BloggerRuntime`：单一入口 `onMainMaterial`；生产 busy / CurrentRequest 权威 = 物理 flight registry（`IParkedTransformHost.HasFlight` / `TryGetFlight` / `bloggerFlights`）。`BloggerRuntimeState`（`Idle`/`InFlight`）为 **PR7 PARTIAL dual-write shadow**，transition API 仍可能读写 `cell.State` 以兼容，**不得**将其视为 CurrentRequest 唯一权威（`InFlight` payload 不是 CurrentRequest 唯一权威）；「有 parked waiter」由 `IParkedTransformHost.HasParked` 物理事实经 `onMaterial` 显式 `hasParkedWaiter` 参数传入；`Sealed` 由 durable projection 查询表示；无 `Parked`/`Disposed` case。
- `Companion`：恢复槽是一次性物理 waiter，`recoveryWaiter: TaskCompletionSource<unit> option`（`// DSL-MUTABLE: resource`）；机会 = waiter 未完成。`StartRecoveryOpportunity`（真实失败后注册，复用未消费 waiter）启动恢复 Task，`OfferRecoveryMaterial`（material 边界唤醒并消费一次，未注册即 no-op）驱动它；重启留 `None`。诚实注：X 侧 `PluginRuntimeScope.RecoveryArming: Dictionary<_, SlotArming>` 仍存在，属 session 级 attempt/XWire 证据路径，**不是** Companion Y 侧 Armed PC，勿混写。
- `AgentFact`：拆分为 7 个 bounded-context family（`PromptFactCases` / `ReviewFactCases` / `ExecutionFactCases` / `OrchestratorFactCases` / `CompanionFactCases` 等），`AgentFact` 为 7-case 分派联合，外层 `match` 一次分派后进入 family 纯 fold，不构造解释器。

## DSL-010：Host 边界白名单

新增 `open Wanxiangshu.Process` 或 `open Wanxiangshu.Infrastructure` 的文件必须先登记 `dsl-ownership.mjs` 的 `HOST_BOUNDARY_OPEN_BASENAMES`，否则 `dsl-ownership` fail-closed。

## DSL-011：测试可见面

新增契约面必须先在 `tests/unit/support/domain.mjs` 开出口，再写 mjs 测试。禁止为测试可见性新增生产 export。
