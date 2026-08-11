# memory-before-disk — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Authoritative in-memory state is changed before the durable fact that justifies the change is committed.

## What to do

Memory was updated before durability. Commit the fact first, then derive runtime state from it.

## Reference

Family E, enforcement-e01, ordinal 41.
