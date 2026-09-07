# provider-attempt-recovery — WHAT

## PAR-001: Failure budget belongs to a Logical Run

Provider failure accounting is lifecycle state of one Logical Run, not a
permanent Session attribute. Each Logical Run binds a fixed participant
(SelectedAgent + Role) with its Persona identity and keeps it unchanged across
all retries. A new Logical Run never inherits the previous run's consecutive
failure count.

## PAR-002: Budget counts consecutive failures only, corrupt bytes fail closed

ProviderFailureProjection tracks one number: the consecutive failure count.
Deserialization meeting illegal bytes returns a decode error and refuses the
corrupt envelope; it never throws uncaptured and never forges a decode failure
into an unknown submission.

## PAR-003: Single writer, one failure advances once, replay replays

ProviderFailureLedger is the only writer allowed to record a confirmed
provider failure or exhaustion. One confirmed failure (deduped by SessionId,
LogicalRunId, AuthorityRootUserMessageId, ProviderRun) advances the failure
count at most once; when it is still the latest failure, repeat observation
writes no fact and adds no count but replays the same admission. An older
failure returns `EpisodeSuperseded` and starts no recovery. Each retry creates
a fresh physical attempt identity (new PhysicalUserMessageId and
ProviderRunIdentity); one failure causes at most one physical send.

## PAR-004: Advance invariant and success reset

One confirmed failure adds 1 to the consecutive failure count and, while the
budget is not spent, yields an exact typed failure licence. The failed
physical provider is handled by ModelRouting; the budget never switches
roles and never stands for the physical provider pool. A main business
request success writes a success fact and resets the consecutive failure
count to zero.

The request kind of a failure recovery is read only from the durable
`ChatExecutionState.ProviderStarted.RequestKind` matching
`SessionId + PhysicalUserMessageId`; a tool continuation of the same physical
request inherits that frozen kind, while the current `ProviderRunIdentity` is
constrained separately by exact recovery authorization. Generic
parent↔child session association and the active role never imply a request
kind; only a proven `BloggerMain | BloggerSquash` may use the association to
locate the owning main session. Manager, coder and other ordinary children
keep their durable `WorkMain` even when an association exists.

## PAR-005: Bounded automatic retry budget

Automatic retry and recovery are strictly bounded (default consecutive
failure limit). When consecutive failures reach the budget the run is
exhausted and no further automatic physical request is issued; later recovery
needs a new Authority Root or an explicit user action.

## PAR-006: Fixed participant, decoupled model routing

Each logical run has a fixed participant (Role) and system prompt. No retry
changes the participant; when execution model routing changes target it is a
scheduler backend choice for the same participant, not a domain identity
change. At consecutive-failure budget the run is exhausted at once; no
over-budget request is ever issued.

## PAR-007: Fold rejection conditions

ProviderFailureProjection strictly refuses illegal budget records:
a consecutive failure count that is not the valid successor, a count beyond
the budget, and any further advance after exhaustion. Refusal is fail-closed
and stops the replay.

## PAR-008: Empty / XML-only terminal never advances

An empty terminal or an XML-only terminal means the response content is
unusable, not that the provider request failed: at most one bounded
Interaction Repair follows; advancing the failure budget on it is forbidden.

## PAR-009: Host attempt number is not the domain count

The Host transport retry sequence is Host-internal state. It is never written
into the domain consecutive failure count and never drives budget judgement
or identity derivation.

## PAR-010: Maintenance sub-request and retry dispatch

A confirmed BloggerMain failure with retry permission, when durable frames
exist and the retry policy allows maintenance, retries first with the
maintenance sub-request `BloggerSquash`; resending BloggerMain first is
forbidden. A Squash success does not clear the consecutive failure count and
the run continues to `BloggerMain`; a Squash failure records the failure and
the policy continues with the next physical retry. With no squash material
the retry sends Main directly.

## PAR-011: Material-based immutable retry policy with exact physical binding

Whether a physical retry carries a prefix probe or a maintenance request is
an immutable per-plan decision of that AttemptPlan, derived from the request
type and the persisted material state — never from transient cross-callback
state. No transient cross-callback channel may carry a recovery permission;
each retry gets an independent physical message identity and the plan freeze
admission completes before rendering.

## PAR-012: Host abort / cleanup residue never advances

Host abort cleanup marking in-flight tool calls interrupted is residue, not a
confirmed provider attempt failure: it never advances the failure budget.

## PAR-013: New provider target is a new executor, not a new identity

Failure and retry dispatch only change the next execution's physical
provider/model target. The durable logical participant run's immutable
`ParticipantIdentity` (own Role, stable Persona, provenance/version),
SessionProviderLanguage, system prompt, CanonicalRole and Authority identity
stay strictly unchanged across all retry attempts; Persona and
provenance/version inherit from that run's durable identity evidence. Each
participant binds one remote LLM target in the current execution. Only a new
run built after the exact terminal closure of the old run may take a new
identity, and machine bookkeeping never leaks into the provider horizon.

## PAR-014: Continuation only after recorded failure within budget

A continuation of the same Logical Run may be sent only after the Host has
stopped automatic retry and the budget still allows it. Sending the
continuation advances nothing and resets nothing.

## PAR-015: StrengthReplica stays out of the owner budget

A StrengthReplica attempt success or failure belongs to a speculative branch;
it never enters the owner Logical Run budget, advances nothing, and clears
nothing.

## PAR-016: RequestKind decides success accounting

Whether a successful provider attempt writes `SuccessRecorded` is decided by
the provable `ProviderRequestKind`: a valid `WorkMain | BloggerMain` success
clears the consecutive failure count; `BloggerSquash | InteractionRepair |
StrengthReplica` does not. `finish=tool-calls` is a valid provider attempt
success, not a failure; it may settle this provider recovery while the Host
turn continues with tools. Blogger RequestKind is proven first by the current
typed request / `BloggerCycleReceipt`, Continuation kind by
`AcceptedContinuationIds`; Role or terminal text alone never reclassifies a
maintenance request as a business success.

## PAR-017: Blogger retry replaces the exact physical binding

After a Blogger provider attempt failure the old `BloggerRequestMaterialized`
closes first as `BloggerRequestAbandoned`. Every automatic retry (same Main
context retry, Main→Squash, Squash→Main) re-materializes the typed context
and binds a new agent-free `PromptKey`; the old PromptKey never proves
ownership across a physical retry.

## PAR-018: Recovery continuation unlocks only on durable events

After a WorkMain failure, when the linked Blogger holds a durable open
request without strictly newer prefix coverage, the recovery continuation
waits for the next committed fact of that journal stream and re-evaluates;
the open request commit/abandon or a coverage advance is the only unlock
event. Timers, deadlines, sleeps, polling and process-local flight/pending
state take no part in the wait. With no durable open producer the physical
retry proceeds at once without waiting for future material.

## PAR-019: Only a typed provider recovery licence authorizes retry

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

## PAR-020: Failure budget is durable domain evidence, not resume authority

The failure budget folds only from the committed Authority Root, typed
provider failures and eligible business-main successes, and expresses only
the current logical run's consecutive failure budget and fixed participant.
It carries no callback, continuation, next action, physical request
permission or workflow entry. Replaying the same durable facts yields the
same budget view. Every retry still consumes its exact typed failure licence
and the retry policy still decides the execution content.
