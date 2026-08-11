# rule-spaghetti — Enforcer

Id: enforcement-b02 / Family: B / Ordinal: 12

## ScoreWhen

A rule set is expressed through nested conditionals, temporary flags, mutation, and early exits such that the reader must simulate execution to recover the rule.

## Nudge

The business rule is buried in control flow. Rewrite it so the rule can be read directly from the code.
