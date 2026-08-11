# cyclic-dependency — Enforcer

## Definition
Module, package, service, or project dependencies form a cycle or require mutual initialization.

## Trigger When
Module, package, service, or project dependencies form a cycle or require mutual initialization.

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
The dependency graph is cyclic. Identify the missing boundary or fact flow and restore one-way dependencies.

## Examples
### Positive
Module, package, service, or project dependencies form a cycle or require mutual initialization.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
The dependency graph is cyclic. Identify the missing boundary or fact flow and restore one-way dependencies.
