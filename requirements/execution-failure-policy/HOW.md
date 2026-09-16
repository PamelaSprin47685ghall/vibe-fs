# execution-failure-policy — HOW

## 架构与核心机制

### Typed normalization

每个公开边界只做一次结构化解码：公开 Host/SDK evidence、persistence receipt、capacity admission result 与本地 invariant violation 被转换为 `ExecutionFailure` 加 diagnostic payload。persistence receipt 必须穷尽解码为 `NotCommitted | Committed | Unknown`；只有明确证明 append 未写入事实的拒绝才是 `NotCommitted`，缺少 definitive evidence 时必须是 `Unknown`。diagnostic payload 不进入 policy 输入的决策字段。无法解码为既有构造的新物理失败必须 fail closed 并推动扩展代数，而非落入默认分支。

### Direct CE decision kernel

`ExecutionFailurePolicy.decide` 是唯一裁决点，由直接 F# CE 执行：

```text
ExecutionFailure
× DurableExecutionLifecycle
× CapacityOwnership
× ProviderRecoveryFacts
→ ExecutionFailureDecision
```

其中 `ProviderRecoveryFacts = LogicalRun × ProviderRun × RequestKind × RetryBudget × Breaker`，`RetryBudget = Available | Exhausted` 是唯一的恢复预算，不存在第二预算；`Breaker = Closed | Open`。

输入与输出都是封闭不可变数据；CE 各分支通过 `return` 结束并直接产生唯一 `ExecutionFailureResolution`，不生成 AST、自由单子或第二解释器。每个分支严格收敛为互斥的 Resolution（`PreserveCurrentFact`、`AwaitAcceptanceReconciliation`、`RetryFreshAttempt`、`TerminalizeAcceptedPreProvider`、`TerminalizeProviderStarted`）。Breaker、capacity settlement 与 fatality 均作为单次求值的正交不可变事实与 Resolution 一并封装于 `ExecutionFailureDecision`。`NotCommitted` 分支按 WHAT 固定为 `Resolution = PreserveCurrentFact`、`Breaker = NoBreakerTransition`、`CapacitySettlement = RetainExactFence/NoCapacitySettlement`、`Fatality = NoFatality`，不越过被拒绝的 transaction step。provider retry 只在 provider 两类分支求值：`ProviderStarted × Closed × Available × 可恢复 request kind` 输出 `RetryFreshAttempt`，`Open` 或 `Exhausted`、不可恢复 request kind、非 `ProviderStarted` phase 一律输出该 phase 合法的 terminal/preserve。breaker、capacity 与 fatality 不允许由解释器二次推导。

Provider recovery 输出携带 sealed `ProviderRecoveryAuthorization`，精确绑定稳定 `LogicalRunId`、本次 fresh `ProviderRunIdentity`、`ProviderRequestKind` 与由三者纯派生的 `ProviderRecoveryDecisionId`。controller 无法自行构造 licence；同一 typed decision 重放得到相同 decision id，新的 physical attempt 必得不同 provider-run/decision identity；相同 authorization identity 的重复发射由 ledger owner 去重，不由 Policy 去重。Failure JS surface 严格暴露单一 `resolution` 字段以及可空的 `authorization` 与 `terminalDisposition`，绝不暴露独立的 retry/message 决策字段。

### Phase-aware ordered interpreter

解释器先解析 typed failure 与 durable phase，再按该 phase 选择唯一合法的因果分支，不存在跨 phase 的 universal release-before-disposition 顺序：

```text
common: resolve typed failure → resolve durable execution phase

No Accepted fact:
     PreserveCurrentFact → no capacity settlement → invoke fatal boundary last if requested

AcceptanceUnknown:
     AwaitAcceptanceReconciliation(exact key) → stop repeated effect

Accepted, before ProviderStarted, terminal requested:
     execute TerminalizeAcceptedPreProvider(exact key, typed terminal)
     → Committed: release exact fence when decision requests it
                  → record exact capacity settlement outcome/unknown
                  → invoke fatal boundary last when requested
     → NotCommitted: resolve PersistenceFailure(NotCommitted), retain exact fence, stop
     → Unknown: resolve PersistenceFailure(Unknown), retain exact fence,
                enter durable reconciliation, stop repeated effect

ProviderStarted:
     confirmed provider failure with Closed + Available + recoverable kind:
       execute RetryFreshAttempt(exact sealed authorization) via provider recovery owner
     otherwise:
       settle exact capacity fence as requested
       execute TerminalizeProviderStarted(exact key, typed terminal) as requested
       → record committed/unknown evidence
       → invoke fatal boundary last when requested

Already terminal:
     PreserveCurrentFact → settle only the exact fence still proven owned
     → record settlement outcome/unknown → invoke fatal boundary last when requested
```

`PreserveCurrentFact` 与 `AwaitAcceptanceReconciliation` 不伪造 terminal。`execution-model-routing` 仅在 pre-provider terminal receipt 为 `Committed` 后消费 exact fence；`managed-chat-execution` 穷尽校验 exact key、durable phase 与 typed disposition；provider recovery owner 独占解释 provider-started retry authorization；`host-boundary` 在该 phase 的全部前置动作完成后才执行 fatal。definitive `NotCommitted` 停在被拒绝步骤之前；`Unknown` 保持 uncertainty，且两者都不得以 finally/cleanup 释放 pre-provider fence 或重复物理 effect。
