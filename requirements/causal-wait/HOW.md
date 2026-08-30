# causal-wait — HOW

## 架构与实现机制

`causal-wait` 通过因果词汇建模、权限类型隔离的内存注册表、异步等待包装器以及静态边界门禁共同实现可诊断且非权威的等待体系：

### 1. 通用因果诊断词汇与前沿算法（`Kernel/CausalWait.fs`）

- **数据模型**：定义 `CausalOwnerRef`（等待主体标识）、`CausalProducerRef`（生产者引用）、`WaitEscape`（五种强类型终止逃逸：`DeadlineAt`、`CancelledBy`、`ProcessLifetime`、`SessionLifetime`、`OpenEndedExternal`）与 `DiagnosticWait` 描述符。
- **退出状态**：`DiagnosticWaitExit`（`WaitResolved`、`WaitFailed`、`WaitCancelled`、`WaitTimedOut`、`WaitDisposed`）仅用于退出时的诊断分类。
- **因果前沿纯算法（`CausalFrontier.ofSnapshot`）**：从根节点出发遍历因果等待图，识别外部阻塞点（`ExternalProducerFrontier`）、断裂边（`BrokenCausalEdge`）、死锁环（`CausalWaitCycle`）与无等待运行（`ProducerRunningWithoutWait`），提供纯诊断输出。

### 2. 读写权限隔离的内存注册表（`Session/CausalWaitRegistry.fs`）

- **双接口设计**：`IWaitObserver`（仅公开 `Enter` 注册租约）供业务 workflow 使用；`IWaitSnapshotReader`（公开 `Snapshot` 读取）仅供诊断工具调用。
- **进程内生命周期**：活跃等待字典与有界环形历史队列（容量 256），单例管理，不持久化至磁盘，重启安全清空。
- 租约（`IWaitLease`）支持幂等释放，退出标记（`MarkExit`）与租约注销（`Dispose`）确保观测状态精确离开活跃集。

### 3. 异步等待包装器（`Session/CausalAwait.fs`）

- `awaitTask` / `awaitUnit`：在异步等待前后包裹 `Enter` 与 `Leave`，异常时自动按类型归类为超时或取消，不改变底层任务的执行语义与顺序。
- `untilSignalOrDeadline`：先尝试读取；未就绪时并行竞争真实依赖信号与强类型截止时间句柄，信号唤醒后重新读取，完全消除固定睡眠与轮询循环。

### 4. 诊断桥接文件（`Session/CausalWaitBridge.fs`）

将内存快照以非阻塞覆盖写方式输出至 `.wanxiangshu/diagnostics/causal-waits.json`，供测试与排障工具采集。该路径已被 git 忽略，且严禁业务代码读取。

### 5. 静态边界防护门禁（`causal-wait-boundary`）

`scripts/checks/causal-wait-boundary.mjs` 与 `requirements/causal-wait/tests/boundary-observation.test.mjs::WHAT[CAUSAL-003] Journal codec surfaces stay free of the causal-wait vocabulary` 静态扫描源码，确保：
- Domain 与 Application 层不触碰快照读取器；
- Fact 与 Journal 编解码器表面不包含因果等待符号；
- Prompt 与业务决策路径不引用诊断数据；
- 关键迁移点不存在裸 Task 等待。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CAUSAL-001 | `requirements/causal-wait/tests/causal-wait.test.mjs::WHAT[CAUSAL-001] RED_8_application_observer_enter_only_snapshot_via_reader` |
| CAUSAL-002 | `requirements/causal-wait/tests/causal-wait.test.mjs::WHAT[CAUSAL-002] RED_1_active_wait_visible_after_enter` |
| CAUSAL-003 | `requirements/causal-wait/tests/boundary-observation.test.mjs::WHAT[CAUSAL-003] Journal codec surfaces stay free of the causal-wait vocabulary` |
| CAUSAL-004 | `requirements/causal-wait/tests/wait-lifecycle.test.mjs::WHAT[CAUSAL-004] CAUSAL_004_observer_surface_has_no_snapshot` |
| CAUSAL-005 | `requirements/causal-wait/tests/until-signal-or-deadline.test.mjs::WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline` |
| CAUSAL-006 | `requirements/causal-wait/tests/causal-wait.test.mjs::WHAT[CAUSAL-006] RED_2_resolve_clears_active_and_records_resolved` |
| CAUSAL-007 | `requirements/causal-wait/tests/causal-frontier.test.mjs::WHAT[CAUSAL-007] RED_5_nested_graph_walks_to_external_frontier` |
| CAUSAL-008 | `requirements/causal-wait/tests/wait-lifecycle.test.mjs::WHAT[CAUSAL-008] CAUSAL_008_fresh_registry_starts_empty_no_durable_state` |
