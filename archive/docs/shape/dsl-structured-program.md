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

## Semantic Vocabulary 所有权（落实 DSL-013 / DSL-014）

四层分工：

```text
Business CE          讲故事（Application workflow 入口与有界递归）
Semantic Vocabulary  给复杂时序一个领域名字与 law（DSL-013/014）
Port Decorator       给一次能力逐层增加 observation / normalization / physical policy（DSL-015）
Physical Adapter     真的碰 OpenCode / Git / process / timer（Infrastructure / Process）
```

落点约束：

- **Vocabulary 只允许住在 Application**（按 bounded context 的具名 module / public function）。不得把业务 Vocabulary 下沉到 Infrastructure tool adapter，也不得上提到 Domain 纯规则层。
- Domain 仍只拥有 `Evidence → Decision` / Projection 等纯函数；不得持有含时序压缩的 Vocabulary 实现。
- Session 只拥有 runtime single-flight 与物理资源；可以把 bare recovery CE 包一层 physical single-flight decorator，但不得拥有业务承诺名字的语义本体。
- Infrastructure / Process 只适配端口与物理实现；不得解释业务命令，不得持有 Manager / Reviewer / Finality 等生命周期 Vocabulary。

压缩（DSL-014）发生在 Application Vocabulary 内部：调用点只见承诺名字；被压缩时序的 proof 挂在该 Vocabulary 上，见 `proof/dsl-structured-program.md`。

## Decorator 与 Port 边界（落实 DSL-015）

```text
raw port
→ transparent decorators（可叠加）
→ 业务 CE / Vocabulary 调用
```

- **Transparent decorator**：不改变业务 trace 集；可在 composition root / Session / Infrastructure 适配处局部 module 叠加（如 causal observation、metrics、protocol/exception normalization）。
- **Semantic decorator**：改变业务 trace 集；必须本身是 Application Semantic Vocabulary，或在业务 CE 调用点具有明确语义名字。不得以匿名 middleware 管道吞掉 retry / fallback / recovery / dedupe / claim / deadline。
- **Port**：Application 只依赖具名 capability 形状；物理实现与 Host/OpenCode 细节留在 Infrastructure adapter。
- **Decorator 组合位置**：composition root 或明确的 port-wiring module；禁止引入全局 `DecoratorBase` / `MiddlewarePipeline` / IOC decorator 框架。

正式条款定义见 [`what/dsl-structured-program.md`](../what/dsl-structured-program.md)（DSL-013 / DSL-014 / DSL-015）。

