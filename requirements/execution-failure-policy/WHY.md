# execution-failure-policy — WHY

执行链会在协议、权限、取消、容量、provider、持久化与本地不变量等不同边界失败。若各调用方解析异常文本并各自决定重试、熔断、容量归还、消息终态或进程退出，同一事实会产生互相矛盾的后果，甚至在提交状态未知时重复物理 effect。

`execution-failure-policy` 是失败分类与后果裁决的唯一 owner：边界先把物理错误收敛为封闭类型，纯策略再一次性给出完整处置。自由文本只用于诊断，永不授权控制流。

## 核心不变量

- 失败代数封闭且穷尽；persistence commit result 明确区分 `NotCommitted | Committed | Unknown`，新增失败语义必须新增类型分支与证明，不能落入 wildcard。
- 唯一公共裁决轴：`ExecutionFailureResolution` 是互斥和类型，每个失败回合严格收敛为一个继续分支（`RetryFreshAttempt`）或一个终结分支（`TerminalizeAcceptedPreProvider`、`TerminalizeProviderStarted`、`AwaitAcceptanceReconciliation`、`PreserveCurrentFact`），绝无既不重试也不终态或两者并存的非法积状态。
- 决策引擎由小型直接 F# CE 执行，各规则分支仅通过 `return` 一个确定 resolution 结束，无 AST、自由单子或第二解释器。熔断、容量结算与致命性是单次求值的正交不可变事实。
- 只有一个恢复预算（`RetryBudget`）：只有已确认的 provider 失败类别可以在 `Closed + Available` 时授权 retry；`Open` 或 `Exhausted` 一律终结；acceptance unknown、stream interrupted、capacity pressure 与 persistence failure 都不能伪装成 provider failure。
- pre-provider terminal 必须先 durable commit、再释放其 exact fence；其他 phase 使用各自合法顺序，fatal 始终是最后一步。
- correctness 只依赖 durable facts、typed evidence 与显式失败事件，不依赖错误文案或墙钟。

## 边界

- `managed-chat-execution` 唯一拥有 `(SessionId, PhysicalUserMessageId)` 消息事实及其 durable 状态迁移；本包只输出携带 exact key 与 typed disposition 的互斥 terminal resolution。
- `execution-model-routing` 唯一拥有 opaque fenced capacity、typed queue 与 execution binding。
- `provider-attempt-recovery` 唯一执行由本策略授权的 provider retry，并保持 logical participant run identity；相同 authorization 的重复发射由 ledger 去重。
- `host-boundary` 只负责把公开 Hook/SDK 观测译为失败代数，并执行 typed hook membrane。
