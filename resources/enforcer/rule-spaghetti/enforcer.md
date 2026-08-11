# rule-spaghetti — Enforcer

## Definition
Business rules are buried in nested conditionals, temporary flags, mutation, and early exits so readers must simulate execution to recover the rule.

## Trigger When
A rule set is expressed through nested conditionals, temporary flags, mutation, and early exits such that the reader must simulate execution to recover the rule.

## Do Not Trigger When
Do not fire when the rule is already a declarative table, combinator pipeline, or pattern match that reads as the specification.

## Distinguish From
wrong-rule-composition mis-wires combinators; missing-rule-combinator lacks composition tools; this tip is imperative spaghetti hiding the rule.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The business rule is buried in control flow. Rewrite it so the rule can be read directly from the code.

## Examples
### Positive
A rule set is expressed through nested conditionals, temporary flags, mutation, and early exits such that the reader must simulate execution to recover the rule.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the rule is already a declarative table, combinator pipeline, or pattern match that reads as the specification.
