# time-capability — HOW

## 架构与实现机制

`time-capability` 通过抽象接口定义、强类型封装、物理与虚拟端口实现及静态门禁共同保证时间能力的可注入性与确定性：

### 1. 纯接口契约（`Kernel/Temporal.fs`）

定义无外部依赖的纯时间接口：
- `IClockPort`：抽象时刻获取能力，提供 `UtcNow()` 接口。
- `ITimerPort`：抽象延迟安排能力，提供 `Delay(milliseconds)` 接口并返回支持取消的 `IDeadlineHandle`。
- `IDeadlineHandle`：包含异步等待任务 `Delay` 与幂等取消操作 `Cancel()`，取消后任务永久处于 pending 状态，杜绝取消后意外唤醒。

### 2. 强类型截止时间（`Process/Deadline.fs`）

- 私有构造 `Deadline of expiresAt`，强制只能通过 `Deadline.ofBudget now budget` 创建，内部自动处理最大值溢出截断。
- 纯函数计算集合：`remaining clock d`（计算剩余毫秒并钳制非负值）、`isExpired clock d`（判定是否超时）、`nextWaitMs clock d`（针对 JS 定时器 Int32 上限进行 `0x7FFFFFFF` 分段封顶处理）。

### 3. 物理与虚拟端口实现（`Process/PtyTiming.fs`）

- **生产物理端口**：`nodeClockPort` 封装真实系统时钟；`nodeTimerPort` 封装 Node.js `setTimeout`，对长预算（≥1000ms）自动执行 `.unref()`，防止定时器非法持有事件循环导致进程无法正常退出。
- **测试虚拟端口**：`createVirtualClockPort` 提供可自由设置与离散推进的基准时钟；`createVirtualTimerPort` 提供无真实休眠的虚拟定时器，通过 `Advance(ms)` 精确触发已到期任务。

### 4. 业务层全局时间禁令门禁（`ambient-time-forbidden`）

`tests/ambient-time-forbidden.test.mjs` 与共享静态门禁对 Domain、Application、Session 层进行全量扫描，严禁在业务代码中出现任何原生全局时间符号，确保时间消费点全部经过显式端口注入。

### 5. 会话时间原点单次绑定（`SessionStartedAt`）

通过 `SessionStartedAtLedger` 与 `SessionStartedAtProjection` 机制，在会话首次生成 prompt 时同步采样注入时钟并追加持久化事件，后续重启通过内存投影 O(1) 获取固定原点，确保会话经过时长（elapsed）的确定性与不可变性。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| TIME-001 | `requirements/time-capability/tests/timer-port.test.mjs` |
| TIME-002 | `requirements/time-capability/tests/deadline-typed.test.mjs` |
| TIME-003 | `requirements/time-capability/tests/timer-port.test.mjs` |
| TIME-004 | `requirements/time-capability/tests/ambient-time-forbidden.test.mjs` |
| TIME-005 | `requirements/time-capability/tests/deadline-typed.test.mjs` |
| TIME-006 | `requirements/time-capability/tests/until-signal-or-deadline.test.mjs` |
| TIME-007 | `requirements/time-capability/tests/pair-session-elapsed.test.mjs` |
