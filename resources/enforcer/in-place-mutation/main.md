# in-place-mutation — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Shared or externally visible state is overwritten in place, destroying the explicit transition from the previous value to the next value.

## What to do

Shared state is being mutated in place. Compute a new value or record an explicit transition instead.

## Reference

Family D, enforcement-d01, ordinal 31.
