# lost-update — Enforcer

## Definition
Concurrent writers perform read-modify-write without version checking, compare-and-swap, serialization, or another conflict protocol.

## Trigger When
Concurrent writers perform read-modify-write without version checking, compare-and-swap, serialization, or another conflict protocol.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
in-place-mutation, optimistic-retry-assumption, permit-leak

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Concurrent updates can overwrite each other. Add a versioned compare-and-swap or a single writer.

## Examples
### Positive
Concurrent writers perform read-modify-write without version checking, compare-and-swap, serialization, or another conflict protocol.

### Near miss
Looks related to in-place-mutation but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
