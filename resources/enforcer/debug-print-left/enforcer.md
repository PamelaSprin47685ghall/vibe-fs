# debug-print-left — Enforcer

## Definition
Temporary logging, tracing, dumps, breakpoints, or debug output remains in production paths.

## Trigger When
Temporary logging, tracing, dumps, breakpoints, or debug output remains in production paths.

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
Temporary debugging output remains. Remove it or convert it into intentional structured diagnostics.

## Examples
### Positive
Temporary logging, tracing, dumps, breakpoints, or debug output remains in production paths.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Temporary debugging output remains. Remove it or convert it into intentional structured diagnostics.
