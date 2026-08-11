# wrong-rule-composition — Enforcer

## Definition
Dependent rules collect meaningless downstream errors instead of short-circuiting, or independent rules stop early instead of returning the full error set.

## Trigger When
Dependent rules collect meaningless downstream errors instead of short-circuiting, or independent rules stop early instead of returning the full error set.

## Do Not Trigger When
Do not fire when composition already short-circuits dependent checks and accumulates independent failures by design.

## Distinguish From
rule-spaghetti hides rules in control flow; missing-rule-combinator lacks tools; this tip misuses composition semantics.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The rule composition strategy is wrong. Short-circuit dependent checks and accumulate independent failures.

## Examples
### Positive
Dependent rules collect meaningless downstream errors instead of short-circuiting, or independent rules stop early instead of returning the full error set.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when composition already short-circuits dependent checks and accumulates independent failures by design.
