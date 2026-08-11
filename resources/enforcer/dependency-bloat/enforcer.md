# dependency-bloat — Enforcer

## Definition
A new dependency, plugin, service, or framework is added for behavior that the existing platform or a small local implementation already provides safely.

## Trigger When
A new dependency, plugin, service, or framework is added for behavior that the existing platform or a small local implementation already provides safely.

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
A dependency was added without proportional value. Use the existing platform or a smaller direct implementation.

## Examples
### Positive
A new dependency, plugin, service, or framework is added for behavior that the existing platform or a small local implementation already provides safely.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A dependency was added without proportional value. Use the existing platform or a smaller direct implementation.
