# F-05 — OpenCode Fallback Continue 唯一物理路径

权威顺序：实现与测试 > 本文 > 历史 docs 中的 ContinuationHost / v2 supervisor 叙述。

## 裁决

1. OpenCode continue **唯一**物理发送路径是 `IActionExecutor.SendContinue` → `SessionDispatcher.Dispatch` → 宿主 `session.prompt`。
2. 已删除且禁止复活：`ContinuationHost`、`ContinuationCommandProcessor`、`ContinuationSupervisor` 及 Hosts 侧同名 adapter（见 `ContinuationCleanupTests`）。
3. `prompt()` Promise 返回 **不是** `LeaseStatus.Dispatched` 信号（删除 prompt-return-equals-dispatch-complete）。
4. `Dispatched` 的唯一写入入口：`recordHostAcceptedContinuation`（host evidence：`chat.message` / `HostReceiptWaiter`）。

## 调用链（Hook → Coordinator → Executor → Dispatcher）

```text
OpenCode plugin event
  → Hosts/OpenCode/Fallback/Hook.createOpencodeFallbackHandler
    → Runtime/Fallback/Coordinator.createHandler  (per-session SerialQueue)
      → handleEvent → handleFallbackTransition
        → FallbackCoordination.executeAction
          → ContinuationExecution.handleSendContinueAction
              append continuation_requested + commit PendingLease(Requested)
              return Some SendContinueIntent
        → queue 退出后 ContinuationIntentExecution.run  (effect re-enter)
          → ContinuationExecutionCore.executeContinuationIntent
            → executeSendContinue
              → runWithRetryGovernor / dispatchWithLeaseTransition
                  append continuation_dispatch_started
                  Requested → DispatchStarted
                  executor.SendContinue(...)          ← 唯一物理 SendContinue
  → Hosts/OpenCode/Fallback/ActionExecutor.sendContinueImpl
    → SessionDispatcher.Dispatch
      → ContinuationPromptBuilder.buildContinuationPrompt
          client.session.prompt(...)
      → HostReceiptWaiter.awaitWithTimeout
      → recordHostAcceptedContinuation               ← Dispatched SSOT
```

并行证据入口（同一 SSOT 函数，非第二发送路径）：

```text
chat.message hook
  → ChatHooksClassification.tryAcceptFallbackContinuation
      HostReceiptWaiterRegistry.tryResolve
      tryAcceptPendingLease (sync 状态)
      recordHostAcceptedContinuation (事件 + InjectedAt once-flag)
```

## 状态语义

| 阶段 | 信号 | 写入点 |
| :--- | :--- | :--- |
| Requested | FSM 决策 + lease 落盘 | `handleContinuationAction` |
| DispatchStarted | 即将调用宿主 API | `dispatchWithLeaseTransition` |
| Dispatched | 宿主 user message / receipt | `recordHostAcceptedContinuation` **only** |
| Running | session.busy | `updateBusyLeases` |
| Settled / Cancelled / Failed | idle/error/abort/preempt | `finishContinuation` 等 |

`handleTransportReturned`（旧名 `handleDispatchComplete`）在 `dispatchAction` 返回后 **只观察、不提升** lease 到 Dispatched。

## 单物理路径不变量（表征测试）

- 生产源码中 `executor.SendContinue` 调用点仅 `ContinuationExecutionCore.executeSendContinue`。
- OpenCode 侧 `IActionExecutor` 实现仅 `Hosts/OpenCode/Fallback/ActionExecutor.fs`。
- 不存在 `ContinuationHost.fs` / supervisor / command processor 文件。
- `recordHostAcceptedContinuation` 是唯一 `Requested|DispatchStarted → Dispatched` 的 effect 路径（纯函数 `tryAcceptPendingLease` 可被其或 ChatHooks sync 门闩调用）。

## 与旧文档差异

- `docs/12-fallback.md` / `docs/14-host-opencode.md` 中 v2 ContinuationHost 表为历史残留；以本文与磁盘源码为准。
- SPEC F-05：主路径 = ActionExecutor + SessionDispatcher + host-receipt Dispatched；不是未落地的 ContinuationHost 双架构。
