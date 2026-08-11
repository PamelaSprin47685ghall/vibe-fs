# context-model-leak — Enforcer

## Definition
One shared model is reused across authentication, ordering, sessions, persistence, UI, or other contexts that assign it different meanings.

## Trigger When
One shared model is reused across authentication, ordering, sessions, persistence, UI, or other contexts that assign it different meanings.

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
One model is serving incompatible bounded contexts. Give each context its own concept and translate explicitly.

## Examples
### Positive
One shared model is reused across authentication, ordering, sessions, persistence, UI, or other contexts that assign it different meanings.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
One model is serving incompatible bounded contexts. Give each context its own concept and translate explicitly.
