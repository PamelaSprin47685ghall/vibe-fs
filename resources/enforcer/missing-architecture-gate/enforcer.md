# missing-architecture-gate — Enforcer

## Definition
A critical boundary or forbidden dependency relies only on team discipline even though a static architecture gate could enforce it.

## Trigger When
A critical boundary or forbidden dependency relies only on team discipline even though a static architecture gate could enforce it.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
missing-invariant-documentation, implicit-convention-magic, missing-rule-combinator

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
An architecture boundary relies on memory alone. Add a static gate that fails when the boundary is crossed.

## Examples
### Positive
A critical boundary or forbidden dependency relies only on team discipline even though a static architecture gate could enforce it.

### Near miss
Looks related to missing-invariant-documentation but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
