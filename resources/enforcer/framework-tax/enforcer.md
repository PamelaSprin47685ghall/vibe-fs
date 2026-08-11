# framework-tax — Enforcer

## Definition
Configuration, lifecycle hooks, dependency injection ceremony, generated layers, or framework conventions exceed the essential complexity of the problem.

## Trigger When
Configuration, lifecycle hooks, dependency injection ceremony, generated layers, or framework conventions exceed the essential complexity of the problem.

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
Framework ceremony is larger than the problem. Remove the framework tax and expose the underlying operation directly.

## Examples
### Positive
Configuration, lifecycle hooks, dependency injection ceremony, generated layers, or framework conventions exceed the essential complexity of the problem.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Framework ceremony is larger than the problem. Remove the framework tax and expose the underlying operation directly.
