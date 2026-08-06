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

- `NodeProcessWait`：进程等待生命周期拆分为 `awaitExitOrDeadline``killAndAwaitAcknowledgement``waitForProcess`。
- `BloggerRuntime`：单一入口 `onMainMaterial`，内部无 `BloggerRuntimeState` 状态 DU；`InFlight` 由单一 Task 表示，`Parked` 由 `let!` 等待表示，`Sealed` 由 durable projection 查询表示，`Disposed` 由 owner cancellation 表示。
- `Companion`：不再暴露 `ArmRecoverySlot/DisarmRecoverySlot/IsRecoveryArmed`；失败路径启动一次性 `runRecoveryOpportunity` CE，由 `TaskCompletionSource` 等待下一份材料。
- `AgentFact`：按 bounded context 拆分为 `PromptFact` / `ReviewFact` / `ExecutionFact` / `OrchestratorFact` / `CompanionFact`，外层只做 `match fact with | Prompt p -> ...` 一次分派，不构造解释器。

## DSL-010：Host 边界白名单

新增 `open Wanxiangshu.Process` 或 `open Wanxiangshu.Infrastructure` 的文件必须先登记 `dsl-ownership.mjs` 的 `HOST_BOUNDARY_OPEN_BASENAMES`，否则 `dsl-ownership` fail-closed。

## DSL-011：测试可见面

新增契约面必须先在 `tests/unit/support/domain.mjs` 开出口，再写 mjs 测试。禁止为测试可见性新增生产 export。
