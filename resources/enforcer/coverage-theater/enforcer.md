# coverage-theater — Enforcer

## Definition
A test or metric increases coverage but does not assert meaningful behavior, identities, values, or failure outcomes.

## Trigger When
A test or metric increases coverage but does not assert meaningful behavior, identities, values, or failure outcomes.

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
Coverage is being mistaken for verification. Add assertions that would fail under a realistic defect.

## Examples
### Positive
A test or metric increases coverage but does not assert meaningful behavior, identities, values, or failure outcomes.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Coverage is being mistaken for verification. Add assertions that would fail under a realistic defect.
