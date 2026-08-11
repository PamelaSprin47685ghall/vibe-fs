# wrong-rule-composition — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Dependent rules collect meaningless downstream errors instead of short-circuiting, or independent rules stop early instead of returning the full error set.

## What to do

The rule composition strategy is wrong. Short-circuit dependent checks and accumulate independent failures.

## Reference

Family B, enforcement-b04, ordinal 14.
