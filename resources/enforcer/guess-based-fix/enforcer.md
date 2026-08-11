# guess-based-fix — Enforcer

## Definition
Changes are tried speculatively until symptoms disappear without a causal explanation or regression test.

## Trigger When
Changes are tried speculatively until symptoms disappear without a causal explanation or regression test.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
guessed-not-verified, missing-regression-test, ignored-tdd

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The fix is based on trial and error. Establish the cause, then encode it in a regression test.

## Examples
### Positive
Changes are tried speculatively until symptoms disappear without a causal explanation or regression test.

### Near miss
Looks related to guessed-not-verified but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
