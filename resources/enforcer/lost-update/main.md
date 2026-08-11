# lost-update — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Concurrent writers perform read-modify-write without version checking, compare-and-swap, serialization, or another conflict protocol.

## What to do

Concurrent updates can overwrite each other. Add a versioned compare-and-swap or a single writer.

## Reference

Family F, enforcement-f09, ordinal 59.
