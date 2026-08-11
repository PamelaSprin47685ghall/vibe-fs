# mutable-public-state — Enforcer

## Definition
Callers can directly modify fields that should be protected by invariants or domain behavior.

## Trigger When
Callers can directly modify fields that should be protected by invariants or domain behavior.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
in-place-mutation, illegal-state-representable, null-ambiguity

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Public mutable fields bypass the object’s rules. Encapsulate the state and expose invariant-preserving operations.

## Examples
### Positive
Callers can directly modify fields that should be protected by invariants or domain behavior.

### Near miss
Looks related to in-place-mutation but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
