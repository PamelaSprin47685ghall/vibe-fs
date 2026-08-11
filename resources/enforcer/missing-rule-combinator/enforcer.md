# missing-rule-combinator — Enforcer

## Definition
Three or more rules with the same input/output shape are manually chained instead of composed through a reusable validation or policy combinator.

## Trigger When
Three or more rules with the same input/output shape are manually chained instead of composed through a reusable validation or policy combinator.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
pattern-sprawl, premature-unification, phase-flag-accumulation

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Repeated rule composition is being written by hand. Introduce a small combinator that exposes the shared rule algebra.

## Examples
### Positive
Three or more rules with the same input/output shape are manually chained instead of composed through a reusable validation or policy combinator.

### Near miss
Looks related to pattern-sprawl but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
