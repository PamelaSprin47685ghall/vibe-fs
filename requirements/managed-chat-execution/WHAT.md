# managed-chat-execution — WHAT

## CHATEXEC-001: 唯一 owner 与 exact execution key

`managed-chat-execution` 是 durable managed chat execution 的唯一 owner。每个执行仅由 exact `(SessionId, PhysicalUserMessageId)` 标识；`SessionId` 可依次承载多个执行，不拥有 session-scoped current execution 终态、租约或替代身份。

## CHATEXEC-002: Versioned durable fact vocabulary

每个 execution 的历史只由带 schema version 的 `Accepted`、`ProviderStarted` 与 terminal disposition 事实构成。旧版本必须经纯、确定、逐级升级后再折叠；进程状态、日志文字与 Host mutable projection 均不得冒充 durable fact。

## CHATEXEC-003: 固定 transaction order

每个 managed chat 必须遵循 `resolve exact identity → durable Accepted → acquire exact capacity → bind exact execution → project into Host → settle exact execution`。`Accepted` 落盘确认前禁止获取容量、建立 binding、修改 Host message 或调用 provider；任一步失败不得越过其后继边界。

## CHATEXEC-004: Accepted 单次建立且 replay 幂等

同一 key 的等值 `Accepted` 重放是幂等 no-op；与既有 identity 或 acceptance 内容冲突必须 fail closed。新物理消息即使复用同一 `SessionId` 也必须建立新 key，不得继承前一 execution 的事实。

## CHATEXEC-005: ProviderStarted 是 provider effect 的 durable 前置

首次 provider 请求只能在 exact execution 已 durable `Accepted`、已取得并绑定 exact capacity，且 `ProviderStarted` 落盘确认后发生。等值 `ProviderStarted` 重放幂等；terminal 后到达的启动事实必须拒绝。

## CHATEXEC-006: Terminal 单赋值且只接受 typed disposition

每个 exact execution 最多持有一个 terminal disposition。相同 terminal 重放幂等，任何不同 terminal 竞争均 fail closed 且不得覆盖首个事实。terminal 决策只接受 `execution-failure-policy` 发布的 closed typed disposition 或明确的 Host success/cancel/delete evidence；free-form text 仅供诊断，不承载 retry、fallback、breaker、capacity、message 或 fatal 语义。

## CHATEXEC-007: Pre-provider failure 精确 settlement

`Accepted` 后、`ProviderStarted` 前发生的拒绝、取消、删除、binding 或 Host projection 失败，必须针对 exact key 写入 typed terminal disposition；若已取得容量则在该 terminal 持久化确认后精确归还。该路径不得调用 provider，不得释放或终结同一 session 的其他 execution。

## CHATEXEC-008: Recovery 只在 durability activation 后事件驱动

插件构造必须是纯 wiring：不得读取 durable execution、启动恢复、获取容量或注册会推进状态的后台工作。durable substrate 激活成功后，recovery 才可折叠非终态 execution，并由 projection activation、capacity change、Host evidence 或 typed failure 事件驱动重入普通准入/settlement 流程；禁止 timer、sleep、deadline、轮询或重启次数参与正确性。

## CHATEXEC-009: Process-local artifact 永不持久化

capacity lease handle、waiter、callback、queue node、cancellation token 与 subscription 均属 process-local artifact，不得写入 execution facts、快照或恢复 token。恢复只能从 durable semantic facts 重建新的本地 artifact；旧 artifact 的缺失不能被解释为 terminal disposition。

## CHATEXEC-010: Cancel/Delete 精确终结并排空

logical cancel 与 session delete 必须枚举 durable projection 中该作用域内尚未 terminal 的 exact execution key，逐个请求 typed settlement，并等待每个已准入 execution 完成 durable terminal 与 exact capacity 归还后再宣告生命周期排空。禁止 session-wide blind release、timer grace period 或 polling 判断完成。

## CHATEXEC-011: Acceptance 原子消费 exact AttemptExecutionProfile

`Accepted` 必须原子携带 `interaction-authority` 发布的 exact `AttemptExecutionProfile`，包括其完整版本化 `ParticipantIdentityEvidence`。execution 只逐字段投影该 evidence；不得从 agent 名称、Session cache、Host parent、model 或旧 execution 推导、补全、改写或独立缓存 Role、initial Tier、Persona、Peer 与 provenance/version。复用 `SessionId` 时，profile 的 LogicalRunId 与 evidence 必须属于 durable exact prior-run closure 之后安装的当前 run；不匹配即 fail closed。
