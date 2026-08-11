# duplicated-control-flow — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

The same workflow, retry sequence, validation order, or state transition algorithm is independently implemented in multiple places.

## What to do

The same control algorithm has multiple owners. Establish one canonical implementation and route all callers through it.

## Reference

Family B, enforcement-b08, ordinal 18.
