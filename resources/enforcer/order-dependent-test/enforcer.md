# order-dependent-test — Enforcer

## Definition
A test passes only after another test, depends on global residue, or changes behavior with suite ordering.

## Trigger When
A test passes only after another test, depends on global residue, or changes behavior with suite ordering.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
mock-hidden-state, missing-regression-test, ignored-tdd

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A test depends on execution order. Give every test isolated, explicit setup and cleanup.

## Examples
### Positive
A test passes only after another test, depends on global residue, or changes behavior with suite ordering.

### Near miss
Looks related to mock-hidden-state but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
