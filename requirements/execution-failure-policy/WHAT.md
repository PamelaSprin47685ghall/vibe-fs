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

异常类型、状态码与公开 Host evidence 可以参与边界解码；message、stack、stderr 等自由文本只可作为诊断附件，严禁决定类别或后果。

## EXECFAIL-002: 唯一纯策略输出覆盖单一互斥 Resolution 与正交处置维度

唯一 policy owner 接收 typed failure、durable execution phase、确切 capacity ownership、provider recovery budget/breaker facts，通过直接 F# CE 纯计算一个不可拆分的 `ExecutionFailureDecision`：

```text
ExecutionFailureResolution =
  | PreserveCurrentFact
  | AwaitAcceptanceReconciliation of ChatExecutionKey
  | RetryFreshAttempt of ProviderRecoveryAuthorization
  | TerminalizeAcceptedPreProvider of ChatExecutionKey * ChatExecutionTerminalDisposition
  | TerminalizeProviderStarted of ChatExecutionKey * ChatExecutionTerminalDisposition

{ Resolution: ExecutionFailureResolution
  Breaker: BreakerDecision
  CapacitySettlement: CapacitySettlement
  Fatality: FatalityDecision }
```

彻底开除独立的 `RetryDecision` 与 `MessageDisposition` 维度及其非法积状态空间；`ExecutionFailureResolution` 是互斥和类型，每个失败回合严格收敛为一个继续分支（`RetryFreshAttempt`）或一个终结分支（`TerminalizeAcceptedPreProvider`、`TerminalizeProviderStarted`、`AwaitAcceptanceReconciliation`、`PreserveCurrentFact`），严禁“既不重试也不终结”或“既重试又终结”的矛盾局面。Breaker、capacity settlement 与 fatality 均作为单次求值的正交不可变事实与 Resolution 一同给出。调用方只能解释这一个 decision，严禁任一边界另算其中某项、按异常文本覆盖结果，或以 wildcard 给出默认 retry。

`PersistenceFailure(NotCommitted)` 的穷尽结果固定为：`Resolution = PreserveCurrentFact`、`Breaker = NoBreakerTransition`、`CapacitySettlement = RetainExactFence(exact fence)`（未持有 fence 时为 `NoCapacitySettlement`）、`Fatality = NoFatality`。它表示当前 transaction step 明确未提交，因此保留当前 durable phase 与已持有的 exact fence，并停止在所有后继边界之前；后续只能由新的 typed persistence/recovery event 重新裁决。该分支不得改变 provider retry 规则。

## EXECFAIL-003: 只有已确认 provider 类别可授权 retry

`ProviderTransient` 与 `ProviderPermanent` 是仅有可进入 provider retry 裁决的类别。`ProviderRecoveryFacts` 只携带一个 `RetryBudget（Available | Exhausted）`、一个 `Breaker（Closed | Open）`、`ProviderRequestKind` 与两个 run identity，不存在第二预算维度。已确认 provider 失败在 `ProviderStarted × Closed × Available × 可恢复 request kind` 时输出 `RetryFreshAttempt`；`Open` 或 `Exhausted` 时输出 terminal；`NoAcceptedFact`、`AcceptedBeforeProvider`、`Terminal` phase 永不重试；`StrengthReplica` 永不消耗 owner recovery。类别本身不保证一定继续。其余类别始终不输出 retry。特别地，`AcceptanceUnknown` 只能进入 durable reconciliation，`StreamInterruptedAfterFirstToken` 不得自动重放可能已产生可见 token 的 effect；解码为非 provider 类别的 unknown、timeout 文案、cancel、tool-policy、join-control、pre-accept refusal 永不进入 retry 裁决（Host 错误边界自身的解码结果一律为 provider 类别，见 EXECFAIL-009）。

任一 `RetryFreshAttempt` 必须携带不可由 caller 构造的 sealed authorization，精确绑定一个稳定 logical operation (`LogicalRunId`)、本次 physical attempt (`ProviderRunIdentity`)、request kind 与由三者纯派生的稳定 policy decision identity。Provider 恢复 prompt 标识精确包含 `ProviderRecoveryDecisionId` 与源 `ProviderRunIdentity`，对外可见文本保持完全一致。同一 typed decision 重放得到相同 decision identity；新的失败 provider run 建立新 identity。相同 authorization identity 的重复物理发射由 ledger owner 去重，不由 Policy 去重。

## EXECFAIL-004: 容量结算只作用于 exact opaque fence

策略依据 typed ownership 输出 `NoCapacitySettlement | RetainExactFence | ReleaseExactFence`。`ReleaseExactFence` 必须携带本次 admission 所得的不可伪造 fence identity，并由 `execution-model-routing` 原子消费；无 fence、旧 epoch、错误 target 或错误 physical message 均不得释放任何容量。fatality、取消、supersede 与失败恢复都不能使用计数减一、session-wide release 或 best-effort cleanup 代替 exact settlement。

## EXECFAIL-005: 终态处置直属 Resolution，不是第二状态机

消息终态直接由 `ExecutionFailureResolution` 中的 `TerminalizeAcceptedPreProvider(ExactExecutionKey, TypedTerminalDisposition)` 与 `TerminalizeProviderStarted(ExactExecutionKey, TypedTerminalDisposition)` 表述，不再设独立于 Resolution 之外的第二状态机或自由字段。

`TerminalizeAcceptedPreProvider` 是 binding、Host projection、拒绝、取消或删除在 durable `Accepted` 后且 `ProviderStarted` 前失败时的 typed terminal command；`TerminalizeProviderStarted` 只适用于已存在 durable `ProviderStarted` 的 execution。`managed-chat-execution` 必须按 exact key 与 durable phase 穷尽验证 command，拒绝跨 phase 或不同 execution 的处置。`execution-failure-policy` 不直接写消息事实，也不重定义 `Accepted → ProviderStarted → terminal` 的合法迁移。任何 diagnostic text 均不得成为 terminal disposition。

## EXECFAIL-006: fatal 必须在确切结算之后执行

`FatalAfterSettlement` 只由 `LocalInvariant` 或无法安全继续的 `PersistenceFailure` policy 分支产生。解释器必须按 durable phase 执行 decision：对 `Accepted` 后、`ProviderStarted` 前的 terminal disposition，必须先取得 exact terminal append 的 `Committed` receipt，之后才可执行 `ReleaseExactFence`；该 append 为 `NotCommitted` 或 `Unknown` 时不得释放 fence。其他 phase 只执行各自合法的 disposition/settlement dependency，严禁套用 universal release-before-disposition sequence。所有 phase 中，fatal boundary 始终是最后一步：只有 decision 要求的 disposition 与 exact capacity settlement 已成功，或对应提交状态已有 durable unknown evidence，才可调用。严禁 catch 后立即退出、吞掉 pre-fatal settlement 失败，或让 Host/UI 私有行为代替结算证据。

## EXECFAIL-007: 提交未知保持未知且禁止重复 effect

`AcceptanceUnknown` 与 `PersistenceFailure(Unknown)` 必须保留显式 uncertainty，依靠 durable read/reconciliation 或外部 physical evidence 收敛。它们不得被映射为 `NotCommitted`、“未发生”、provider transient、retryable 或成功；在收敛前不得重复发送消息、重复 provider attempt、重复获取或释放容量。只有明确证明本次 append 未写入事实的 receipt 才可形成 `PersistenceFailure(NotCommitted)`。

## EXECFAIL-008: 决策与恢复时间无关

policy 与 interpreter 的推进仅由 typed input、durable fact、capacity event、Host terminal evidence 或 persistence result 驱动。deadline、sleep、elapsed time、轮询次数与错误文本不得授权 retry、breaker transition、capacity settlement、terminal resolution 或 fatality。

## EXECFAIL-009: Host 错误边界不做失败分类

除非 typed control，Host 上报的任何错误一律解码为 `ProviderTransient`：
operator abort → `UserCancelled`，supersede → `Superseded`（CRASH-008 / HOST-002）。
工具调用等语义错误由 OpenCode 在自己的循环内解决，以 part 而非 session error 呈现，不经过本边界。

理由：边界处没有可信证据区分 upstream 失败类别；任何“猜类别”的终点都是把 provider 噪音变成调用方可见的终态。
重试、预算与上下文替换由 provider recovery 路径独占决定（PAR-005/PAR-008/PAR-019），
预算耗尽仍按 HOSTFAIL-006 产生唯一 typed terminal。
本边界不削弱 HOSTFAIL-004：plugin/config/schema/permission 等 **hook 失败**仍 fail-loud。

`LocalInvariant`、`ProtocolRejection`、`AuthorizationDenied`、`StreamInterruptedAfterFirstToken` 等类别仍保留在封闭代数中，
由 provider adapter、持久化边界与 legacy 解码使用；EXECFAIL-003 关于“这些类别永不进入 retry”的约束适用于这些边界。
重试必须以替换上下文的方式发出（PAR-011 wire 重建 prefix、丢弃旧 retry 行、`ProviderRetryAttempt` continuation），不得盲目重放已产生可见输出的 attempt。

## EXECFAIL-010: 致命入口逐处有据

所有生产 fatal 入口及转入该入口的边界必须纳入可检查清单。每处记录触发前提、所属状态、不可达证明或实际回归；新增入口、入口漂移和缺失证据必须使标准验证失败。性质测试调用编译后的生产实现，固定 seed 并允许重放反例；测试自造的决策模型不能代替生产实现证据。

协议修复耗尽、过期回调、确定未发送和运行环境拒绝不得仅因“开发时没预料”成为 LocalInvariant。可达失败必须由所属请求或资源明确收敛；不释放其他请求的所有权，不重发 outcome unknown 的 effect，不在 fatal 后安排结算。保留的不变量熔断仍须满足 EXECFAIL-006。

## EXECFAIL-011: 未分类 hook 失败必须携带自有证据

hook 抛出的不可识别异常不拥有"无已接受事实、无所属执行"的免费推定。边界只能从 hook 实参证明执行身份（`sessionID` 字段或 output.messages 单一会话 + 末尾物理用户消息），能证明则以该 `ChatExecutionKey` 下发 `SettlementIncomplete`，后续处置交由相应 phase 的 settlement owner，不补造执行已干净的记录；不能证明则 key 保持 None。未知类别不升级为 process fatal，也不降级为可重试 provider 噪音——它作为不变的 rethrow 信号交给 Host，fatal 决策保留给证据齐全的 phase。
