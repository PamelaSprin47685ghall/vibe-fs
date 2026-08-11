# sleep-based-synchronization — Enforcer

## Definition
Sleep-based synchronization substitutes elapsed wall time for a causal fact such as readiness, completion, visibility, ownership transfer, or propagation. The root-cause is that duration is treated as evidence that a cause occurred, so correctness is a probability coupled to machine load rather than a wait on the event itself.

## Governing Principle
Time passing does not imply the event being awaited occurred. A fixed delay merely chooses a probability that the cause will have happened by then. Causal synchronization waits on evidence produced by the event itself, so the program advances because the prerequisite became true rather than because a clock expired.

## Trigger When
Trigger when fixed sleeps or delays are used to make tests or production flows wait for another operation to become ready, complete, release, or propagate.

## Do Not Trigger When
- The delay is rate limiting, protocol backoff, or a deliberate human-facing pause.
- A timeout budget only bounds waiting while an actual causal signal remains the success condition.
- `sleep` is used in a demo or script with no correctness claim.
- The wait is on an awaitable or condition, and any remaining timeout is failure policy only.

## Distinguish From
`time-dependent-test` concerns tests depending on wall time broadly. `timeout-inflated-to-pass` lengthens a budget to hide failure. `blocking-event-loop` monopolizes the shared executor while waiting. This rule specifically confuses duration with causation. Tie-break: if sleep is the success signal, this rule owns the case.

## Decision Procedure
1. Name the fact the code hopes will become true during the sleep.
2. Identify an observable event, state, or awaitable that proves that fact directly.
3. Wait on that evidence instead.
4. Keep any timeout only as failure policy, never as success evidence.

## Examples
- positive: a test `sleep(500)` then asserts a file exists, treating elapsed time as “the writer finished.”
- near-miss: a retry backoff sleeps between attempts but success is still an HTTP 200, not the sleep itself.
- counterexample: the test awaits a completion callback or polls a documented ready state under a failure timeout.

## Nudge
Do not wait for time when you mean to wait for cause. Synchronize on the event or state transition that makes progress legitimate.
