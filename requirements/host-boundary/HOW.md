# host-boundary — HOW

## 架构机制

### 1. 信号适配与碎片过滤

宿主事件编解码器（`HostEventCodec`）在边界拦截所有流式碎片（如 `part.delta`、`message.updated`）。普通碎片不离开边界；粗粒度会话条件转化为 `HostSignal`，exact assistant lifecycle 字段转化为只供 provider-start/terminal accounting 使用的 typed physical observation，绝不成为业务 `HostSignal`：
- `SessionIdle` 与 `ProviderRetry`：触发单飞调和流程。
- `AttemptAborted`：撤销当前物理尝试的静止能力，不推进业务重试。
- `ProviderFailure`：结合快照中的确切助手消息共同确认失败终态。

### 2. 快照投影与身份因果解析

- **SessionSnapshotPort**：提供一致的消息结构投影，维护工具调用与执行结果的状态对齐。
- **因果绑定**：公开 `message.updated.properties.info` 同时携带 exact `sessionID`、assistant `id`、exact user `parentID`、`role=assistant` 与 `time.created` 时，边界才发布 exact provider-start observation；任一字段缺失或不匹配即安全失败。
- **Pre-run transform 边界**：公开顺序为 `chat.message → experimental.chat.messages.transform(user only) → chat.params(可重复) → provider`。Transform 只按 exact `(SessionId, PhysicalUserMessageId)` 冻结 pending attempt plan，绝不建立 `ProviderRunIdentity` 或写 `ProviderStarted`。首次 exact assistant observation 一次性绑定 plan 并先持久 `ProviderStarted`；同一事件若还携带 terminal evidence，只有 start 持久确认后才进入 terminal accounting。
- **两半边身份守门**：`ToolHostCodec` 必须在上下文内同时获取消息 ID 与调用 ID，否则拒绝执行。

### 3. 调和器调度与事件驱动收敛

`Scheduler` 维护会话的单飞调和生命周期：
- 单一会话至多并发一次调和，避免并发快照冲突。
- 依赖因果事件驱动推进，快照未决时保持挂起，杜绝全局墙钟轮询。
- 维护物理执行租约与完成状态，确保失败步骤与最终执行完成明确区分。

### 4. Typed Hook Membrane、结算与加载纯洁性

- **一次 typed normalization**：所有交付 Host 的 Hook 先从公开 Hook/SDK evidence 归一为 `ExecutionFailure`；wire/schema rejection 直接形成 `ProtocolRejection`。diagnostic text 不进入决策输入，未识别结构 fail closed，不存在 catch-all retry/fatal。
- **单一 decision interpreter**：membrane 只解释 `execution-failure-policy` 返回的六维 decision。`FatalAfterSettlement` 路径严格执行 exact capacity fence settlement → managed-chat typed disposition durable submission → settlement committed/unknown evidence → `FatalProcess`，任一步不得委托 Host/UI 私有 cleanup。
- **closed policy score**：`HookPolicy.metadata` 以 `HookKey` 穷尽匹配承载每个 live Hook 的 criticality、context/effects、retry、capacity、failure、identity 与 admission 权限；`PluginHooks.create` 只按显式固定顺序调用 `registeredHook`，由 score 唯一生成 Host key 与 `policyAwareHook` diagnostic operation。
- **optional observation boundary**：Casebook observation 只能经 `HookPolicy.observeOptional` 执行。Plugin composition 注入 `Diagnostic.emit` physical port；boundary 捕获失败、调用该 port 并返回 typed outcome。critical tool-after checkpoint 与 result mutation 已先完成，optional outcome 不参与其返回值或 failure decision。
- **阶段划分**：严格分离 Load Phase 与 Activation Phase。加载期严禁调用宿主会话 API 或触碰持久化日志，确保插件初始化的无副作用。

### 5. 公开 contract 与真实 canary

- contract adapter 只 import 受支持公开 Hook/SDK surface；architecture proof 拒绝 Host fork、private module、monkey patch 与 UI/DOM 依赖。
- canary 启动声明支持的真实 Host build，通过公开入口执行 Hook ordering、snapshot identity、routing projection、terminal observation 与 fatal settlement 场景，并从公开输出断言结果。mock/fixture tests 仍可做低层 contract proof，但不能标记为 canary。
- canary failure 或能力不可观察时环境 fail closed；不得以版本猜测、wall-clock wait 或 UI 文案推定支持。

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
| HOST-BOUNDARY-008 | `requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs` |
| HOST-BOUNDARY-009 | `requirements/host-boundary/tests/tool-host-codec.test.mjs` |
| HOST-BOUNDARY-010 | `requirements/host-boundary/tests/shared-state.test.mjs` |
| HOST-BOUNDARY-011 | `requirements/host-boundary/tests/host-message-projection.test.mjs`, `requirements/host-boundary/tests/host-message-sanitize-surface.test.mjs` |
| HOST-BOUNDARY-012 | `requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-013 | `requirements/host-boundary/tests/loop-sensor-wiring-owner.test.mjs` |
| HOST-BOUNDARY-014 | `requirements/host-boundary/tests/host-hooks.test.mjs`, `requirements/host-boundary/tests/chat-hook-settlement.test.mjs` |
| HOST-BOUNDARY-015 | `requirements/host-boundary/tests/tool-result-bound.test.mjs` |
| HOST-BOUNDARY-016 | `requirements/host-boundary/tests/events-port.test.mjs` |
| HOST-BOUNDARY-017 | `requirements/host-boundary/tests/host-session-context.test.mjs` |
| HOST-BOUNDARY-018 | `requirements/host-boundary/tests/host018-no-fork.test.mjs` |
| HOST-BOUNDARY-019 | `requirements/host-boundary/tests/opencode-chat-admission-canary.test.mjs` |
| HOST-BOUNDARY-020 | `requirements/host-boundary/tests/session-snapshot-locality.test.mjs`, `requirements/host-boundary/tests/opencode-chat-admission-canary.test.mjs` |
| HOST-BOUNDARY-021 | `requirements/host-boundary/tests/plugin-load-purity.test.mjs`, `requirements/host-boundary/tests/host-signal-bootstrap-composition.test.mjs` |
| HOST-BOUNDARY-022 | `requirements/host-boundary/tests/chat-hook-settlement.test.mjs` |
| HOST-BOUNDARY-023 | `requirements/host-boundary/tests/host018-no-fork.test.mjs`, `requirements/host-boundary/tests/opencode-chat-admission-canary.test.mjs` |
| HOST-BOUNDARY-024 | `requirements/host-boundary/tests/hook-policy.test.mjs`, `scripts/checks/hook-policy.mjs` |
| HOST-BOUNDARY-025 | `requirements/host-boundary/tests/diagnostics.test.mjs`, `requirements/host-boundary/tests/known-failure-stderr.test.mjs` |
