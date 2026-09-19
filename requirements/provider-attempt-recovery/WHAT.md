# provider-attempt-recovery — WHAT

## [001] Failure budget belongs to a Logical Run

Provider failure accounting is lifecycle state of one Logical Run, not a
permanent Session attribute. Each Logical Run binds a fixed participant
(SelectedAgent + Role) with its Persona identity and keeps it unchanged across
all retries. A new Logical Run never inherits the previous run's consecutive
failure count.

## [002] Budget counts consecutive failures only, corrupt bytes fail closed

ProviderFailureProjection tracks one number: the consecutive failure count.
Deserialization meeting illegal bytes returns a decode error and refuses the
corrupt envelope; it never throws uncaptured and never forges a decode failure
into an unknown submission.

## [003] Single writer, one failure advances once, replay replays

ProviderFailureLedger is the only writer allowed to record a confirmed
provider failure or exhaustion. One confirmed failure (deduped by SessionId,
LogicalRunId, AuthorityRootUserMessageId, ProviderRun) advances the failure
count at most once; when it is still the latest failure, repeat observation
writes no fact and adds no count but replays the same admission. An older
failure returns `EpisodeSuperseded` and starts no recovery. Each retry creates
a fresh physical attempt identity (new PhysicalUserMessageId and
ProviderRunIdentity); one failure causes at most one physical send.

## [004] Advance invariant and success reset

One confirmed failure adds 1 to the consecutive failure count and, while the
budget is not spent, yields an exact typed failure licence. The failed
physical provider is settled by ModelRouting under provider-attempt-recovery-021: the failure keeps
its target for the LWR retry, and only a failed LWR retry condemns the
provider. The budget never switches roles and never stands for the physical
provider pool. A main business request success writes a success fact and
resets the consecutive failure count to zero.

The request kind of a failure recovery is read only from the durable
`ChatExecutionState.ProviderStarted.RequestKind` matching
`SessionId + PhysicalUserMessageId`; a tool continuation of the same physical
request inherits that frozen kind, while the current `ProviderRunIdentity` is
constrained separately by exact recovery authorization. Generic
parent↔child session association and the active role never imply a request
kind; only a proven `BloggerMain | BloggerSquash` may use the association to
locate the owning main session. Manager, coder and other ordinary children
keep their durable `WorkMain` even when an association exists.

## [005] Bounded automatic retry budget

Automatic retry and recovery are strictly bounded (default consecutive
failure limit). When consecutive failures reach the budget the run is
exhausted and no further automatic physical request is issued; later recovery
needs a new Authority Root or an explicit user action.

## [006] Fixed participant, decoupled model routing

Each logical run has a fixed participant (Role) and system prompt. No retry
changes the participant; when execution model routing changes target it is a
scheduler backend choice for the same participant, not a domain identity
change. At consecutive-failure budget the run is exhausted at once; no
over-budget request is ever issued.

## [007] Fold rejection conditions

ProviderFailureProjection strictly refuses illegal budget records:
a consecutive failure count that is not the valid successor, a count beyond
the budget, and any further advance after exhaustion. Refusal is fail-closed
and stops the replay.

## [008] Empty / XML-only terminal never advances

An empty terminal or an XML-only terminal means the response content is
unusable, not that the provider request failed: at most one bounded
Interaction Repair follows; advancing the failure budget on it is forbidden.

## [009] Host attempt number is not the domain count

The Host transport retry sequence is Host-internal state. It is never written
into the domain consecutive failure count and never drives budget judgement
or identity derivation.

## [010] Maintenance sub-request and retry dispatch

A confirmed BloggerMain failure with retry permission, when durable frames
exist and the retry policy allows maintenance, retries first with the
maintenance sub-request `BloggerSquash`; resending BloggerMain first is
forbidden. A Squash success does not clear the consecutive failure count and
the run continues to `BloggerMain`; a Squash failure records the failure and
the policy continues with the next physical retry. With no squash material
the retry sends Main directly.

## [011] Material-based immutable retry policy with exact physical binding

Whether a physical retry carries a prefix probe or a maintenance request is
an immutable per-plan decision of that AttemptPlan, derived from the request
type and the persisted material state — never from transient cross-callback
state. No transient cross-callback channel may carry a recovery permission;
each retry gets an independent physical message identity and the plan freeze
admission completes before rendering.

## [012] Host abort / cleanup residue never advances

Host abort cleanup marking in-flight tool calls interrupted is residue, not a
confirmed provider attempt failure: it never advances the failure budget.

## [013] New provider target is a new executor, not a new identity

Failure and retry dispatch only change the next execution's physical
provider/model target. The durable logical participant run's immutable
`ParticipantIdentity` (own Role, stable Persona, provenance/version),
SessionProviderLanguage, system prompt, CanonicalRole and Authority identity
stay strictly unchanged across all retry attempts; Persona and
provenance/version inherit from that run's durable identity evidence. Each
participant binds one remote LLM target in the current execution. Only a new
run built after the exact terminal closure of the old run may take a new
identity, and machine bookkeeping never leaks into the provider horizon.

## [014] Continuation only after recorded failure within budget

A continuation of the same Logical Run may be sent only after the Host has
stopped automatic retry and the budget still allows it. Sending the
continuation advances nothing and resets nothing.

“Host has stopped automatic retry” has exactly one operational definition
(provider-attempt-recovery-022): the exact failed `ProviderRunIdentity`'s Host terminal projection
has been observed. A coarse `session.error` only finalizes the failure; it
never authorizes the physical send. provider-attempt-recovery-023 owns the obligation for an
accepted retry the Host never executed.

## [015] StrengthReplica stays out of the owner budget

A StrengthReplica attempt success or failure belongs to a speculative branch;
it never enters the owner Logical Run budget, advances nothing, and clears
nothing.

## [016] RequestKind decides success accounting

Whether a successful provider attempt writes `SuccessRecorded` is decided by
the provable `ProviderRequestKind`: a valid `WorkMain | BloggerMain` success
clears the consecutive failure count; `BloggerSquash | InteractionRepair |
StrengthReplica` does not. `finish=tool-calls` is a valid provider attempt
success, not a failure; it may settle this provider recovery while the Host
turn continues with tools. Blogger RequestKind is proven first by the current
typed request / `BloggerCycleReceipt`, Continuation kind by
`AcceptedContinuationIds`; Role or terminal text alone never reclassifies a
maintenance request as a business success.

## [017] Blogger retry replaces the exact physical binding

After a Blogger provider attempt failure the old `BloggerRequestMaterialized`
closes first as `BloggerRequestAbandoned`. Every automatic retry (same Main
context retry, Main→Squash, Squash→Main) re-materializes the typed context
and binds a new agent-free `PromptKey`; the old PromptKey never proves
ownership across a physical retry.

## [018] Recovery continuation unlocks only on durable events

After a WorkMain failure, when the linked Blogger holds a durable open
request without strictly newer prefix coverage, the recovery continuation
waits for the next committed fact of that journal stream and re-evaluates;
the open request commit/abandon or a coverage advance is the only unlock
event. Timers, deadlines, sleeps, polling and process-local flight/pending
state take no part in the wait. With no durable open producer the physical
retry proceeds at once without waiting for future material.

## [019] Only a typed provider recovery licence authorizes retry

The recovery path accepts only the `execution-failure-policy` typed
`RetryFreshAttempt` licence for `ProviderTransient | ProviderPermanent`,
bound to the exact `ProviderRunIdentity`, request kind and policy decision
identity; the ledger verifies the current attempt matches and never
recomputes budget, breaker or failure class. `LocalInvariant`,
`ProtocolRejection`, `AuthorizationDenied`, `UserCancelled`, `Superseded`,
`CapacityQueueFull`, `AcceptanceUnknown`,
`StreamInterruptedAfterFirstToken` and any `PersistenceFailure` never advance
the budget, never spend the provider failure budget, and never create a
retry attempt. Wildcard retry, reclassification by exception/terminal text,
or treating a pending acceptance as a provider failure are forbidden.

## [020] Failure budget is durable domain evidence, not resume authority

The failure budget folds only from the committed Authority Root, typed
provider failures and eligible business-main successes, and expresses only
the current logical run's consecutive failure budget and fixed participant.
It carries no callback, continuation, next action, physical request
permission or workflow entry. Replaying the same durable facts yields the
same budget view. Every retry still consumes its exact typed failure licence
and the retry policy still decides the execution content.

## [021] 首次失败保留物理目标；LWR 重试失败才驱逐 provider

一次已确认的 provider 类失败驱逐物理 provider 只有一个合法依据：失败的 attempt 本身就是已发出的 LWR 重试。

- 判定只读两条 durable 事实：该 `PhysicalUserMessageId` 已被接受为 `ProviderRetryAttempt` continuation，且失败的 `ProviderRunIdentity` 正是为该请求建立 durable `ProviderStarted` 的那个 run。一个物理消息驱动多个 provider step：重试 episode 的后续 step 失败不是 LWR 重试自身失败。连续失败计数、失败序号、错误文本与进程内状态都不能推断“已试过 LWR”。
- 失败 attempt 不是 LWR 重试（携带原上下文）时：严禁标记该 provider 失败；恢复重投绑定原物理目标——同一 session 的下一次 fresh admission 以失败 attempt 的 exact target 作为调度偏好（单次消费）——并以 LWR 替换上下文发起（context-compression-010/context-compression-011 的 `FrozenRecordPrefix` 探针）。
- 失败 attempt 是 LWR 重试时：其物理 provider 被永久 poison，后续重投由调度器轮换到其它候选目标；既不改变 participant identity，也不改变预算代数。
- 权限只属于确切的失败与确切的 attempt：provider-run witness 单次消费，因此重复通知、旧回调、取消与提交未知都不能再取得该权限；未取得 typed `RetryFreshAttempt` licence 的失败同样不结算目标。
- ordinary 恢复、sync delegate 装饰器与恢复重入共用同一规则：结算只发生在 `Retry.attempt` 授权且尚未 dispatch 的 redispatch 内，LWR 重试事实与失败目标绑定在同一处读取。


## [022] 恢复重投以宿主停止自动重试为发送前提

provider-attempt-recovery-014 的“Host has stopped automatic retry”只有一个操作定义：该确切
`ProviderRunIdentity` 的宿主终态投影（finalized errored assistant message）已被观察。
粗粒度的 `session.error`（coarse wake）只负责失败定局（provider-attempt-recovery-003 的预算推进与
host-boundary-005 的失败终结论），从不授权物理发送；`session.idle` 既会在 halt
内发生（run 尚在），也会在 run 结束时发生，两者载荷相同，不能作为该前提的证据。

- 发送前提是 process-local 的发送栅栏，精确 key = `(SessionId, ProviderRunIdentity)`：
  同 session 的其它 run、迟到的 idle、更早或更晚的尝试都不能满足它；
- 会话中止 / 替换 / 删除使该 session 上 pending 的恢复发送永久失效（安全侧失败）；
- 栅栏不写 Journal、不参与 crash recovery。重启后没有观察 → 不自动发送；悬挂态
  由 provider-attempt-recovery-023 的义务扫描定夺；
- 该前提不引入任何 timer/deadline/polling：它只等待宿主自己的终态投影事件。


## [023] 已接受但从未 ProviderStarted 的恢复重投是显式义务

一个物理恢复重投可以被宿主接受（`ChatExecution` 的 `Accepted`）而永不执行：宿主的
异步 prompt 在会话 run 仍在进行时只 join 该 run，而被 join 的 run 若以 error 结束就
不会再读这条消息。这种状态不得悬空。

- 义务的 durable 形状只有一种：该 `ChatExecutionKey` 处于 `Accepted` 且没有
  `ProviderStarted`；
- 触发只来自宿主证据：该 session 的 `session.idle` 到达时，对**该 session 中
  恰好处于上述形状**的执行做一次恢复判定，其它生命周期的执行不属于本次义务，
  不得被 idle 观察重新审判；
- 定夺必须是终局的：要么用确切的已接受material恢复执行（仅当绑定了 typed
  resume capability 时），要么把该执行结为终态并把该 turn 的失败报出——绝不静默悬挂；
- 该义务不授权发送新文本、不生成替换 `PromptClaim`：provider-attempt-recovery-003 的“一次失败最多一次
  物理发送”不被本条款放宽；
- 重启后的同一义务由 boot recovery sweep 承担（同样的窄化形状）。
