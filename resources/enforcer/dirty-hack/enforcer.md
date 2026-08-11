# dirty-hack — Enforcer

## Definition
A fallback, bypass, compatibility shim, duplicated path, or special case is added to avoid repairing the underlying model or boundary.

## Trigger When
A fallback, bypass, compatibility shim, duplicated path, or special case is added to avoid repairing the underlying model or boundary.

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
A workaround is hiding the root cause. Repair the governing abstraction or invariant instead.

## Examples
### Positive
A fallback, bypass, compatibility shim, duplicated path, or special case is added to avoid repairing the underlying model or boundary.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A workaround is hiding the root cause. Repair the governing abstraction or invariant instead.
