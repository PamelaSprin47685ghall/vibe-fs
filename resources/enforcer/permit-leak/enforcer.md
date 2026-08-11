# permit-leak — Enforcer

## Definition
A semaphore, gate, lock, lease, or capacity permit can be lost on exceptions, cancellation, or early return.

## Trigger When
A semaphore, gate, lock, lease, or capacity permit can be lost on exceptions, cancellation, or early return.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
lost-update, optimistic-retry-assumption, impure-core

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A concurrency permit can leak. Acquire it through a scoped construct that guarantees release.

## Examples
### Positive
A semaphore, gate, lock, lease, or capacity permit can be lost on exceptions, cancellation, or early return.

### Near miss
Looks related to lost-update but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
