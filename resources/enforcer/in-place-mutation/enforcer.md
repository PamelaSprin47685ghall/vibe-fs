# in-place-mutation — Enforcer

## Definition
Shared or externally visible state is overwritten in place, destroying the explicit transition from the previous value to the next value.

## Trigger When
Shared or externally visible state is overwritten in place, destroying the explicit transition from the previous value to the next value.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
mutable-public-state, overwrite-history, lost-update

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Shared state is being mutated in place. Compute a new value or record an explicit transition instead.

## Examples
### Positive
Shared or externally visible state is overwritten in place, destroying the explicit transition from the previous value to the next value.

### Near miss
Looks related to mutable-public-state but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
