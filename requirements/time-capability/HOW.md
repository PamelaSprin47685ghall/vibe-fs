# time-capability — HOW

## 架构与实现机制

`time-capability` 通过抽象接口定义、强类型封装、物理与虚拟端口实现及静态门禁共同保证时间能力的可注入性与确定性：

### 1. 纯接口契约（`foundation-temporal-contract`）

定义无外部依赖的纯时间接口：
- `IClockPort`：抽象时刻获取能力，提供 `UtcNow()` 接口。
- `ITimerPort`：抽象延迟安排能力，提供 `Delay(milliseconds)` 接口并返回支持取消的 `IDeadlineHandle`。
- `IDeadlineHandle`：包含异步等待任务 `Delay` 与幂等取消操作 `Cancel()`，取消后任务永久处于 pending 状态，杜绝取消后意外唤醒。

`Foundation/Temporal.{fsi,fs}`独占`Wanxiangshu.Owner.time-capability.foundation-temporal-contract.fsproj`。该project零依赖，只发布capability type，不发布实现值或factory。

### 2. 强类型截止时间（`process-deadline-contract`）

- 私有构造 `Deadline of expiresAt`，强制只能通过 `Deadline.ofBudget now budget` 创建，内部自动处理最大值溢出截断。
- 纯函数计算集合：`remaining clock d`（计算剩余毫秒并钳制非负值）、`isExpired clock d`（判定是否超时）、`nextWaitMs clock d`（针对 JS 定时器 Int32 上限进行 `0x7FFFFFFF` 分段封顶处理）。

`Process/Deadline.{fsi,fs}`独占`Wanxiangshu.Owner.time-capability.process-deadline-contract.fsproj`。它不依赖timer implementation；`DeadlineSurface`是其representation consumer，不与contract同居。

### 3. Node物理端口（`process-node-timing-adapter`）

`nodeClockPort`封装真实系统时钟；`nodeTimerPort`封装Node.js `setTimeout`，对长预算（≥1000ms）自动执行`.unref()`，防止定时器非法持有事件循环导致进程无法正常退出。

`Process/NodeTiming.{fsi,fs}`独占adapter project，只消费clock/timer capability contract与所需bounded task primitive。ordinary production consumer若直接构造Node capability，必须显式引用该adapter；它不因获得capability type而自动获得实现。

### 4. Virtual verification implementation（`process-virtual-timing`）

`createVirtualClockPort`提供可自由设置与离散推进的基准时钟；`createVirtualTimerPort`提供无真实休眠的虚拟定时器，通过`Advance(ms)`精确触发已到期任务。`Process/VirtualTiming.{fsi,fs}`独占verification runtime project；Node adapter不编译该source，ordinary production consumer不引用该project。

### 5. Production-bound representation Surface（`foundation-temporal`）

`Process/DeadlineSurface.{fsi,fs}`与`Process/Surface.{fsi,fs}`独占composition project，为注册semantic surface显式消费实际投影所需的contract、Node adapter与virtual implementation。它不承载任何时间公式或factory实现，不为业务consumer提供compat facade。

### 6. 业务层全局时间禁令门禁（`ambient-time-forbidden`）

`scripts/checks/g4r-ce-vocabulary.mjs` 先遍历完整 `src/Wanxiangshu/`，再按精确文件路径排除已审查的物理时钟、timer、Host 与持久化适配器。例外不接受目录前缀；新 sibling 默认进入扫描。生产根或声明的 scan root 不存在时 collector 直接失败，CLI 永久 hard-fail，不存在 soft phase。

`tests/ambient-time-forbidden.test.mjs` 调用与 CLI 相同的 analyzer，分别固定 clean production、全树 collector、单 token mutation + allowlist sibling decoy、missing-root fail-closed。TIME-004 是 source absence/architecture law，不登记到 `Process/Surface.js`；Surface Manifest 只约束 runtime semantic boundary，不能证明全生产树不存在 ambient-time token。

### 7. 会话时间原点单次绑定（`SessionStartedAt`）

`Execution/Session/SessionStartedAtProjection.{fsi,fs}`独占`execution-session-sessionstartedatprojection` bounded contract。`SessionStartedAtLedger`显式同时消费projection与clock capability；durable fold只消费projection，不获得Node/virtual timing implementation。在会话首次生成 prompt 时同步采样注入时钟并追加持久化事件，后续重启通过内存投影 O(1) 获取固定原点，确保会话经过时长（elapsed）的确定性与不可变性。

### 8. Temporal边界的production行为proof

`Process/Surface.js`直接投影production `IClockPort`、`ITimerPort`、`Deadline`、Node adapter与virtual implementation。TIME-008的行为proof固定三个可区分错误世界：两个capability实例不得共享推进状态；`Deadline`只随显式clock input变化且不积累隐藏状态；构造Node capability不得改变virtual clock/timer。第四个proof直接读取正式owner-project inventory，固定六个production locality的source闭集及每个fresh compiler-resolved consumer的最窄ProjectReference；它不扫描源码、不重建dependency analyzer。Node物理时钟与timer的外界正确性属于adapter canary，不用墙钟容差或真实timer等待伪装成确定性unit proof。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| TIME-001 | `requirements/time-capability/tests/clock-port-virtual.test.mjs::WHAT[TIME-001] TIME_001_virtual_clocks_are_independent_not_ambient` |
| TIME-002 | `requirements/time-capability/tests/deadline-typed.test.mjs::WHAT[TIME-002] TIME_002_deadline_of_budget_and_remaining_are_pure_clock_functions` |
| TIME-003 | `requirements/time-capability/tests/timer-port.test.mjs::WHAT[TIME-003] VERIFY_004_virtual_timer_fires_exactly_when_advanced_past_deadline` |
| TIME-004 | `requirements/time-capability/tests/ambient-time-forbidden.test.mjs::WHAT[TIME-004] domain_application_session_contain_no_raw_time_tokens` |
| TIME-005 | `requirements/time-capability/tests/deadline-typed.test.mjs::WHAT[TIME-005] TIME_005_verdict_follows_injected_clock_not_value` |
| TIME-006 | `requirements/time-capability/tests/until-signal-or-deadline.test.mjs::WHAT[TIME-006] THEOREM_untilSignalOrDeadline_deadline_without_material_is_WaitTimedOut` |
| TIME-007 | `requirements/time-capability/tests/pair-session-elapsed.test.mjs::WHAT[TIME-007] TIME_007_session_started_at_is_bind_once_to_first_prompt_sample`；`requirements/time-capability/tests/session-started-at-bind-surface.test.mjs::WHAT[TIME-007] SessionStartedAtLedger owns bindSessionStartedAt entry point for transform boundary` |
| TIME-008 | `requirements/time-capability/tests/m6-slice-boundary.test.mjs::WHAT[TIME-008] production inventory separates contracts adapter verification and representation`；`requirements/time-capability/tests/m6-slice-boundary.test.mjs::WHAT[TIME-008] clock and timer capabilities are opaque instance-bound values`；`requirements/time-capability/tests/m6-slice-boundary.test.mjs::WHAT[TIME-008] Deadline is immutable and decided only by explicit clock input`；`requirements/time-capability/tests/m6-slice-boundary.test.mjs::WHAT[TIME-008] Node capability construction cannot mutate virtual time` |
