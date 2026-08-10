# DSL 结构化程序规则 — 边界

行为不变量见 `what/dsl-structured-program.md`。

## DSL-008：分层所有权

```text
Domain         纯规则 / Evidence / Decision / Projection
Application    CE workflow / 直接调用端口
Session        运行时 single-flight 与物理资源所有权
Process        进程/PTY 生命周期
Infrastructure Host hooks / codec / resource adapter
```

`Domain` 不得引用 Infrastructure、OpenCode、Process 或 `Fable.Core.JsInterop`。
Infrastructure 只适配外部协议，不解释业务命令。

## DSL-009：模块与职责

- `NodeProcessWait` 拥有进程退出、deadline、取消和 kill acknowledgement 的完整等待作用域。
- `BloggerRuntime` 的 material 协调入口是 `onMainMaterial`；物理 flight registry 拥有 busy 与 current request。
- `Companion` 恢复机会由一次性物理 waiter 拥有；durable evidence 与 waiter 生命周期分离。
- record `mutable` 与 `ref` 字段只可属于明确的 Session/Process 物理资源；Domain/Application 一律拒绝，未标注 physical owner 的 record 一律拒绝。
- `SessionRecovery` 从 Journal evidence 派生 permit，再重入普通 workflow。
- `AgentFact` 只负责 bounded-context family 分派；各 family 的纯 fold 拥有本域投影。

不得为了兼容旧流程位置而建立第二个 writer。工作范围可以由 Active Change 限定，目标
语义仍只由正式条款定义。

## DSL-010：Host 边界白名单

新增业务文件需要打开 Host/Process/Infrastructure 命名空间时，必须先在
`scripts/checks/dsl-ownership.mjs` 登记边界 basename；未登记时 fail closed。

## DSL-011：测试可见面

测试对 Fable 产物形状的适配只属于 `tests/unit/support/domain.mjs`。
新增公共契约先在该 facade 开口；不得仅为测试便利新增生产 export。

## 因果 wait 观测边界（落实 DSL-012）

因果 wait observation 属于 Session/Infrastructure 诊断面：

- Domain / Application decision 不得读取 wait snapshot；
- Journal / Fact codec 不得编码 CausalWait；
- 静态边界：`scripts/checks/causal-wait-boundary.mjs`。

正式条款定义见 [`what/dsl-structured-program.md`](../what/dsl-structured-program.md)（DSL-012）。

