# commented-out-code — Enforcer

## Definition
Old implementation code is retained in comments instead of being removed.

## Trigger When
Old implementation code is retained in comments instead of being removed.

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
Commented-out code is being used as storage. Delete it and rely on version control.

## Examples
### Positive
Old implementation code is retained in comments instead of being removed.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Commented-out code is being used as storage. Delete it and rely on version control.
