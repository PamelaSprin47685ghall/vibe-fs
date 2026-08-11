# ignored-tdd — Enforcer

## Definition
Production behavior is implemented or changed before a failing behavioral test demonstrates the required outcome.

## Trigger When
Production behavior is implemented or changed before a failing behavioral test demonstrates the required outcome.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
missing-regression-test, guess-based-fix, order-dependent-test

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
TDD order was skipped. Add a failing behavioral test before changing the implementation.

## Examples
### Positive
Production behavior is implemented or changed before a failing behavioral test demonstrates the required outcome.

### Near miss
Looks related to missing-regression-test but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
