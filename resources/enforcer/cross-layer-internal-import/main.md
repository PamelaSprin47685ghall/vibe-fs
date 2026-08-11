# cross-layer-internal-import — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A higher or unrelated layer imports internal implementation members rather than a declared public boundary.

## What to do

A layer is reaching through another layer’s boundary. Depend on the public contract, not its internals.

## Reference

Family C, enforcement-c03, ordinal 23.
