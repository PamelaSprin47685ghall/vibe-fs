# provider-attempt-recovery — HOW

## Architecture and core mechanism

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
8. **Identity isolation**: budget advance spends budget and rotates the
   provider target; it never changes identity. No execution-selected agent
   field exists; provider rotation happens only in the model layer. Every
   attempt reuses the same durable logical participant run identity, Persona,
   language, CanonicalRole, Authority identity and system prompt bytes; the
   controller cannot issue a new identity.
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
PAR-020's exact production-bound proof replays the same facts to the same
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

## Verification and test landing

| Proposition | Landing test |
|---|---|
| PAR-001 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-001] an_accepted_authority_root_creates_the_budget` |
| PAR-002 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-002] a_fresh_budget_starts_at_zero_with_no_budget_spent` |
| PAR-003 | `requirements/provider-attempt-recovery/tests/provider-failure-ledger.test.mjs::WHAT[PAR-003] same_failure_observed_twice_advances_once` |
| PAR-004 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-004] failure_adds_one_to_the_consecutive_count` |
| PAR-005 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-005] the_default_automatic_retry_budget_is_twelve`; `requirements/provider-attempt-recovery/tests/provider-failure-ledger.test.mjs::WHAT[PAR-005] twelfth_failure_admission_is_retry_exhausted` |
| PAR-006 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-006] retry_keeps_fixed_participant_with_decoupled_model_routing` |
| PAR-007 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-007] each_rejection_names_a_different_cause` |
| PAR-008 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-008] an_invalid_terminal_earns_at_most_one_repair_and_never_advances` |
| PAR-009 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-009] the_domain_count_is_reachable_only_through_a_confirmed_failure` |
| PAR-010 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-010] blogger_retry_dispatch_selects_squash_when_material_exists_and_main_otherwise` |
| PAR-011 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-011] retry_decision_is_material_based_and_physically_bound` |
| PAR-012 | `requirements/provider-attempt-recovery/tests/abort-residue.test.mjs::WHAT[PAR-012] PAR_012_an_interrupted_tool_call_is_not_a_confirmed_failure` |
| PAR-013 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-013] participant_identity_role_and_persona_remain_immutable_across_retries` |
| PAR-014 | `requirements/provider-attempt-recovery/tests/provider-failure-ledger.test.mjs::WHAT[PAR-014] a_continuation_has_a_unique_accounted_and_budgeted_occasion` |
| PAR-015 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-015] independent_sessions_keep_independent_budgets` |
| PAR-016 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-016] success_accounting_requires_proven_request_kind` |
| PAR-017 | `requirements/context-compression/tests/blogger-runtime.test.mjs::WHAT[PAR-017] Blogger retry replaces exact physical ownership before the next binding` |
| PAR-018 | `requirements/provider-attempt-recovery/tests/retry-policy.test.mjs::WHAT[PAR-018] recovery_retry_unlocks_only_on_durable_material_without_waiters` |

P0 recovery re-entry proof: `requirements/structured-workflow/tests/recovery-reentry.test.mjs`; hard gate: `scripts/checks/p0-recovery-join.mjs`. The gate also constrains Blogger failures to resolve the exact main session before ledger append, and `NoActiveRun` to stop recovery.
| PAR-019 | `requirements/provider-attempt-recovery/tests/retry-owner.test.mjs::WHAT[PAR-019] one policy owner licenses every provider recovery attempt`; `requirements/verification-system/tests/retry-owner.test.mjs::WHAT[PAR-019] rejects nested physical retry owner`; `requirements/verification-system/tests/retry-owner.test.mjs::WHAT[PAR-019] rejects retry classification from diagnostic text` |
| PAR-020 | `requirements/provider-attempt-recovery/tests/failure-budget.test.mjs::WHAT[PAR-020] budget_replay_exposes_domain_evidence_without_resume_authority` |
| PAR-002 legacy bytes | `requirements/provider-attempt-recovery/tests/failure-budget-decoder.test.mjs::WHAT[PAR-002] legacy_fallback_bytes_decode_one_way_and_never_re_encode_offsets` |
| PAR-011 plan freeze | `requirements/provider-attempt-recovery/tests/freeze-admission.test.mjs::WHAT[PAR-011] same-key same-plan replays the admitted plan` |
