# pattern-sprawl — Enforcer

## Definition
Class hierarchies, factories, visitors, strategies, or interfaces simulate behavior that sealed data, pattern matching, and first-class functions could express directly.

## Trigger When
Class hierarchies, factories, visitors, strategies, or interfaces simulate behavior that sealed data, pattern matching, and first-class functions could express directly.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
premature-unification, missing-rule-combinator, incidental-complexity-dominates

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Design-pattern scaffolding is obscuring a simpler algebraic model. Prefer closed data and direct composition.

## Examples
### Positive
Class hierarchies, factories, visitors, strategies, or interfaces simulate behavior that sealed data, pattern matching, and first-class functions could express directly.

### Near miss
Looks related to premature-unification but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
