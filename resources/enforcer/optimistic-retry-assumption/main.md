# optimistic-retry-assumption — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

An external effect is retried because its result is unknown, without an idempotency identity or at-most-once recovery contract.

## What to do

An unknown external effect is being retried optimistically. Establish idempotency or an explicit at-most-once protocol.

## Reference

Family E, enforcement-e09, ordinal 49.
