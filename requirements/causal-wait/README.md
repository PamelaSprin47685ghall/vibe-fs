# causal-wait

> 等待必须可诊断、可观测，但诊断观测不能升级为业务 authority。

一句话 WHY：**业务等待需要知道「正在等什么 / 为什么还没发生」，但诊断这一等待不能反过来成为 durable business fact、prompt authority 或决策真相源——观察可以看程序，程序绝不可以看观察。**

## 本包保证什么

- 跨业务 owner、跨 Host turn、跨 provider attempt、跨 physical capability 的等待，都能生成 **process-local 诊断观测**（谁在等、等什么、谁有资格满足、终止路径是什么）。
- 观测**只描述依赖与进展**：不得决定业务分支、不得 mint permit、不得写 Journal、不得用于 recovery/dedupe、不得影响 PromptAuthority / Finality / Reviewer / Manager 决策。
- **event-driven wake 优先于 polling**：等待由实际依赖解除，不是 wall-clock 运气。
- 取消 / 完成后，观测生命周期**终止**，不能复活业务机会。
- 诊断输出有**最小未满足因果前沿**（frontier），一次 dump 回答「卡在哪里、为什么」。

## 世界什么时候 RED

- 系统只能靠盲轮询 / sleep 理解等待（无法回答「在等什么」）。
- 诊断状态被升级成可改变业务结果的事实（写 Journal、进 prompt、驱动决策）。
- 等待被取消后，旧的观测仍被视为「仍在等」，复活已终止的业务机会。
- Application 业务代码持有 `IWaitSnapshotReader`，或 Domain 引用 CausalWait 实现。

## 不归本包

- 时间 capability（clock/timer/deadline 注入）→ `time-capability`。
- 业务流程由语言结构表达 → `structured-workflow`。
- 某个具体 reviewer / process / session 的等待条件 → 各业务 owner（`review-assurance`、`delegation`、`process-execution` 等）。
- crash recovery；process-local 观测可在重启后安全消失 → `crash-reconciliation`。
- Host snapshot 的业务事实定义 → `host-boundary`。

## HOW 概览（实现模型）

| 概念 | 实现 | 说明 |
|---|---|---|
| 诊断词汇 | `Kernel/CausalWait.fs` | `CausalOwnerRef` / `CausalProducerRef` / `WaitEscape` / `DiagnosticWait` / exit / transition / snapshot / frontier |
| 注册表 | `Session/CausalWaitRegistry.fs` | process-local：active dict + 有界 ring buffer（默认 256）+ 单调 sequence；`IWaitObserver`（Enter）与 `IWaitSnapshotReader`（Snapshot）类型隔离 |
| await 括弧 | `Session/CausalAwait.fs` | `awaitTask` / `awaitUnit` / `race` / `untilSignalOrDeadline`：enter → await → resolve\|cancel\|fail → leave；无 slice timer / 无轮询间隔 / 无 UtcNow 循环 |
| 诊断桥 | `Session/CausalWaitBridge.fs` | Scheme B：`<workspace>/.wanxiangshu/diagnostics/causal-waits.json`，git-excluded、best-effort、业务不可读 |
| 静态边界 | `scripts/checks/causal-wait-boundary.mjs` | Domain 不引用 CausalWait；Application 不持有 reader；Fact/Journal codec 不编码 CausalWait；诊断不进 Prompt/decision 路径 |

## proof 概览

`requirements/causal-wait/tests/`：

- `causal-wait.test.mjs`（MOVE 自 `tests/unit/kernel/`）— 注册表/await 括弧/hub 契约（RED-1..4、history、RED-8）。
- `causal-frontier.test.mjs`（MOVE 自 `tests/unit/kernel/`）— frontier 纯诊断算法（RED-5..7、empty）。
- `wait-lifecycle.test.mjs`（NEW）— 取消/完成后观测终止、Dispose 默认 WaitDisposed、MarkExit 幂等、默认容量 256、observer 无 Snapshot 面。
- `escape-taxonomy.test.mjs`（NEW）— WaitEscape 五 case 与诊断渲染 tag 全区分（CCE-005 显式终止路径可见）。

另有 REUSE：`tests/unit/session/causal-wait-bridge.test.mjs`（bridge 文件 + E2E 诊断格式化，含 verification-system MECHANISM）、`tests/unit/temporal/until-signal-or-deadline.test.mjs`（event-driven 词汇）、`scripts/checks/causal-wait-boundary.mjs`（静态门，经 check.mjs 运行）。

## 阅读顺序

1. `WHY.md` — 为什么这个包必须独立存在（失败模式考古）。
2. `WHAT.md` — 唯一 normative 合同（编号命题）。
3. `HOW.md` — 实现模型、类型隔离、历史与弃权。
4. `PROOF.md` — 每条命题的测试落点与运行命令。

## DEPENDS ON

无 hard 产品依赖（`requirements-design/INDEX.md` 依赖骨架 Phase E 结论）：wait 的 deadline 是**可选 escape**（需要时消费 `time-capability`），event-driven wake **不依赖** `structured-workflow`。
