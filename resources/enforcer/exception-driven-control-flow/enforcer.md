# exception-driven-control-flow — Enforcer

## Definition
Exceptions are intentionally thrown and caught to express ordinary branching, iteration, absence, or expected retries.

## Trigger When
Exceptions are intentionally thrown and caught to express ordinary branching, iteration, absence, or expected retries.

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
Exceptions are being used as ordinary control flow. Replace them with explicit branches or typed results.

## Examples
### Positive
Exceptions are intentionally thrown and caught to express ordinary branching, iteration, absence, or expected retries.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Exceptions are being used as ordinary control flow. Replace them with explicit branches or typed results.
