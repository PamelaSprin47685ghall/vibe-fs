# managed-chat-execution — WHAT

## CHATEXEC-001: 唯一 owner 与 exact execution key

`managed-chat-execution` 是 durable managed chat execution 的唯一 owner。每个执行仅由 exact `(SessionId, PhysicalUserMessageId)` 标识；`SessionId` 可依次承载多个执行，不拥有 session-scoped current execution 终态、租约或替代身份。

## CHATEXEC-002: Versioned durable fact vocabulary

每个 execution 的历史只由带 schema version 的 `Accepted`、`ProviderStarted` 与 terminal disposition 事实构成。`Accepted` 携带 pre-provider `AcceptedChatExecutionEvidence`：exact key、IdentitySeed、EffectiveAgent、PromptOrigin 与 authority evidence，禁止包含尚未存在的 ProviderRun。`ProviderStarted` 携带 `ProviderStartedExecutionEvidence`：完整 accepted evidence 加 Host 实际观察到的 ProviderRunIdentity、ProviderRequestKind 与 projection choice。terminal evidence 是 closed DU：`PreProvider` 携带 accepted evidence，或 `AfterProviderStart` 携带 started evidence；不得用 option/bool 拼接阶段。旧版本必须经纯、确定、逐级升级后再折叠；进程状态、日志文字与 Host mutable projection 均不得冒充 durable fact。

## CHATEXEC-003: 固定 transaction order

每个 managed chat 必须遵循 `resolve pre-provider identity → durable Accepted → acquire exact capacity → bind exact execution key → project into Host → settle admission → provider effect`。Host 实际暴露 ProviderRun 后才可建立 started evidence 并持久 `ProviderStarted`。`Accepted` 落盘确认前禁止获取容量、建立 binding、修改 Host message 或调用 provider；任一步失败不得越过其后继边界，任何边界不得预测或伪造 ProviderRunIdentity。

## CHATEXEC-004: Accepted 单次建立且 replay 幂等

同一 key 的等值 pre-provider `Accepted` evidence 重放是幂等 no-op；与既有 identity 或 acceptance 内容冲突必须 fail closed。新物理消息即使复用同一 `SessionId` 也必须建立新 key，不得继承前一 execution 的事实。ProviderStarted 只能在 accepted evidence 完全相等后增加 Host-observed run evidence。

## CHATEXEC-005: ProviderStarted 是 provider effect 的 durable 前置

首次 provider body 只能在 exact execution 已 durable `Accepted`、已取得并绑定 exact capacity，Host 已建立 exact ProviderRunIdentity，且对应 `ProviderStarted` 落盘确认后发生。每个 exact provider-start observation 必须在任何 failure/idle wake 前把 reconciler physical cursor 推到自身 `PhysicalUserMessageId`，但不得因此铸造 authority。该事实把整个 physical user-message execution 推入 provider phase；同一 physical execution 内 tool result 触发的后续 Host assistant ProviderRun 复用这一已持久化 phase，不得要求第二份 frozen admission plan，也不得追加第二个 `ProviderStarted`。等值 public `message.updated` 重放幂等；terminal 后到达的首次启动事实必须拒绝。

## CHATEXEC-006: Terminal 单赋值且只接受 typed disposition

每个 exact execution 最多持有一个 terminal disposition。`PreProvider` 只允许 `Cancelled | Rejected | Failed`，禁止 `Completed`；`AfterProviderStart` 绑定 exact started evidence并允许四种 disposition。相同 evidence+terminal 重放幂等，任何不同 evidence 或 terminal 竞争均 fail closed 且不得覆盖首个事实。terminal 决策只接受 `execution-failure-policy` 发布的 closed typed disposition 或明确的 Host success/cancel/delete evidence；free-form text 仅供诊断，不承载 retry、fallback、breaker、capacity、message 或 fatal 语义。

## CHATEXEC-007: Pre-provider failure 精确 settlement

`Accepted` 后、`ProviderStarted` 前发生的拒绝、取消、删除、binding 或 Host projection 失败，必须针对 exact key 写入 typed terminal disposition；若已取得容量则在该 terminal 持久化确认后精确归还。该路径不得调用 provider，不得释放或终结同一 session 的其他 execution。

## CHATEXEC-008: Recovery 只在 durability activation 后事件驱动

插件构造必须是纯 wiring：不得读取 durable execution、启动恢复、获取容量或注册会推进状态的后台工作。durable substrate 激活成功后，recovery 才可折叠非终态 execution，并由 projection activation、capacity change、Host evidence 或 typed failure 事件驱动重入普通准入/settlement 流程；禁止 timer、sleep、deadline、轮询或重启次数参与正确性。

## CHATEXEC-009: Process-local artifact 永不持久化

capacity lease handle、waiter、callback、queue node、cancellation token 与 subscription 均属 process-local artifact，不得写入 execution facts、快照或恢复 token。恢复只能从 durable semantic facts 重建新的本地 artifact；旧 artifact 的缺失不能被解释为 terminal disposition。

## CHATEXEC-010: Cancel/Delete 精确终结并排空

logical cancel 与 session delete 必须枚举 durable projection 中该作用域内尚未 terminal 的 exact execution key，逐个请求 typed settlement，并等待每个已准入 execution 完成 durable terminal 与 exact capacity 归还后再宣告生命周期排空。禁止 session-wide blind release、timer grace period 或 polling 判断完成。

## CHATEXEC-011: Acceptance 原子消费 pre-provider authority evidence

`Accepted` 必须原子消费 Task14 frozen managed intent 与 `interaction-authority` 发布的 current authority evidence，建立 exact `AcceptedChatExecutionEvidence`，包括完整版本化 `ParticipantIdentityEvidence`，但不包含 ProviderRunIdentity。Provider-start owner 随后只能把 Host-observed ProviderRunIdentity 与 accepted evidence 组合成 `ProviderStartedExecutionEvidence`。两阶段均只逐字段投影 owner-issued evidence；不得从 agent 名称、Session cache、Host parent、model 或旧 execution 推导、补全、改写或独立缓存 Role、initial Tier、Persona、Peer、provenance/version 或物理 run。复用 `SessionId` 时，LogicalRunId 与 evidence 必须属于 durable exact prior-run closure 之后安装的当前 run；不匹配即 fail closed。

## CHATEXEC-012: Recovery decision 只由 durable execution 与显式 physical evidence 决定

`managed-chat-execution` 独占纯 `Evidence → Decision` 公式。Evidence 只包含 canonical `ChatExecutionState`、exact public provider receipt observation（missing、ambiguous、absent、alive、terminal）、exact physical resource observation、typed persistence commitment，以及 `execution-failure-policy` 已发布的 retry/fallback/terminal decision。Decision 是 closed DU：`Ignore | ReconcilePhysical | ResumePreProvider | RequeueEligible | Finalize | MarkManualIntervention`；每个 effectful case 携带 Task29 所需的 exact execution evidence 或 typed provider recovery authorization，但本公式不执行 effect。

`Accepted` 且 exact provider absent 才可恢复 pre-provider admission；observed provider 必须先 reconcile durable started/terminal facts。`ProviderStarted` 且 provider alive 只观察，exact terminal 才 finalize，exact absent 只消费 failure policy authorization；typed supersession 因而只能使用 policy 发布的 exact cancelled terminal。durable terminal 若 physical resource 仍 held 则请求 exact reconciliation，否则幂等忽略。missing/ambiguous receipt、unknown persistence/resource、无 policy authorization 均 fail closed 为 manual intervention；stale external evidence 不得改写当前 execution。禁止 Role、error text、terminal prose、idle、timer、process age、cursor、registry presence 或 process-local capacity state参与判断。同一 Evidence 必须永远产生同一 Decision。

## CHATEXEC-013: Execution reliability query 只投影 canonical lifecycle

`Accepted without Terminal`、`ProviderStarted without Terminal` 与每个 `LogicalRunId` 的 physical attempt 数只能从 canonical `ChatExecutionProjection` 只读导出。查询返回不可变 process-local snapshot，不写 durable fact，不 terminalize execution，不授权 retry/fallback，不读取 diagnostic counter 作为恢复 evidence。Recovery pending/manual intervention 只投影 `PluginRecoveryScope.PendingChatRecoveryOwnership()` 的 typed ownership，不复制或清理其状态。

## CHATEXEC-014: Incident evidence capture 与 replay 无 correctness authority

Incident envelope 必须 versioned、确定序列化且只包含 canonical serialized ChatExecution facts/status、immutable capacity snapshot 与 reconciliation decision、causal diagnostics、exact public Host version/contract evidence、typed recovery observation/decision。capture 必须复用 owner projection 并拒绝未知字段；diagnostic owner 负责清除 credential、path、stack、prompt/content/payload。replay 必须重新折叠 canonical projection、重跑 capacity reconciliation 与同一 `ChatExecutionRecoveryRuntime` representation，tamper、未知 schema/字段、缺证据、Host contract 不受支持或 observation 不匹配时 fail closed。

Replay 只返回 typed owner effect request；不得写 fact、清 counter、释放 fence、改变 queue/capacity、执行 retry/fallback 或把 operator 变成 recovery authority。相同 envelope 重放必须幂等。若 exact accepted-message public replay capability 没有 Host canary evidence，operator 必须升级处理，禁止重发或手工补状态。
