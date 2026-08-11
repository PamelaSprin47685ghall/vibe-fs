# permit-leak — Enforcer

Id: enforcement-f06 / Family: F / Ordinal: 56

## ScoreWhen

A semaphore, gate, lock, lease, or capacity permit can be lost on exceptions, cancellation, or early return.

## Nudge

A concurrency permit can leak. Acquire it through a scoped construct that guarantees release.
