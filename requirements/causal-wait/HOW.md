# causal-wait — HOW

## 架构与实现机制

`causal-wait` 通过因果词汇建模、权限类型隔离的内存注册表、异步等待包装器以及静态边界门禁共同实现可诊断且非权威的等待体系：

### 1. 通用因果诊断词汇与前沿算法（`Execution/Session/Wait/CausalWait.fs`）

- **数据模型**：定义 `CausalOwnerRef`（等待主体标识）、`CausalProducerRef`（生产者引用）、`WaitEscape`（五种强类型终止逃逸：`DeadlineAt`、`CancelledBy`、`ProcessLifetime`、`SessionLifetime`、`OpenEndedExternal`）与 `DiagnosticWait` 描述符。
- **退出状态**：`DiagnosticWaitExit`（`WaitResolved`、`WaitFailed`、`WaitCancelled`、`WaitTimedOut`、`WaitDisposed`）仅用于退出时的诊断分类。
- **因果前沿纯算法（`CausalFrontier.ofSnapshot`）**：从根节点出发遍历因果等待图，识别外部阻塞点（`ExternalProducerFrontier`）、断裂边（`BrokenCausalEdge`）、死锁环（`CausalWaitCycle`）与无等待运行（`ProducerRunningWithoutWait`），提供纯诊断输出。

### 2. 读写权限隔离的内存注册表（`Execution/Session/Wait/Registry.fs`）

- **双接口设计**：`IWaitObserver`（仅公开 `Enter` 注册租约）供业务 workflow 使用；`IWaitSnapshotReader`（公开 `Snapshot` 读取）仅供诊断工具调用。
- **进程内生命周期**：活跃等待字典与有界环形历史队列（容量 256），单例管理，不持久化至磁盘，重启安全清空。
- 租约（`IWaitLease`）支持幂等释放，退出标记（`MarkExit`）与租约注销（`Dispose`）确保观测状态精确离开活跃集。
- `Execution/Session/Wait/Surface.fs` 将两个接口分别封装成不透明 `ObserverHandle` 与 `SnapshotReaderHandle`。测试必须通过真实 observer 登记等待、通过真实 reader 读取同一 registry；把任一 handle 传给另一种操作会在 capability 边界拒绝。不存在返回固定真假值的权限镜像。

### 3. 异步等待包装器（`Execution/Session/Wait/Await.fs`）

- `awaitTask` / `awaitUnit`：在异步等待前后包裹 `Enter` 与 `Leave`，异常时自动按类型归类为超时或取消，不改变底层任务的执行语义与顺序。
- `untilSignalOrDeadline`：先尝试读取；未就绪时并行竞争真实依赖信号与强类型截止时间句柄，信号唤醒后重新读取，完全消除固定睡眠与轮询循环。

### 4. 诊断桥接文件（`Execution/Session/Wait/Bridge.fs`）

将内存快照以非阻塞覆盖写方式输出至 `.wanxiangshu/diagnostics/causal-waits.json`，供测试与排障工具采集。该路径已被 git 忽略，且严禁业务代码读取。

### 5. 静态边界防护门禁（`causal-wait-boundary`）

`scripts/checks/causal-wait-boundary.mjs` 是唯一 scanner owner。CLI 与 `requirements/causal-wait/tests/boundary-observation.test.mjs` 调用同一个纯 `analyzeObservationBoundary(files)`；requirement test 不读取源码重建第二套 detector。

- collector 必须找到 `src/Wanxiangshu`，读取全部 production `.fs`；根缺失、目录不可读或文件读取失败直接抛错，不返回空集合。
- 任意 `Journal` 路径及所有 `Fact.fs` / `Facts.fs` carrier 的 executable F# 禁止完整 causal-wait vocabulary。
- `Execution/Session/Wait` owner 外禁止 `IWaitSnapshotReader`、`DiagnosticWaitSnapshot`、registry、bridge、semantic Surface、reader hub member 与 module-open hub alias。只允许业务 writer `CausalWaitHub.observer` 与 composition wiring `CausalWaitHub.setWorkspace`。
- `.wanxiangshu/diagnostics/causal-waits.json` 是 owner-owned locator；production owner 外出现即失败，防止绕过 typed reader 直接读取桥接文件。
- F# 注释与字符串中的符号由 `maskFSharpTrivia` 排除，作为 false-positive decoy；locator 字符串本身不是 decoy，因为它就是可读取诊断文件的协议地址。
- gate 在每次 `scripts/check.mjs` 中先执行 legal baseline 与 Journal、Fact、未来 decision path、module-open alias、locator 五类精确 mutation。每个 mutation 必须实际改变 fixture，且只产生预期单一 violation。
- 原有关键迁移点 bare Task await 与 `Registry.fs` 的 `DSL-MUTABLE` 标注继续由同一次全树采集执行。

机械保证边界：该门禁证明受支持 F# source path 中不存在上述读取能力或 locator，不声称完成通用跨语言数据流证明。行为测试补足 capability 因果：observer 能写、reader 能读、二者不可互换；WHAT 与 oracle 的业务正确性仍由 owner review 负责。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CAUSAL-001 | `requirements/causal-wait/tests/causal-wait.test.mjs::WHAT[CAUSAL-001] RED_8_application_observer_enter_only_snapshot_via_reader` |
| CAUSAL-002 | `requirements/causal-wait/tests/causal-wait.test.mjs::WHAT[CAUSAL-002] RED_1_active_wait_visible_after_enter` |
| CAUSAL-003 | `requirements/causal-wait/tests/boundary-observation.test.mjs::WHAT[CAUSAL-003] business observer cannot read the diagnostic snapshot`；`requirements/causal-wait/tests/boundary-observation.test.mjs::WHAT[CAUSAL-003] shared analyzer accepts the real production tree`；`requirements/causal-wait/tests/boundary-observation.test.mjs::WHAT[CAUSAL-003] analyzer rejects snapshot reads in an unlisted future decision path` |
| CAUSAL-004 | `requirements/causal-wait/tests/wait-lifecycle.test.mjs::WHAT[CAUSAL-004] CAUSAL_004_observer_and_reader_capabilities_are_not_interchangeable` |
| CAUSAL-005 | `requirements/causal-wait/tests/until-signal-or-deadline.test.mjs::WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline` |
| CAUSAL-006 | `requirements/causal-wait/tests/causal-wait.test.mjs::WHAT[CAUSAL-006] RED_2_resolve_clears_active_and_records_resolved` |
| CAUSAL-007 | `requirements/causal-wait/tests/causal-frontier.test.mjs::WHAT[CAUSAL-007] RED_5_nested_graph_walks_to_external_frontier` |
| CAUSAL-008 | `requirements/causal-wait/tests/wait-lifecycle.test.mjs::WHAT[CAUSAL-008] CAUSAL_008_fresh_registry_starts_empty_no_durable_state` |
