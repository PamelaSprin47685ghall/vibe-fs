# causal-wait — HOW

## 架构与实现机制

`causal-wait` 以五个compiler boundary locality实现可诊断且非权威的等待体系：

### 1. Pure contract（`execution-session-wait-contract`）

- **数据模型**：定义 `CausalOwnerRef`（等待主体标识）、`CausalProducerRef`（生产者引用）、`WaitEscape`（五种强类型终止逃逸：`DeadlineAt`、`CancelledBy`、`ProcessLifetime`、`SessionLifetime`、`OpenEndedExternal`）与 `DiagnosticWait` 描述符。
- **退出状态**：`DiagnosticWaitExit`（`WaitResolved`、`WaitFailed`、`WaitCancelled`、`WaitTimedOut`、`WaitDisposed`）仅用于退出时的诊断分类。
- **join/wake纯词汇**：`JoinInterruptReason`、`JoinWaitOutcome`、`MailboxWakeReason`与`NonEmptyBatch`只描述typed等待结果；contract不构造`TaskCompletionSource`或mailbox。
- **因果前沿纯算法（`CausalFrontier.ofSnapshot`）**：从根节点出发遍历因果等待图，识别外部阻塞点（`ExternalProducerFrontier`）、断裂边（`BrokenCausalEdge`）、死锁环（`CausalWaitCycle`）与无等待运行（`ProducerRunningWithoutWait`），提供纯诊断输出。

### 2. Registry / await runtime（`execution-session-wait-runtime`）

- **双接口设计**：`IWaitObserver`（仅公开 `Enter` 注册租约）供业务 workflow 使用；`IWaitSnapshotReader`（公开 `Snapshot` 读取）仅供诊断工具调用。
- **进程内生命周期**：`CausalWaitProcess.local()`独占单例runtime；活跃等待字典与有界环形历史队列（容量256）不持久化，重启安全清空。
- 租约（`IWaitLease`）支持幂等释放，退出标记（`MarkExit`）与租约注销（`Dispose`）确保观测状态精确离开活跃集。
- `CausalWaitRuntime.BindDiagnosticTarget`只接受`IWaitDiagnosticSink`且first-bind；后续plugin实例不能重定向。sink失败被诊断边界吸收，不改变业务等待。
- 所有production workflow通过Host composition显式获得`IWaitObserver`；不存在global observer或service locator。
- `Execution/Session/Wait/Surface.fs` 将两个接口分别封装成不透明 `ObserverHandle` 与 `SnapshotReaderHandle`。测试必须通过真实 observer 登记等待、通过真实 reader 读取同一 registry；把任一 handle 传给另一种操作会在 capability 边界拒绝。不存在返回固定真假值的权限镜像。

异步等待包装器：

- `awaitTask` / `awaitUnit`：在异步等待前后包裹 `Enter` 与 `Leave`，异常时自动按类型归类为超时或取消，不改变底层任务的执行语义与顺序。
- `untilSignalOrDeadline`：先尝试读取；未就绪时并行竞争真实依赖信号与强类型截止时间句柄，信号唤醒后重新读取，完全消除固定睡眠与轮询循环。

### 3. Node diagnostic adapter（`execution-session-wait-diagnostic-adapter`）

`CausalWaitBridge.target`是唯一Node/path/fs owner，将内存快照以非阻塞覆盖写方式输出至`.wanxiangshu/diagnostics/causal-waits.json`。Host composition把该窄sink注入runtime；业务workflow既不持有adapter，也不能读取诊断文件。

### 4. CompletionMailbox runtime（`execution-session-wait-completion-mailbox`）

`CompletionMailbox`独占PTY/agent completion的process-local wake resource。pure wait contract只定义无状态泛型`ICompletionMailbox` capability，不包含实现；delegation fork runtime把它收窄为`ForkCompletionMailbox`并只消费必填factory。`CompletionMailboxRuntime.create`是唯一physical constructor；Change、Finality、ToolRuntimeScope与proof composition显式选择并注入，Host adapter与Fork runtime均不引用foreign runtime。Sync runtime同样只接收两个结果类型精确的await函数；`CausalAwait`只在Host composition绑定。由此delegation sync/fork/recovery locality继续是runtime，不因跨owner physical implementation被误标为composition。

### 5. Proof Surface（`execution-session-wait-proof-surface`）

`Execution/Session/Wait/Surface.fs`只投影production contract/runtime/adapter/mailbox。owner inventory证明没有production locality引用该Surface；`PROC-008` mailbox proof也在此注册，删除`Process/Surface.fs`中的第二份mailbox镜像。

### 6. 静态边界防护门禁（`causal-wait-boundary`）

`scripts/checks/causal-wait-boundary.mjs` 是唯一 scanner owner。CLI 与 `requirements/causal-wait/tests/boundary-observation.test.mjs` 调用同一个纯 `analyzeObservationBoundary(files)`；requirement test 不读取源码重建第二套 detector。

- collector 必须找到 `src/Wanxiangshu`，读取全部 production `.fs`；根缺失、目录不可读或文件读取失败直接抛错，不返回空集合。
- 任意 `Journal` 路径及所有 `Fact.fs` / `Facts.fs` carrier 的 executable F# 禁止完整 causal-wait vocabulary。
- `Execution/Session/Wait` owner 外禁止 `IWaitSnapshotReader`、`DiagnosticWaitSnapshot`、registry、semantic Surface与已删除的`CausalWaitHub`；唯一bridge出口是`PluginHostWiring`调用`CausalWaitBridge.target`构造注入sink。业务writer只能消费composition注入的`IWaitObserver`。
- `.wanxiangshu/diagnostics/causal-waits.json` 是 owner-owned locator；production owner 外出现即失败，防止绕过 typed reader 直接读取桥接文件。
- F# 注释与字符串中的符号由 `maskFSharpTrivia` 排除，作为 false-positive decoy；locator 字符串本身不是 decoy，因为它就是可读取诊断文件的协议地址。
- gate 在每次 `scripts/check.mjs` 中先执行 legal baseline 与 Journal、Fact、未来 decision path、global hub、module-open alias、locator六类精确mutation。每个mutation必须实际改变fixture，且只产生预期单一violation。
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
| CAUSAL-009 | `requirements/causal-wait/tests/m6-slice-boundary.test.mjs::WHAT[CAUSAL-009] production inventory separates contract runtime adapter mailbox and proof surface`；`requirements/causal-wait/tests/causal-wait-bridge.test.mjs::WHAT[CAUSAL-009] CAUSAL_BRIDGE_first_binding_is_stable_and_refreshes_on_lifecycle` |
