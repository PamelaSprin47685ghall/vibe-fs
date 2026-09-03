# time-capability — WHAT

本文件是 `time-capability` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## TIME-001: 时钟与定时器是显式注入的 capability

业务代码（Domain / Application / Session 层）严禁依赖隐式全局时间原语。凡需要获取当前时刻或安排异步延迟的组件，必须通过显式注入的能力接口实现：获取当前时刻使用 `IClockPort.UtcNow()`，安排延时使用 `ITimerPort.Delay(milliseconds)`（返回强类型的 `IDeadlineHandle`）。

## TIME-002: deadline 与 elapsed 有 typed 表达，不散落为裸时刻比较

所有截止时间必须由强类型 `Deadline`（私有构造，仅能通过 `Deadline.ofBudget now budget` 创建）封装表达，并通过 `Deadline.remaining`、`Deadline.isExpired` 与 `Deadline.nextWaitMs` 等纯函数由注入时钟驱动消费。业务代码严禁持有裸 `DateTimeOffset` 进行手工超时比对，防止时间戳溢出与时区错位。

## TIME-003: 时间可虚拟化；测试用虚拟实现替换物理时钟与 timer

时间系统必须提供确定性的虚拟实现（`createVirtualTimerPort` 与 `createVirtualClockPort`）。虚拟定时器在调用 `Advance` 跨越截止点时精确触发回调，支持取消与清理；虚拟时钟支持从固定时间原点进行确定性的离散时间推进与设置，确保时序证明完全可重放且独立于物理墙钟。

## TIME-004: Domain / Application / Session 禁止直接读 ambient 时间

`src/Wanxiangshu/` 下属于 Domain、Application 与 Session 层的源码文件中，严禁出现任何未经授权的全局时间符号（包括 `DateTimeOffset.UtcNow`、`DateTime.Now`、`Date.now`、`setTimeout`、`timerTask`），底层物理适配器层的例外必须通过静态门禁白名单进行显式声明。

## TIME-005: 时间值本身不是 authority；只有消费它的领域规则 + 注入时钟决定意义

时刻值与截止时间本身不具备业务裁决权。相同的截止时间由不同的领域规则消费时将产生不同的业务决策；判定必须由「领域规则 + 显式注入的时钟」共同计算得出，严禁使时间值自身成为独立驱动状态转移的权威。

## TIME-006: deadline 是 causal-wait 的可选 escape

截止时间作为因果等待（`causal-wait`）的可选终止逃逸路径（`WaitEscape.DeadlineAt`）存在。本包独立提供强类型时刻能力，供因果等待机制在需要有界超时时进行消费，两者之间不存在双向硬依赖。

## TIME-007: HOST-013 的 SessionStartedAt 绑定首次 prompt，一次采样形成新 marker 的 elapsed

每个面向 Provider 的 Session 的起始时刻 `SessionStartedAt`，严格定义为该 Session 首次开始构建或发送 prompt 时，由显式注入的 `IClockPort.UtcNow()` 采样获得的时刻，并执行持久化单次绑定（bind-once）。后续每次新 occurrence 再从同一时钟采样当前时间计算经过时长并生成人类可读片段，随当前 MarkerText 固化持久化，严禁在重试或重启时重置时间原点。

## TIME-008: temporal vocabulary、capability、adapter与projection必须分居

pure clock/timer capability与pure `Deadline`分别形成无physical value/factory的contract；Node clock/timer implementation位于唯一adapter，virtual verification implementation独立且不得进入production authority。`SessionStartedAt`是独立owner projection，不得与clock implementation同居。runtime只消费composition注入的mandatory capability；direct `Date.now`、`setTimeout`、ambient fallback与contract→adapter反向closure一律拒绝。
