# flaky-test-tolerated — Enforcer

## Definition
A nondeterministic test is accepted, quarantined indefinitely, or treated as harmless.

## Trigger When
A nondeterministic test is accepted, quarantined indefinitely, or treated as harmless.

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
A flaky test is being tolerated. Find and remove the nondeterminism before relying on the suite.

## Examples
### Positive
A nondeterministic test is accepted, quarantined indefinitely, or treated as harmless.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A flaky test is being tolerated. Find and remove the nondeterminism before relying on the suite.
