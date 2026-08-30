# execution-failure-policy — WHAT

## EXECFAIL-001: 失败分类是封闭代数

执行失败必须在最早可信边界收敛为下列穷尽分类：

```text
LocalInvariant
ProtocolRejection
AuthorizationDenied
UserCancelled
Superseded
CapacityQueueFull
ProviderTransient
ProviderPermanent
AcceptanceUnknown
StreamInterruptedAfterFirstToken
PersistenceFailure(NotCommitted | Committed | Unknown)
```

持久化提交结果是封闭代数：`NotCommitted` 表示 receipt 已明确证明本次 append 未写入任何事实，`Committed` 表示失败事实已确认提交，`Unknown` 表示是否提交不可证明。明确拒绝不得折叠成 `Unknown`，也不得伪装成 protocol/provider failure。任何新增类别或提交结果必须扩展代数及穷尽策略，严禁用 unknown/wildcard 分支吸收。异常类型、状态码与公开 Host evidence 可以参与边界解码；message、stack、stderr 等自由文本只可作为诊断附件，严禁决定类别或后果。

## EXECFAIL-002: 唯一纯策略输出覆盖六个处置维度

唯一 policy owner 接收 typed failure、durable execution phase、确切 capacity ownership、provider recovery budget/breaker facts，纯计算一个不可拆分的 `ExecutionFailureDecision`：

```text
{ retry
  fallback
  breaker
  capacitySettlement
  messageDisposition
  fatality }
```

每个字段均为封闭类型；所有分类及 persistence commit state 必须显式给出六项结果。调用方只能解释这一个 decision，严禁任一边界另算其中某项、按异常文本覆盖结果，或以 wildcard 给出默认 retry/fallback。

`PersistenceFailure(NotCommitted)` 的穷尽结果固定为：`retry = NoRetry`、`fallback = NoFallback`、`breaker = NoBreakerTransition`、`capacitySettlement = RetainExactFence(exact fence)`（未持有 fence 时为 `NoCapacitySettlement`）、`messageDisposition = KeepCurrentFact`、`fatality = NoFatality`。它表示当前 transaction step 明确未提交，因此保留当前 durable phase 与已持有的 exact fence，并停止在所有后继边界之前；后续只能由新的 typed persistence/recovery event 重新裁决。该分支不得改变 provider retry/fallback 规则。

## EXECFAIL-003: 只有 provider 类别可授权 retry 与 fallback

`ProviderTransient` 与 `ProviderPermanent` 是仅有可进入 provider retry/fallback 裁决的类别。策略还必须结合 durable budget、breaker 与 request kind 明确选择 `NoRetry | RetryFreshAttempt` 及 `NoFallback | AdvanceFallback`；类别本身不保证一定继续。其余类别始终输出 `NoRetry + NoFallback`。特别地，`AcceptanceUnknown` 只能进入 durable reconciliation，`StreamInterruptedAfterFirstToken` 不得自动重放可能已产生可见 token 的 effect。

## EXECFAIL-004: 容量结算只作用于 exact opaque fence

策略依据 typed ownership 输出 `NoCapacitySettlement | RetainExactFence | ReleaseExactFence`。`ReleaseExactFence` 必须携带本次 admission 所得的不可伪造 fence identity，并由 `execution-model-routing` 原子消费；无 fence、旧 epoch、错误 target 或错误 physical message 均不得释放任何容量。fatality、取消、supersede 与失败恢复都不能使用计数减一、session-wide release 或 best-effort cleanup 代替 exact settlement。

## EXECFAIL-005: 消息处置是 typed command，不是第二状态机

`messageDisposition` 只能是交付给 `managed-chat-execution` 的封闭 command：

```text
KeepCurrentFact
TerminalizeAcceptedPreProvider(ExactExecutionKey, TypedTerminalDisposition)
TerminalizeProviderStarted(ExactExecutionKey, TypedTerminalDisposition)
AwaitAcceptanceReconciliation(ExactExecutionKey)
```

`TerminalizeAcceptedPreProvider` 是 binding、Host projection、拒绝、取消或删除在 durable `Accepted` 后且 `ProviderStarted` 前失败时的 typed terminal command；`TerminalizeProviderStarted` 只适用于已存在 durable `ProviderStarted` 的 execution。`managed-chat-execution` 必须按 exact key 与 durable phase 穷尽验证 command，拒绝跨 phase 或不同 execution 的处置。`execution-failure-policy` 不直接写消息事实，也不重定义 `Accepted → ProviderStarted → terminal` 的合法迁移。任何 diagnostic text 均不得成为 terminal disposition。

## EXECFAIL-006: fatal 必须在确切结算之后执行

`FatalAfterSettlement` 只由 `LocalInvariant` 或无法安全继续的 `PersistenceFailure` policy 分支产生。解释器必须按 durable phase 执行 decision：对 `Accepted` 后、`ProviderStarted` 前的 terminal disposition，必须先取得 exact terminal append 的 `Committed` receipt，之后才可执行 `ReleaseExactFence`；该 append 为 `NotCommitted` 或 `Unknown` 时不得释放 fence。其他 phase 只执行各自合法的 disposition/settlement dependency，严禁套用 universal release-before-disposition sequence。所有 phase 中，fatal boundary 始终是最后一步：只有 decision 要求的 disposition 与 exact capacity settlement 已成功，或对应提交状态已有 durable unknown evidence，才可调用。严禁 catch 后立即退出、吞掉 pre-fatal settlement 失败，或让 Host/UI 私有行为代替结算证据。

## EXECFAIL-007: 提交未知保持未知且禁止重复 effect

`AcceptanceUnknown` 与 `PersistenceFailure(Unknown)` 必须保留显式 uncertainty，依靠 durable read/reconciliation 或外部 physical evidence 收敛。它们不得被映射为 `NotCommitted`、“未发生”、provider transient、retryable 或成功；在收敛前不得重复发送消息、重复 provider attempt、重复获取或释放容量或推进 fallback。只有明确证明本次 append 未写入事实的 receipt 才可形成 `PersistenceFailure(NotCommitted)`。

## EXECFAIL-008: 决策与恢复时间无关

policy 与 interpreter 的推进仅由 typed input、durable fact、capacity event、Host terminal evidence 或 persistence result 驱动。deadline、sleep、elapsed time、轮询次数与错误文本不得授权 retry、fallback、breaker transition、capacity settlement、message terminal 或 fatality。
