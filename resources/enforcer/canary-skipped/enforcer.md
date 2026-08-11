# canary-skipped — Enforcer

## Definition
A behavior that depends on undocumented Host or provider ordering is changed without a real integration canary.

## Trigger When
A behavior that depends on undocumented Host or provider ordering is changed without a real integration canary.

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
An undocumented Host assumption lacks a canary. Prove it against the real boundary before release.

## Examples
### Positive
A behavior that depends on undocumented Host or provider ordering is changed without a real integration canary.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
An undocumented Host assumption lacks a canary. Prove it against the real boundary before release.
