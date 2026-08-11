# expected-failure-as-exception — Enforcer

## Definition
A foreseeable business outcome such as not found, unauthorized, insufficient balance, or invalid transition is represented by an exception.

## Trigger When
A foreseeable business outcome such as not found, unauthorized, insufficient balance, or invalid transition is represented by an exception.

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
An expected business outcome is being treated as an exception. Return a typed result that forces callers to handle it.

## Examples
### Positive
A foreseeable business outcome such as not found, unauthorized, insufficient balance, or invalid transition is represented by an exception.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
An expected business outcome is being treated as an exception. Return a typed result that forces callers to handle it.
