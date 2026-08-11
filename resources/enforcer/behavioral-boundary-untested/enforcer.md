# behavioral-boundary-untested — Enforcer

## Definition
A public behavior is tested only through private helpers and never through the real supported entry point.

## Trigger When
A public behavior is tested only through private helpers and never through the real supported entry point.

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
The behavior is verified only below its contract boundary. Test it through the real public entry point.

## Examples
### Positive
A public behavior is tested only through private helpers and never through the real supported entry point.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
The behavior is verified only below its contract boundary. Test it through the real public entry point.
