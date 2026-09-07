# provider-attempt-recovery — WHY

One confirmed provider attempt failure must retry with a bounded fresh
physical binding without reselecting Authority and without changing the
participant identity — and the automatic spend must stop on time.

**provider-attempt-recovery guarantees: after one confirmed provider attempt
failure, the system knows exactly which executor runs next, how many retries
remain, and when to stop completely.**

## Core invariants and tensions

- **Confirmed failure vs process amnesia**: an attempt failure is a
  business-layer failure proven by snapshot (fully distinct from the process
  amnesia of `crash-reconciliation`) and advances only through the single
  ProviderFailureLedger.
- **Unbounded need vs bounded automatic budget**: the need to recover is
  open-ended, but the automatic retry budget is strictly bounded (stop at
  budget, then wait for a new Authority Root or an explicit action).
- **New executor vs same identity**: failure dispatch changes only the next
  physical execution target; the durable logical participant run keeps its
  `ParticipantIdentity`, Persona, language, system prompt, CanonicalRole and
  Authority identity for the whole run.
- **Failure classification vs single recovery interpreter**: this package
  (concretely `Wanxiangshu.Participant.Provider.Attempt.Fallback.ProviderRecoveryWorkflow`
  as the sole interpreter) owns provider-started retry; `execution-failure-policy`
  solely owns failure classification and licensing, and this package only
  consumes its licence — never parses exceptions or error prose.
  Managed-chat crash or resource recovery never starts provider work and never
  publishes an empty requeue request.
- **Durable domain evidence vs resume address**: the failure budget only
  integrates committed root, failure and success facts and answers the
  current failure budget and the next physical executor; it stores no
  callback, continuation, pending action or process-local permission. Recovery
  after a crash never resumes the flow from the budget alone; it still needs
  a typed failure licence and this attempt's exact physical binding.
- **Recovery prompt identity and exact dedupe**: a provider recovery prompt
  identity carries exactly the `ProviderRecoveryDecisionId` and the source
  `ProviderRunIdentity`, with fully identical visible text. Replaying the
  same failure event re-enters the same durable claim and never sends a
  second physical request; a new failed provider run opens a new claim and
  sends a new physical request.
- **Provider health vs failure budget**: `ModelRouting` permanently poisons
  the failed physical provider; the failure budget only counts consecutive
  failures and the budget. The two are orthogonal: the budget never revives a
  failed provider, and the provider health table never rewrites the logical
  participant identity.

## What a boundary violation means

- After a provider failure the system reselects Authority, changes the
  Persona, or rewrites the system prompt.
- One failure is recorded by several observers, so the budget is overspent.
- After exhaustion the system still issues automatic physical requests.
- A duplicated physical send for one confirmed failure (same failure counted
  twice, or one licence sending twice).
- After a crash, recovery rebuilds permission from durable bytes that were
  never a licence (for example re-deriving a retry from a stored count),
  so a request with no failure advance of its own triggers history handling.
- The budget is used as a resume program counter: a continuation is rebuilt,
  the failure policy is skipped, or a physical request is resent directly
  from the stored count.

## DEPENDS ON

- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`
