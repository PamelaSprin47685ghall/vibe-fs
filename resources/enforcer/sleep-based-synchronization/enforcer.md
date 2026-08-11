# sleep-based-synchronization — Enforcer

## Definition
Sleep-based synchronization substitutes elapsed wall time for a causal fact such as readiness, completion, visibility, ownership transfer, or propagation.

## Governing Principle
Time passing does not imply the event being awaited occurred. A fixed delay merely chooses a probability that the cause will have happened by then, coupling correctness to machine load and environment speed. Causal synchronization waits on evidence produced by the event itself, so the program advances because the prerequisite became true rather than because a clock expired.

## Trigger When
Trigger when fixed sleeps/delays are used to make tests or production flows wait for another operation to become ready, complete, release, or propagate.

## Do Not Trigger When
- The delay is rate limiting, protocol backoff, or a deliberate human-facing pause.
- A timeout budget only bounds waiting while an actual causal signal remains the success condition.
- `sleep` is used in a demo/script with no correctness claim.
- The wait is on an awaitable/condition, and any remaining timeout is failure policy only.

## Distinguish From
time-dependent-test concerns tests depending on wall time broadly. timeout-inflated-to-pass lengthens a budget to hide failure. This rule specifically confuses duration with causation. Tie-break: fire here when sleep is the success signal; fire timeout-inflated-to-pass when an existing wait’s budget is grown to hide flakes; fire time-dependent-test when assertions depend on the clock rather than a sleep used as a join.

## Decision Procedure
Name the fact the code hopes will become true during the sleep. Identify an observable event/state/awaitable that proves that fact directly and wait on it instead.

## Examples
- positive: a test `sleep(500)` then asserts a file exists, treating elapsed time as “the writer finished.”
- near-miss: a retry backoff sleeps between attempts but success is still an HTTP 200, not the sleep itself.
- counterexample: the test awaits a completion callback or polls a documented ready state under a failure timeout.

## Nudge
Do not wait for time when you mean to wait for cause. Synchronize on the event or state transition that makes progress legitimate.
