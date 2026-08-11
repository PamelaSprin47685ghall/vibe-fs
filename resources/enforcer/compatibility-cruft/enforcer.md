# compatibility-cruft — Enforcer

## Definition
Compatibility layers, aliases, duplicate formats, or dual paths are added without a real external compatibility requirement.

## Trigger When
Compatibility layers, aliases, duplicate formats, or dual paths are added without a real external compatibility requirement.

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
Compatibility machinery lacks a justified external contract. Remove the duplicate path and keep one canonical interface.

## Examples
### Positive
Compatibility layers, aliases, duplicate formats, or dual paths are added without a real external compatibility requirement.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Compatibility machinery lacks a justified external contract. Remove the duplicate path and keep one canonical interface.
