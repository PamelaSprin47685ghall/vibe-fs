# phase-flag-accumulation — Enforcer

## Definition
New flags are repeatedly added to patch interactions between lifecycle phases, producing combinatorial behavior.

## Trigger When
New flags are repeatedly added to patch interactions between lifecycle phases, producing combinatorial behavior.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
program-counter-state, non-exhaustive-transition, illegal-state-representable

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Lifecycle flags are accumulating into an implicit state machine. Replace the flag product with a smaller explicit model or structured flow.

## Examples
### Positive
New flags are repeatedly added to patch interactions between lifecycle phases, producing combinatorial behavior.

### Near miss
Looks related to program-counter-state but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
