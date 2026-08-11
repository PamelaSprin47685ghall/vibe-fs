# domain-language-drift — Enforcer

## Definition
Several names refer to the same concept, or one name is used for several different concepts across modules.

## Trigger When
Several names refer to the same concept, or one name is used for several different concepts across modules.

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
Domain language is drifting. Choose one term per concept and separate concepts that currently share a name.

## Examples
### Positive
Several names refer to the same concept, or one name is used for several different concepts across modules.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Domain language is drifting. Choose one term per concept and separate concepts that currently share a name.
