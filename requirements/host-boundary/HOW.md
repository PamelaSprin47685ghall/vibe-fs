# host-boundary — HOW

## 架构机制

### 1. 信号适配与碎片过滤

宿主事件编解码器（`HostEventCodec`）在边界拦截所有流式碎片（如 `part.delta`、`message.updated`），仅将满足生命周期粗粒度条件的事件转化为 `HostSignal`：
- `SessionIdle` 与 `ProviderRetry`：触发单飞调和流程。
- `AttemptAborted`：撤销当前物理尝试的静止能力，不推进业务重试。
- `ProviderFailure`：结合快照中的确切助手消息共同确认失败终态。

### 2. 快照投影与身份因果解析

- **SessionSnapshotPort**：提供一致的消息结构投影，维护工具调用与执行结果的状态对齐。
- **因果绑定**：通过严格的因果四条件（角色为助手、完成时间未设、父消息匹配最新用户消息、ID 为最大）解析 `ProviderRunIdentity`。若出现匹配缺失或歧义，一律安全失败。
- **Pre-run transform 边界**：`experimental.chat.messages.transform` 发生在本次 provider inference 之前，禁止在这里等待本次 assistant child。Recovery transform 先按 exact physical user id 冻结 pending attempt plan；后续完整 Host turn / tool-continuation 观测提供 run identity 时才完成一次性绑定。
- **两半边身份守门**：`ToolHostCodec` 必须在上下文内同时获取消息 ID 与调用 ID，否则拒绝执行。

### 3. 调和器调度与事件驱动收敛

`Scheduler` 维护会话的单飞调和生命周期：
- 单一会话至多并发一次调和，避免并发快照冲突。
- 依赖因果事件驱动推进，快照未决时保持挂起，杜绝全局墙钟轮询。
- 维护物理执行租约与完成状态，确保失败步骤与最终执行完成明确区分。

### 4. 致命保护膜与加载纯洁性

- **Hook Fatal Membrane**：所有交付给宿主的 Hook 函数统一包裹异常熔断包装器，同步或异步抛出的不变量异常直接触发进程级致命退出。
- **阶段划分**：严格分离 Load Phase 与 Activation Phase。加载期严禁调用宿主会话 API 或触碰持久化日志，确保插件初始化的无副作用。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| HOST-BOUNDARY-001 | `requirements/host-boundary/tests/host001-fragment-events.test.mjs` |
| HOST-BOUNDARY-002 | `requirements/host-boundary/tests/host001-fragment-events.test.mjs` |
| HOST-BOUNDARY-003 | `requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-004 | `requirements/host-boundary/tests/host004-turnunknown-boundary.test.mjs` |
| HOST-BOUNDARY-005 | `requirements/host-boundary/tests/reconcile-idle-early.test.mjs` |
| HOST-BOUNDARY-006 | `requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-007 | `requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-008 | `requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs`（typed fail-closed 判定）· `requirements/host-boundary/tests/xwire.test.mjs`（pre-inference 冻结未绑定 plan，无 snapshot 输入）· `scripts/checks/transform-causality-gate.mjs`（seam 函数边界 + 有序因果 + wait/retry 禁令，20 fixtures 自证可红） |
| HOST-BOUNDARY-009 | `requirements/host-boundary/tests/tool-host-codec.test.mjs` |
| HOST-BOUNDARY-010 | `requirements/host-boundary/tests/shared-state.test.mjs` |
| HOST-BOUNDARY-011 | `requirements/host-boundary/tests/host-message-projection.test.mjs`, `requirements/host-boundary/tests/host-message-sanitize-surface.test.mjs` |
| HOST-BOUNDARY-012 | `requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-013 | `requirements/host-boundary/tests/loop-sensor-wiring-owner.test.mjs` |
| HOST-BOUNDARY-014 | `requirements/host-boundary/tests/host-hooks.test.mjs` |
| HOST-BOUNDARY-015 | `requirements/host-boundary/tests/tool-result-bound.test.mjs` |
| HOST-BOUNDARY-016 | `requirements/host-boundary/tests/events-port.test.mjs` |
| HOST-BOUNDARY-017 | `requirements/host-boundary/tests/host-session-context.test.mjs` |
| HOST-BOUNDARY-018 | `requirements/host-boundary/tests/host018-no-fork.test.mjs` |
| HOST-BOUNDARY-019 | `requirements/host-boundary/tests/host-capability-observation.test.mjs`, `requirements/host-boundary/tests/ordered-transform.test.mjs` |
| HOST-BOUNDARY-020 | `requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-021 | `requirements/host-boundary/tests/plugin-load-purity.test.mjs`, `requirements/host-boundary/tests/host-signal-bootstrap-composition.test.mjs` |
