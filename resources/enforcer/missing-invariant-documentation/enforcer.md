# missing-invariant-documentation — Enforcer

## Definition
A non-obvious invariant is essential to correctness but exists only in implementation details or tribal knowledge.

## Trigger When
A non-obvious invariant is essential to correctness but exists only in implementation details or tribal knowledge.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
missing-architecture-gate, missing-regression-test, guessed-not-verified

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A critical invariant is undocumented. State it at the owning contract and add a mechanical guard where possible.

## Examples
### Positive
A non-obvious invariant is essential to correctness but exists only in implementation details or tribal knowledge.

### Near miss
Looks related to missing-architecture-gate but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
