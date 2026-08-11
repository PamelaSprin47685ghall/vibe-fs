# missing-regression-test — Enforcer

## Definition
A defect is fixed without a test that fails on the old behavior and passes on the corrected behavior.

## Trigger When
A defect is fixed without a test that fails on the old behavior and passes on the corrected behavior.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
ignored-tdd, guess-based-fix, order-dependent-test

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A bug fix lacks a regression test. Capture the failure before considering the fix complete.

## Examples
### Positive
A defect is fixed without a test that fails on the old behavior and passes on the corrected behavior.

### Near miss
Looks related to ignored-tdd but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
