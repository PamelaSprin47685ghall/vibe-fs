# optimistic-retry-assumption — Enforcer

## Definition
An external effect is retried because its result is unknown, without an idempotency identity or at-most-once recovery contract.

## Trigger When
An external effect is retried because its result is unknown, without an idempotency identity or at-most-once recovery contract.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
lost-update, partial-write-assumption, permit-leak

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
An unknown external effect is being retried optimistically. Establish idempotency or an explicit at-most-once protocol.

## Examples
### Positive
An external effect is retried because its result is unknown, without an idempotency identity or at-most-once recovery contract.

### Near miss
Looks related to lost-update but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
