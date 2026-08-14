# time-capability

> 时间与等待的物理能力必须显式进入系统，不能由 ambient clock/timer 偷渡业务判断。

一句话 WHY：**时间若从 ambient wall clock / 全局 timer 偷渡业务代码，同一事实在不同运行时刻会产生不同判断，proof 与 replay 都失去确定性。**

## 本包保证什么

- 时钟（clock）与定时器（timer）是**显式注入的 capability**，业务层拿到的永远是注入的 port，不是全局 `UtcNow`。
- 截止时间（deadline）与已耗时间（elapsed）有 **typed 表达**，不散落为裸 `DateTime` 加临时比较。
- 时间可**虚拟化**：测试用虚拟时钟/虚拟 timer 替换物理时钟，proof 确定、可重放、无 wall-clock 偶然性。
- Domain / Application / Session 不直接读 ambient `UtcNow` 或全局 timer。
- **时间值本身不是 authority**：同一个时刻值不携带业务意义，只有消费它的领域规则 + 注入时钟决定判断。

## 世界什么时候 RED

- 业务结果可能仅因 wall-clock 环境、测试运行速度或隐藏 timer 不同而改变。
- 某个业务判断直接读了 `DateTimeOffset.UtcNow` / `Date.now` / `setTimeout` 而不是注入的 `IClockPort` / `ITimerPort`。
- deadline 在业务代码里退化成裸时刻 + 手写比较，且无法在测试中推进时间。
- 同一段逻辑在同一虚拟输入下，两次运行给出不同答案（时间成了隐藏的随机源）。

## 不归本包

- 等待的业务因果关系 → `causal-wait`。
- 超时预算的具体数值（如 DevOps 10s）与 deadline 的业务意义 → `process-execution` / `delegation` 等消费方。
- 等待语义、event-driven vs polling → `causal-wait`。
- 静态禁 ambient 的 gate 机制本身 → `structured-workflow`（`g4r-ce-vocabulary.mjs` 的 RAW_TIME 扫描）与 `verification-system`。

## HOW 概览（实现模型）

| 概念 | 实现 | 说明 |
|---|---|---|
| 时钟 port | `Kernel/Temporal.fs` 的 `IClockPort.UtcNow` | 注入式墙钟；物理适配器在 `Process/PtyTiming.fs` |
| 定时器 port | `ITimerPort.Delay(ms)` → `IDeadlineHandle` | 一次可取消；`Cancel` 后 `Delay` 永久 pending |
| 虚拟时间 | `PtyTiming.createVirtualTimerPort` / `createVirtualClockPort` | 测试推进时间，回调在截止点精确触发 |
| typed deadline | `Process/Deadline.fs`（私有构造 + `ofBudget`/`remaining`/`isExpired`/`nextWaitMs`） | 有界、防溢出、JS timer 上限分段 |
| 消费示例 | `Process/ProcessRunner.fs`、`Session/CompletionMailbox.fs` | deadline 从注入 clock + budget 计算，不读 ambient |

## proof 概览

`requirements/time-capability/tests/`：

- `timer-port.test.mjs`（MOVE 自 `tests/unit/execution/`）— ITimerPort 虚拟 timer 契约（到点触发 / cancel / dispose / 独立 handle）。
- `deadline-typed.test.mjs`（NEW）— Deadline 有界、防溢出、`nextWaitMs` 上限、判定只随注入时钟变化。
- `clock-port-virtual.test.mjs`（NEW）— IClockPort 虚拟时钟推进/设定，多消费者相互独立，无 ambient。

另有 REUSE 落点：`tests/unit/execution/devops-join-timeout.test.mjs`、`tests/unit/process/**`（EXEC-011）、`tests/unit/temporal/**`、`tests/unit/verify/g4r-ce-vocabulary.test.mjs`（ambient 静态扫描）。

## 阅读顺序

1. `WHY.md` — 为什么这个包必须独立存在（失败模式考古）。
2. `WHAT.md` — 唯一 normative 合同（编号命题）。
3. `HOW.md` — 实现模型、物理适配器边界、历史与弃权。
4. `PROOF.md` — 每条命题的测试落点与运行命令。

## DEPENDS ON

无。本包不依赖任何其它 package 的 guarantee（`requirements-design/INDEX.md` 依赖骨架 Phase E 结论）。
