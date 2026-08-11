# sleep-based-synchronization — Main

## What To Do Now
Replace sleep with an await on a condition, event, readiness probe, or state transition. Bound the wait and fail closed on timeout.

## Repair Strategy
Name the causal event being approximated. Subscribe or poll that condition with a deadline. Delete fixed delays from tests and production coordination.

## Decision Branches
If only a coarse health endpoint exists, poll it with backoff and a cap—still condition-based, not a single blind sleep. If the system cannot signal, add the signal.

## Wrong Fixes
sleep(2) in tests "to let CI catch up". Production Thread.Sleep to order microservices. Increasing sleep until flakes drop.

## Verification
Under slow and fast conditions, waits complete on the signal; removing the signal fails deterministically without relying on duration luck.

## Done When
Synchronization is signal-based with bounded waits; fixed sleeps are not load-bearing for correctness.

## Scope and Authority
Coordination and tests. Not product UX timers that are the feature.
