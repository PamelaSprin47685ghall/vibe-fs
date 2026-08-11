# catch-all-swallows-future — Enforcer

## Definition
A wildcard, default branch, generic fallback, or broad catch silently absorbs future domain cases that should require explicit handling.

## Trigger When
A wildcard, default branch, generic fallback, or broad catch silently absorbs future domain cases that should require explicit handling.

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
A catch-all branch is hiding future cases. Make the match exhaustive so new states create a visible failure.

## Examples
### Positive
A wildcard, default branch, generic fallback, or broad catch silently absorbs future domain cases that should require explicit handling.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A catch-all branch is hiding future cases. Make the match exhaustive so new states create a visible failure.
