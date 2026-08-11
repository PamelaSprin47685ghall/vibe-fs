# boolean-blindness — Enforcer

## Definition
Multiple booleans encode independent meanings, modes, permissions, or lifecycle states and allow ambiguous call sites or invalid combinations.

## Trigger When
Multiple booleans encode independent meanings, modes, permissions, or lifecycle states and allow ambiguous call sites or invalid combinations.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when the observed pattern is intentional, documented, and verified at the owning contract.

## Distinguish From
Related tips that share vocabulary but different boundary.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Boolean flags are hiding distinct domain meanings. Replace them with named cases or explicit types.

## Examples
### Positive
Multiple booleans encode independent meanings, modes, permissions, or lifecycle states and allow ambiguous call sites or invalid combinations.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Boolean flags are hiding distinct domain meanings. Replace them with named cases or explicit types.
