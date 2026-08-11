# weakened-test-to-pass — Enforcer

## Definition
Assertions, cases, fixtures, or expected outcomes are removed or weakened primarily to make a failing test pass.

## Trigger When
Assertions, cases, fixtures, or expected outcomes are removed or weakened primarily to make a failing test pass.

## Do Not Trigger When
Do not fire when tests are correctly updated because the specified contract intentionally changed and old expectations are obsolete.

## Distinguish From
test-implementation-coupled asserts internals; coverage-theater pads metrics; this tip dilutes expectations to hide defects.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The test was weakened instead of fixing the defect. Restore the behavioral expectation and repair the implementation.

## Examples
### Positive
Assertions, cases, fixtures, or expected outcomes are removed or weakened primarily to make a failing test pass.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when tests are correctly updated because the specified contract intentionally changed and old expectations are obsolete.
