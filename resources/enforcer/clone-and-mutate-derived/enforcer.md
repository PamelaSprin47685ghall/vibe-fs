# clone-and-mutate-derived — Enforcer

## Definition
A new domain value is created by cloning a mutable prototype and patching selected fields.

## Trigger When
A new domain value is created by cloning a mutable prototype and patching selected fields.

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
A derived value is being made through clone-and-mutate. Construct the intended immutable value directly.

## Examples
### Positive
A new domain value is created by cloning a mutable prototype and patching selected fields.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A derived value is being made through clone-and-mutate. Construct the intended immutable value directly.
