# timeout-inflated-to-pass — Enforcer

## Definition
A timeout is inflated to pass when a larger waiting budget is used to convert a failure or hang into apparent success without explaining why the operation requires the additional time.

## Governing Principle
A timeout is a policy about how long uncertainty may persist; it is not a mechanism that causes progress. Raising it can hide missing signals, deadlocks, resource leaks, pathological scheduling, or unbounded work while changing none of those causes. A justified timeout follows from measured service behavior and an SLO; an unjustified one merely moves the point at which evidence becomes inconvenient.

## Trigger When
Trigger when a failing test or operation is made green primarily by increasing timeout/retry duration without root-cause analysis or measured capacity evidence.

## Do Not Trigger When
- Measurement shows the old budget contradicts a defined latency/SLO envelope and the underlying operation remains causally healthy.
- An explicit SLO change, with evidence, raises the policy for a still-progressing operation.
- A separate retry budget is justified by idempotent remote tail latency, not to hide a hang or missing signal.
- Timeouts used as intentional failure injection in negative tests.

## Distinguish From
`sleep-based-synchronization` substitutes delay for a readiness signal. `repeat-until-pass` selects a lucky run. Tie-break: if the wait is used as a causal substitute for readiness, use `sleep-based-synchronization`; if the failure threshold is raised to conceal the same unresolved wait, use this rule.

## Decision Procedure
Determine what event should complete the operation and why it did not occur within the old budget. Fix causality first; adjust the timeout only from measured legitimate latency afterward.

## Examples
- positive: a flaky integration is greened by raising the timeout from 2s to 30s with no causal diagnosis.
- near-miss: p99 measurement shows legitimate remote latency of 1.8s against a 500ms budget, and the SLO is updated with evidence.
- counterexample: inserting `sleep(5)` so a later assertion usually sees a ready state is `sleep-based-synchronization`.

## Nudge
A larger clock does not repair a missing cause. Explain the delay, fix the stalled mechanism, then set timeout from evidence rather than from the value that happens to make tests green.
