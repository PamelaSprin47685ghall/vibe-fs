# stringly-typed-error — Enforcer

## Definition
Callers decide program behavior by interpreting error strings, localized text, message fragments, or regular expressions.

## Trigger When
Callers interpret error strings, localized text, message fragments, or regular expressions to determine program behavior.

## Do Not Trigger When
Do not fire when strings are display-only and control flow already branches on a typed code/case before formatting.

## Distinguish From
weak-boundary-parsing is general untyped data; expected-failure-as-exception is control via exceptions; this tip is parsing error prose for logic.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Program logic is parsing error prose. Replace the string contract with a closed typed error value.

## Examples
### Positive
Callers interpret error strings, localized text, message fragments, or regular expressions to determine program behavior.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when strings are display-only and control flow already branches on a typed code/case before formatting.
