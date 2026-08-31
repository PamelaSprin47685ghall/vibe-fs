# execution-failure-policy — HOW

## 架构与核心机制

### Typed normalization

每个公开边界只做一次结构化解码：公开 Host/SDK evidence、persistence receipt、capacity admission result 与本地 invariant violation 被转换为 `ExecutionFailure` 加 diagnostic payload。persistence receipt 必须穷尽解码为 `NotCommitted | Committed | Unknown`；只有明确证明 append 未写入事实的拒绝才是 `NotCommitted`，缺少 definitive evidence 时必须是 `Unknown`。diagnostic payload 不进入 policy 输入的决策字段。无法解码为既有构造的新物理失败必须 fail closed 并推动扩展代数，而非落入默认分支。

### Pure decision kernel

`ExecutionFailurePolicy.decide` 是唯一六维裁决点：

```text
ExecutionFailure
× DurableExecutionLifecycle
× CapacityOwnership
× ProviderRecoveryFacts
→ ExecutionFailureDecision
```

输入与输出都是封闭不可变数据；模式匹配穷尽全部 failure constructors 与 `NotCommitted | Committed | Unknown` persistence commit states，并在每个分支构造 retry、fallback、breaker、capacity settlement、message disposition、fatality 六项。`NotCommitted` 分支按 WHAT 固定为 `NoRetry + NoFallback + NoBreakerTransition + RetainExactFence/NoCapacitySettlement + KeepCurrentFact + NoFatality`，不越过被拒绝的 transaction step。provider retry/fallback 只在 provider 两类分支求值。breaker、capacity、message 与 fatality 不允许由解释器二次推导。

Provider recovery 输出携带 sealed `ProviderRecoveryAuthorization`，精确绑定稳定 `LogicalRunId`、本次 fresh `ProviderRunIdentity`、`ProviderRequestKind` 与由三者纯派生的 `ProviderRecoveryDecisionId`。controller 无法自行构造 licence；同一 typed decision 重放得到相同 decision id，新的 physical attempt 必得不同 provider-run/decision identity。

### Phase-aware ordered interpreter

解释器先解析 typed failure 与 durable phase，再按该 phase 选择唯一合法的因果分支，不存在跨 phase 的 universal release-before-disposition 顺序：

```text
common: resolve typed failure → resolve durable execution phase

No Accepted fact:
     KeepCurrentFact → no capacity settlement → invoke fatal boundary last if requested

AcceptanceUnknown:
     AwaitAcceptanceReconciliation(exact key) → stop repeated effect

Accepted, before ProviderStarted, terminal requested:
     submit TerminalizeAcceptedPreProvider(exact key, typed terminal)
     → Committed: release exact fence when decision requests it
                  → record exact capacity settlement outcome/unknown
                  → invoke fatal boundary last when requested
     → NotCommitted: decide PersistenceFailure(NotCommitted), retain exact fence, stop
     → Unknown: decide PersistenceFailure(Unknown), retain exact fence,
                enter durable reconciliation, stop repeated effect

ProviderStarted:
     settle exact capacity fence as requested
     → submit TerminalizeProviderStarted(exact key, typed terminal) as requested
     → record committed/unknown evidence
     → invoke fatal boundary last when requested

Already terminal:
     KeepCurrentFact → settle only the exact fence still proven owned
     → record settlement outcome/unknown → invoke fatal boundary last when requested
```

`KeepCurrentFact` 与 `AwaitAcceptanceReconciliation` 不伪造 terminal。`execution-model-routing` 仅在 pre-provider terminal receipt 为 `Committed` 后消费 exact fence；`managed-chat-execution` 穷尽校验 exact key、durable phase 与 typed disposition；`provider-attempt-recovery` 消费 retry/fallback authorization；`host-boundary` 在该 phase 的全部前置动作完成后才执行 fatal。definitive `NotCommitted` 停在被拒绝步骤之前；`Unknown` 保持 uncertainty，且两者都不得以 finally/cleanup 释放 pre-provider fence 或重复物理 effect。

## 规划中的可执行证明

| 命题 | 唯一落点测试 |
|---|---|
| EXECFAIL-001 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-001] observes every closed failure and persistence commitment variant`；`requirements/execution-failure-policy/tests/host-codec.test.mjs::WHAT[EXECFAIL-001] Host adapter returns closed typed failures from structural evidence`；`requirements/execution-failure-policy/tests/provider-mapping.test.mjs::WHAT[EXECFAIL-001] adapter returns typed ProviderTransient`；`requirements/execution-failure-policy/tests/provider-mapping.test.mjs::WHAT[EXECFAIL-001] provider adapter preserves permanent kind and exact attempt identity` |
| EXECFAIL-002 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-002] every phase and failure yields exactly six closed decision dimensions`；`requirements/execution-failure-policy/tests/cancel-retry-fallback-stream.test.mjs::WHAT[EXECFAIL-002] cancel/retry/fallback/stream matrix is interpreted by registered owners` |
| EXECFAIL-003 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-003] rejects illegal retry and breaker policy mutations`；`requirements/execution-failure-policy/tests/cancel-retry-fallback-stream.property.test.mjs::WHAT[EXECFAIL-003] generated budgets, fallback, persistence, Host evidence, and Hook Promises preserve one owner licence` |
| EXECFAIL-004 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-004] capacity settlement preserves the exact opaque fence reference`；`requirements/execution-failure-policy/tests/capacity-mapping.test.mjs::WHAT[EXECFAIL-004] wrong exact capacity fence identity returns closed conflict`；`requirements/execution-failure-policy/tests/capacity-mapping.test.mjs::WHAT[EXECFAIL-004] stale exact capacity fence is closed without exposing handle` |
| EXECFAIL-005 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-005] message disposition carries the exact execution key and typed terminal` |
| EXECFAIL-006 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-006] LocalInvariant requests fatality only after typed settlement commands`；`requirements/execution-failure-policy/tests/cancel-retry-fallback-stream.property.test.mjs::WHAT[EXECFAIL-006] retry, fatal, and exact-release mutations violate closed policy laws` |
| EXECFAIL-007 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-007] persistence commitment remains explicit and uncertainty reconciles without repeated effect`；`requirements/execution-failure-policy/tests/persistence-mapping.test.mjs::WHAT[EXECFAIL-007] journal writer outcomes preserve exact persistence commitment` |
| EXECFAIL-008 | `requirements/execution-failure-policy/tests/policy.test.mjs::WHAT[EXECFAIL-008] policy is deterministic and ignores diagnostic or temporal decoration`；`requirements/execution-failure-policy/tests/host-codec.test.mjs::WHAT[EXECFAIL-008] Host classification ignores diagnostic wording`；`requirements/execution-failure-policy/tests/provider-mapping.test.mjs::WHAT[EXECFAIL-008] provider diagnostic text never drives classification`；`requirements/execution-failure-policy/tests/persistence-mapping.test.mjs::WHAT[EXECFAIL-008] persistence diagnostics cannot change commitment` |
