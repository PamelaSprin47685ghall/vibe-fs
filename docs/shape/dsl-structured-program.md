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

- `NodeProcessWait`：进程等待生命周期拆分为 `awaitExitOrDeadline`/`killAndAwaitAcknowledgement`/`waitForProcess`。
- `BloggerRuntime`：单一入口 `onMainMaterial`；`BloggerRuntimeState` 只有 `InFlight`（payload 即 CurrentRequest 唯一权威）与 `Idle` 二态；「有 parked waiter」由 `IParkedTransformHost.HasParked` 物理事实经 `onMaterial` 显式 `hasParkedWaiter` 参数传入；`Sealed` 由 durable projection 查询表示；无 `Parked`/`Disposed` case。
- `Companion`：恢复槽是一次性物理信号，`ArmRecoverySlot`（真实失败置位）/`IsRecoveryArmed`（squash 决策查询）/`DisarmRecoverySlot`（squash 启动清位）；无 `TaskCompletionSource` waiter、无 `TryConsumeRecoverySlot`。
- `AgentFact`：拆分为 7 个 bounded-context family（`PromptFactCases` / `ReviewFactCases` / `ExecutionFactCases` / `OrchestratorFactCases` / `CompanionFactCases` 等），`AgentFact` 为 7-case 分派联合，外层 `match` 一次分派后进入 family 纯 fold，不构造解释器。

## DSL-010：Host 边界白名单

新增 `open Wanxiangshu.Process` 或 `open Wanxiangshu.Infrastructure` 的文件必须先登记 `dsl-ownership.mjs` 的 `HOST_BOUNDARY_OPEN_BASENAMES`，否则 `dsl-ownership` fail-closed。

## DSL-011：测试可见面

新增契约面必须先在 `tests/unit/support/domain.mjs` 开出口，再写 mjs 测试。禁止为测试可见性新增生产 export。
