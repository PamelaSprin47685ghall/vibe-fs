# rule-spaghetti — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A rule set is expressed through nested conditionals, temporary flags, mutation, and early exits such that the reader must simulate execution to recover the rule.

## What to do

The business rule is buried in control flow. Rewrite it so the rule can be read directly from the code.

## Reference

Family B, enforcement-b02, ordinal 12.
