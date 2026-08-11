# sleep-based-synchronization — Enforcer

## Definition
Sleep-based synchronization substitutes elapsed wall time for a causal fact such as readiness, completion, visibility, ownership transfer, or propagation.

## Governing Principle
Time passing does not imply the event being awaited occurred. A fixed delay merely chooses a probability that the cause will have happened by then, coupling correctness to machine load and environment speed. Causal synchronization waits on evidence produced by the event itself, so the program advances because the prerequisite became true rather than because a clock expired.

## Trigger When
Trigger when fixed sleeps/delays are used to make tests or production flows wait for another operation to become ready, complete, release, or propagate.

## Do Not Trigger When
Do not trigger for rate limiting, protocol backoff, deliberate human-facing delay, or timeout budgets that bound waiting while an actual causal signal remains the success condition.

## Distinguish From
time-dependent-test concerns tests depending on wall time broadly. timeout-inflated-to-pass lengthens a budget to hide failure. This rule specifically confuses duration with causation.

## Decision Procedure
Name the fact the code hopes will become true during the sleep. Identify an observable event/state/awaitable that proves that fact directly and wait on it instead.

## Nudge
Do not wait for time when you mean to wait for cause. Synchronize on the event or state transition that makes progress legitimate.
