# provider-attempt-recovery — HOW

## Architecture and core mechanism

### Retry decorator（唯一 retry 引擎）

`Fallback/Retry.fs` 的 `Retry.attempt` 是唯一的 retry 决策执行点：纯策略（`ExecutionFailurePolicy.decide`）
→ `RetryPorts.Admit`（`ProviderFailureLedger` 仍是唯一写者）→ `RetryPorts.Redispatch`（路径插件）
→ `Dispatched | Superseded | Terminal`。普通 turn、Blogger 与 dedicated SyncDelegate child 共用
同一引擎、同一预算与同一 licence；路径只提供 redispatch 策略与 terminal sink。

dedicated delegate child 的插件是 `ProviderRecoveryWorkflow.continueDelegateCallAfterConfirmedFailure`；
delegate 只消费 verdict（`Ok unit` 保持调用 pending，`Error reason` 才终结），因此单次瞬态尝试失败
不再直接失败调用方（delegation-023）。

### Failure budget algebra and single writer

- **ProviderRequestKind + ProviderFailureBudget**:
  `src/Wanxiangshu/Participant/Provider/Attempt/FailureBudget.fs` owns the
  pure consecutive-failure budget (`FailureBudget`, `Verdict =
  MayRetry | Exhausted`, default 12) and the exact attempt identity
  (`FailedProviderAttemptIdentity`, four-part dedupe key). Request kinds live
  in `RequestKind.fs`; `Context/Prefix/Candidate.fs` owns neither.
- **ProviderFailureLedger**: the single writer. It dedupes on the exact
  failed `ProviderRunIdentity` and appends `FailureRecorded`,
  `RetryExhausted` or `SuccessRecorded`.
- **Confirmed failure admission**:
  `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs` returns
  `Task<Result<FailureAdmissionOutcome,string>>`; every consumer exhausts
  `RetryAuthorized | RetryExhausted | EpisodeSuperseded | NoActiveRun`. The
  exact latest failure replays its retry/exhaustion admission; an older
  failure and a missing active run both stop.
- **Request-kind boundary**: `Fallback/Workflow.fs` reads only the durable
  `ChatExecutionState.ProviderStarted.RequestKind` matching the failed
  `SessionId + PhysicalUserMessageId`; a tool continuation of the same
  physical request inherits that frozen kind, while authorization binds the
  current `ProviderRunIdentity` separately. The active role and the session
  association never imply a request kind. `SessionAssociationProjection`
  locates the main session only after the request kind is proven
  `BloggerMain | BloggerSquash`; an ordinary fork child's parent association
  never changes its `WorkMain`.
- **Retry policy**: `src/Wanxiangshu/Participant/Provider/Attempt/RetryPolicy.fs`
  (`BloggerRetryPolicy.nextRequest`) maps the failed kind plus squash
  material presence to the next physical request kind
  (`BloggerMain→BloggerSquash` with material, `BloggerMain→BloggerMain`
  without, `BloggerSquash→BloggerMain` always); anything else is
  `NoActiveBloggerRun`. A maintenance failure and a main failure both cost
  exactly one budget unit; a maintenance success clears nothing, a main
  business success clears the count.
- **Legacy Fallback bytes**: the `Fallback` envelope family
  (`FallbackCursorAdvanced | FallbackExhausted | FallbackSucceeded`) decodes
  one way into the `ProviderFailure` family (`FailureRecorded |
  RetryExhausted | SuccessRecorded`). Stored `PreviousOffset / NextOffset /
  FinalOffset` bytes are dropped at decode time and never re-encoded; the
  writer only ever emits the `ProviderFailure` family.

### Recovery orchestration

1. **Typed recovery licence**: the Host snapshot supplies only the exact
   `ProviderRunIdentity` and terminal evidence; `execution-failure-policy`
   solely decides failure class, retry, breaker and budget consequence. The
   recovery path consumes only a provider recovery licence bound to the
   current run, request kind and decision identity — never terminal text —
   and builds no recovery for non-provider classes.
2. **Admission**: the `RetryFreshAttempt` licence is recorded once for that
   exact failed ProviderRun; only `RetryAuthorized` continues,
   `RetryExhausted` terminalizes, `EpisodeSuperseded | NoActiveRun` stop
   idempotently. WorkMain with permission gets one fresh physical retry;
   BloggerMain with frames sends BloggerSquash first.
3. **WorkMain retry physical ownership**: the recovery continuation passes
   normal Prompt admission first; only after `PromptIngress` persists that
   `ProviderRetryAttempt` as `PhysicalAccepted` does the exact
   `PhysicalUserMessageId` anchor a one-shot recovery permit.
   `messages.transform` consumes only a permit with a fully equal physical
   id; a tool continuation, an old retry or an ordinary user material of the
   same session never claims it.
4. **ProviderRun late binding**: `messages.transform` runs before provider
   inference and freezes only the pending attempt plan (authority, physical
   id, request kind, prefix choice) — never a future assistant run. A later
   tool-continuation visibility or reconciled turn supplies
   `PhysicalUserMessageId + ProviderRunIdentity` and binds the pending plan
   once into the `AttemptExecutionProfile`, then prefix promotion and
   success accounting run.
5. **Blogger retry ownership**: the failed open request is abandoned first;
   the next typed request materializes before the physical send and binds
   that send's agent-free PromptKey. Main→Main, Main→Squash and Squash→Main
   share the one rule. `Fallback/Workflow.fs` resolves the main session only
   after the exact durable request kind is proven Blogger, and appends the
   failure record under that main session; `Interaction/Repair/InteractionRepair.fs`
   likewise never records the Blogger session as its own main session.
6. **Event unlock**: WorkMain recovery with a durable open producer in the
   linked Blogger subscribes to committed journal change via
   `AgentJournal.awaitChangeFromOrCancel`; `BlogObservationCommitted`,
   `BlogObservationsSquashed`, `BloggerRequestAbandoned` and sibling facts
   re-evaluate the condition, and plugin shutdown unsubscribes explicitly.
   With no open producer the retry proceeds at once — no flight/pending
   reads, no timeout/polling.
7. **Success accounting**: RequestKind is proven by typed request, durable
   receipt or accepted continuation evidence; failure recovery trusts only
   the exact physical ChatExecution durable ProviderStarted evidence.
   Squash/repair success writes no SuccessRecorded; only WorkMain/BloggerMain
   success clears the failure count.
8. **Identity isolation and target settlement**: budget advance spends budget
   and settles the failed physical target under provider-attempt-recovery-021 — the first failure
   keeps the provider and binds the session's next fresh admission to the
   failed target for the LWR-replaced retry; only a failed LWR retry poisons
   the provider and the following dispatch rotates. It never changes identity.
   No execution-selected agent field exists; provider rotation happens only in
   the model layer. Every attempt reuses the same durable logical participant
   run identity, Persona, language, CanonicalRole, Authority identity and
   system prompt bytes; the controller cannot issue a new identity.
9. **Single budget projection**: `LogicalRunId` is the stable logical
   operation identity; the Host-created fresh `ProviderRunIdentity` is the
   physical attempt identity. `ProviderFailureProjection.mayRetry` against
   `ProviderFailureBudget.DefaultBudget (12)` is the only automatic retry
   budget projection; the initial request counts into the failure sequence,
   the 12th failure writes exhaustion only, and no 13th physical request is
   sent. One logical operation therefore satisfies `physicalAttempts ≤ 12`.
   A duplicate ProviderRun observation writes no second record and re-enters
   the same durable recovery claim.
10. **Single licence**: `ExecutionFailurePolicy.decide` seals one
    `ProviderRecoveryAuthorization` with a stable `ProviderRecoveryDecisionId`
    from the exact `LogicalRunId + ProviderRunIdentity + ProviderRequestKind`.
    `ProviderFailureLedger.recordAuthorizedFailure` accepts only that licence
    and rechecks the logical run; Host retry signals, dispatchers, repair and
    session recovery cannot issue licences.
11. **Provider recovery prompt identity and single interpreter**:
    `Wanxiangshu.Participant.Provider.Attempt.Fallback.ProviderRecoveryWorkflow`
    solely interprets provider-started retry. A recovery prompt identity binds
    the exact `ProviderRecoveryDecisionId` and source `ProviderRunIdentity`
    with identical physical send text; the same failure replay re-enters the
    existing durable claim and sends no second physical request, while a new
    failed provider run opens a new claim and sends. Managed-chat crash or
    resource recovery never starts provider recovery and never publishes a
    lazy requeue.

12. **provider-attempt-recovery-008 routing — unusable content is repair, not a provider failure**:
    an attempt that reports an error while its formal visible text is unusable
    (empty / XML-only, the shared `TerminalValidity` gate) is content damage.
    `Interaction/Repair/CompletedTurn.fs` (`classifyErroredContent`) keeps it on
    `TurnNeedsContinuation`, and
    `Composition/Turn/ReconcilePass.fs` (`materializeFailureWitness` +
    `ReconcileProgram.failureWitnessMintsTerminal`) refuses to mint a
    provider-failure terminal unless the decoded class is a confirmed provider
    class. The turn therefore reaches
    `OrdinaryTurnWorkflow → InteractionRepairWorkflow.repairMissingFinalReport`
    — the idle-gated repair nudge that replaces the dead-ended context before
    the next physical attempt — and never advances the failure budget or reports
    a terminal to the caller. A confirmed provider class (transient/permanent)
    still terminalizes with the policy owning retry vs terminal, so ordinary
    provider fallback is unchanged.

13. **provider-attempt-recovery-021 target settlement — one rule for every recovery entry**: the
    settlement runs inside `Retry.attempt`'s licensed redispatch (the single
    engine shared by the ordinary turn path, Blogger recovery and dedicated
    SyncDelegate children), after the durable prompt claim proved this
    dispatch is the first one for that failure. `Fallback/Workflow.fs` reads
    two durable facts — whether the failed attempt's exact physical request was
    accepted as a `ProviderRetryAttempt` continuation, and whether the failed
    provider run is the exact run that established that request's durable
    `ProviderStarted` (one physical message drives several provider steps, so
    a later step of a successful retry episode is not the LWR retry itself) —
    and settles the exact failed witness (execution-model-routing-006/execution-model-routing-017) once: an LWR
    retry's own failure condemns
    its provider (`ModelRouting.CondemnFailedTarget`), every other failure
    keeps the provider and binds the session's next fresh admission to the
    failed target (`ModelRouting.RetainFailedTargetForRetry`). Duplicate
    notifications, stale callbacks, cancellations, unknown submissions and
    unlicensed failures stop before the settlement; the witness and the
    retry binding are each consumed exactly once.

## Final production path

- `src/Wanxiangshu/Participant/Provider/Attempt/FailureBudget.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Planner.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/RetryPolicy.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Evidence.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Fact.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Facts.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ProviderFailureFactFold.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Projection.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs`

`Fallback/ProviderFailureSurface.fs` is the single recovery JS proof
surface. `FailureBudget.fs` keeps only the `ConsecutiveFailureCount` domain
value; `Fallback/ProviderFailureSurface.fs` folds real durable facts behind
an opaque projection handle and projects only the logical run, authority
root, failure count, dedupe cardinality and exhaustion. The surface exposes
no continuation, next action, or physical send power.
provider-attempt-recovery-020's exact production-bound proof replays the same facts to the same
restricted view; flow permission stays with `RetryPolicy` and the typed
failure policy.

## Dependencies

DEPENDS ON:
- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`

