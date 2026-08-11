# optimistic-retry-assumption — Enforcer

Id: enforcement-e09 / Family: E / Ordinal: 49

## ScoreWhen

An external effect is retried because its result is unknown, without an idempotency identity or at-most-once recovery contract.

## Nudge

An unknown external effect is being retried optimistically. Establish idempotency or an explicit at-most-once protocol.
