# fallback continue 终止条件调查

## 结论

当前实现不是按“思考输出字节数”为唯一条件继续。空输出检测的真实谓词是：最近一条 assistant 消息存在，且其 `parts` 无 `tool`/`dynamic-tool`，无非空白 `text`。因此“仅对 0 字节思考输出继续”应准确改述为“对无可见文本且无工具调用的 assistant 输出继续”。

`parts` 为 `null`、非数组或缺失时也被视为无内容；没有 assistant 消息则不是空输出。任何工具 part 都阻止该谓词，即使工具文本为空。

## 主要判断链

### 1. 错误 fallback

- `src/Kernel/FallbackKernel/Decision.fs:41-62`：`classifyError` 先检查生命周期、abort、401/402/403、显式不可重试、`MaxRetries`，再进入 `RetrySame`；非上述错误的默认分支仍是 `RetrySame`，不是终止。
- `src/Kernel/FallbackKernel/StateMachine.fs`：`handleSessionError` 将 `RetrySame`/`ImmediateFallback`/`Exhausted` 转为重试、扫描或耗尽状态；`sendOrContinue` 是内核发出 `SendContinue` 的入口。
- `src/Runtime/Fallback/FallbackCoordination.fs` → `ContinuationExecutionCore.fs` → 宿主 `ActionExecutor.fs`：运行时把动作转成实际 `SendContinue`/`session.prompt`。物理发送路径只有一条。
- `src/Kernel/FallbackKernel/Recovery.fs`：`selectModel`/`scanStartIndex` 决定 fallback 链扫描位置；候选链由 `src/Runtime/Fallback/FallbackChainResolution.fs` 解析。

### 2. 空输出与工具调用

- `src/Runtime/Fallback/FallbackMessageDetection.fs:101-125`：`isIdleNoContentAndNoTools` 实现空输出谓词。
- `src/Hosts/OpenCode/Fallback/MessageInspectionObservation.fs`：从宿主消息提取当前轮次观察并调用上述检测。
- 空输出通过当前轮证据进入 `EmptyOutputError`/`SessionIdleObserved`，随后子会话决策向 `DispatchPrompt`/continue 前进；对应回归在 `tests/SubsessionEmptyOutputContinueTests.fs:83-102`。
- `src/Runtime/Fallback/FallbackBridgeScanToolText.fs:114-134`：工具文本扫描阶段先处理 `allTodosCompleted`，再处理 XML/raw tool-call 恢复，最后按 transcript settle。
- `settleByTranscript`（同文件 `:39-60`）使用 `isLastAssistantToolFinish` 与 `hasToolResultAfter`：非工具结束或已有工具结果 ⇒ `TaskComplete`；工具结束且尚无结果 ⇒ 保持 `Active`，不宣告任务完成。
- `isLastAssistantToolFinish`（`FallbackMessageDetection.fs:157-171`）识别 `FinishReason.ToolCalls`，以及未知 finish 中含 `tool` 且不等于 `tool_use_error` 的值；这使未知宿主 finish 字符串存在误判边界。
- `hasToolResultAfter`（`:173-185`）只需 assistant 消息之后任一消息角色满足工具结果角色即可。

### 3. 生命周期、gate 与最终清理

- `src/Kernel/FallbackSubagentGate.fs:29-63`：`needFallbackContinue` 的最高优先级是 `TaskComplete`/`Cancelled`；非自然 `TerminalOrigin` 也直接禁止 continue。之后依次检查活跃事件、主 continuation 等待、busy、nudge、phase、`ConsumedByHost`。
- `FallbackSubagentGate.fs:82-95`：`terminalObservation` 将完成/取消、非自然终止、`Exhausted`、`PropagatedToOuter` 视为终端。它与 `needFallbackContinue` 并非同一谓词：前者更保守，后者在 Active + Idle + `ConsumedByHost` 时仍允许继续。
- `src/Kernel/FallbackRuntimeLifecycle.fs` 定义 `Active/TaskComplete` 与 `Idle/Retrying` 的基础转换；完整 `FallbackPhase`/`FallbackLifecycle` 类型在 `src/Kernel/FallbackKernel/Types.fs`。
- `src/Runtime/Fallback/FallbackIdleSettlement.fs:70-120`：只有 `TaskComplete`、`Cancelled`、`Exhausted` 或 Idle 且无 intent 才进入 post-settlement；pending lease 在已被 host 接受后才清除，等待 acceptance 的 lease 被保留。
- OpenCode 的 idle/busy 事件翻译在 `src/Hosts/OpenCode/Fallback/EventTranslator.fs`，状态识别在 `src/Hosts/OpenCode/Fallback/HostEventInspection.fs`。

## 现有测试覆盖

已有覆盖：

- `tests/SubsessionEmptyOutputContinueTests.fs`：无 assistant、空 assistant、空白文本继续；draining 后成功回到 `Available` 并重置 continue count。
- `tests/OpencodeFallbackChildIdleTests.fs`：当前用户轮次锚定 assistant 证据，idle 后返回当前报告而非 stale 消息。
- `tests/FallbackKernelTests.fs`、`FallbackKernelCancelAndRecoveryTests.fs`：fallback kernel 的错误、取消、恢复状态。
- `tests/RetryDispatchGovernorTests.fs`：dispatch 限流/取消期间的 governor 行为。
- `tests/FallbackEventBridge*Tests.fs`、`FallbackIntegrationTests.fs`：事件桥接与集成路径。
- `tests/ContinuationPathSsotTests.fs`、`ContinuationCleanupTests.fs`：continue 的唯一发送路径与旧文件清理架构约束。

直接空白：

- `isIdleNoContentAndNoTools` 没有针对 `parts=null`、非数组、仅 tool、仅空白 text、混合 text+tool 的独立单元测试。
- `isLastAssistantToolFinish` 与 `hasToolResultAfter` 的组合矩阵无直接测试，尤其未知 finish 字符串和工具结果紧邻/隔行场景。
- `FallbackSubagentGate.needFallbackContinue` 与 `terminalObservation` 的完整交叉分支无专项测试。
- `handleScanToolCallAsText` 的 `allTodosCompleted`、raw tool-call、无 raw tool-call 三路无独立测试。

## 边界与后续实现提示

1. “0 字节思考”不是源码概念；源码只观察结构化消息的 text/tool parts，不区分 reasoning/thinking part 与普通 text part。若需求真正针对思考字段，必须先定义宿主消息 schema，再新增明确谓词。
2. `parts=null`/非数组当前等价空输出，可能把消息协议损坏误判为 LLM 空答；这是最值得补测试并决定策略的边界。
3. 工具调用结束不是单一 finish 字段：`ToolCalls` 无结果时继续等待/恢复；有结果后才可 settle。不能用 idle 事件单独宣告完成。
4. `allTodosCompleted` 先于 transcript settle；todo 已终止会直接把 episode 标为 `TaskComplete`，即使后续消息检测仍有未决工具语义。需要业务确认该优先级。
5. 不应将 `terminalObservation` 结果直接替代 `needFallbackContinue`；二者承担“已终端”和“是否继续占 gate”两种不同责任。
