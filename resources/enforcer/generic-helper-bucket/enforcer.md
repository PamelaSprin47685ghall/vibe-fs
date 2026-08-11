# generic-helper-bucket — Enforcer

## Definition
Files or modules named helpers, utils, common, core, service, primitives, or misc collect unrelated operations without one governing concept.

## Trigger When
Files or modules named helpers, utils, common, core, service, primitives, or misc collect unrelated operations without one governing concept.

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
A generic helper bucket is hiding missing ownership. Move each operation to the domain or boundary that owns it.

## Examples
### Positive
Files or modules named helpers, utils, common, core, service, primitives, or misc collect unrelated operations without one governing concept.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A generic helper bucket is hiding missing ownership. Move each operation to the domain or boundary that owns it.
