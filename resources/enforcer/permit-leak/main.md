# permit-leak — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A semaphore, gate, lock, lease, or capacity permit can be lost on exceptions, cancellation, or early return.

## What to do

A concurrency permit can leak. Acquire it through a scoped construct that guarantees release.

## Reference

Family F, enforcement-f06, ordinal 56.
