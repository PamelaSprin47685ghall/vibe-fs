# premature-unification — Enforcer

## Definition
Similar-looking code or concepts with different lifecycles, invariants, or reasons to change are unified before they represent the same knowledge.

## Trigger When
Similar-looking code or concepts with different lifecycles, invariants, or reasons to change are unified before they represent the same knowledge.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
pattern-sprawl, god-module, half-finished-refactor

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Similarity was mistaken for shared knowledge. Separate concepts that change for different reasons.

## Examples
### Positive
Similar-looking code or concepts with different lifecycles, invariants, or reasons to change are unified before they represent the same knowledge.

### Near miss
Looks related to pattern-sprawl but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
