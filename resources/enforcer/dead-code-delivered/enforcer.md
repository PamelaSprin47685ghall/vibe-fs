# dead-code-delivered — Enforcer

## Definition
Unreachable, unused, superseded, or unreferenced production code is left behind.

## Trigger When
Unreachable, unused, superseded, or unreferenced production code is left behind.

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
Dead production code remains. Delete it and let version control preserve history.

## Examples
### Positive
Unreachable, unused, superseded, or unreferenced production code is left behind.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Dead production code remains. Delete it and let version control preserve history.
