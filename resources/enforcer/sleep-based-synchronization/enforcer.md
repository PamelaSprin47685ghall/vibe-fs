# sleep-based-synchronization — Enforcer

## Definition
Fixed sleeps or delays stand in for readiness, completion, ordering, or propagation causality.

## Trigger When
Fixed sleeps or delays are used to wait for readiness, completion, ordering, or propagation.

## Do Not Trigger When
Do not fire for intentional rate limits, backoff ceilings paired with real signals, or human-facing animation delays unrelated to correctness.

## Distinguish From
time-dependent-test is tests coupled to wall clocks; timeout-inflated-to-pass grows budgets to hide hangs; this tip is sleep as synchronization.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A fixed sleep is standing in for causality. Wait for an explicit signal or observable state transition.

## Examples
### Positive
Fixed sleeps or delays are used to wait for readiness, completion, ordering, or propagation.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire for intentional rate limits, backoff ceilings paired with real signals, or human-facing animation delays unrelated to correctness.
