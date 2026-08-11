# failure-path-untested — Enforcer

## Definition
New error handling, cancellation, rollback, retry, malformed input, or recovery behavior has no direct test.

## Trigger When
New error handling, cancellation, rollback, retry, malformed input, or recovery behavior has no direct test.

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
A newly introduced failure path is untested. Add a test that exercises the actual failure and its observable result.

## Examples
### Positive
New error handling, cancellation, rollback, retry, malformed input, or recovery behavior has no direct test.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A newly introduced failure path is untested. Add a test that exercises the actual failure and its observable result.
