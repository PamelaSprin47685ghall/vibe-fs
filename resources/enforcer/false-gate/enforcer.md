# false-gate — Enforcer

## Definition
A gate can remain green because it scans the wrong path, matches nothing, ignores failures, or checks a condition that cannot fail.

## Trigger When
A gate can remain green because it scans the wrong path, matches nothing, ignores failures, or checks a condition that cannot fail.

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
A quality gate can pass without checking the intended property. Add a self-test that proves the gate turns red.

## Examples
### Positive
A gate can remain green because it scans the wrong path, matches nothing, ignores failures, or checks a condition that cannot fail.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A quality gate can pass without checking the intended property. Add a self-test that proves the gate turns red.
