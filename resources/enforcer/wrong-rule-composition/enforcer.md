# wrong-rule-composition — Enforcer

Id: enforcement-b04 / Family: B / Ordinal: 14

## ScoreWhen

Dependent rules collect meaningless downstream errors instead of short-circuiting, or independent rules stop early instead of returning the full error set.

## Nudge

The rule composition strategy is wrong. Short-circuit dependent checks and accumulate independent failures.
