# illegal-state-representable — Enforcer

## Definition
Nullable fields and flags allow combinations that cannot exist in the real domain.

## Trigger When
Nullable fields and flags allow combinations that cannot exist in the real domain.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
null-ambiguity, primitive-obsession, non-exhaustive-transition

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Illegal domain states are representable. Encode each valid state explicitly and attach only the data meaningful in that state.

## Examples
### Positive
Nullable fields and flags allow combinations that cannot exist in the real domain.

### Near miss
Looks related to null-ambiguity but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
