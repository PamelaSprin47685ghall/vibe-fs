# premature-optimization — Enforcer

## Definition
Complexity is introduced for performance before a measured bottleneck or explicit resource constraint exists.

## Trigger When
Complexity is introduced for performance before a measured bottleneck or explicit resource constraint exists.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
incidental-complexity-dominates, guess-based-fix, premature-unification

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Optimization was introduced without evidence of a bottleneck. Keep the simple design until measurement justifies complexity.

## Examples
### Positive
Complexity is introduced for performance before a measured bottleneck or explicit resource constraint exists.

### Near miss
Looks related to incidental-complexity-dominates but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
