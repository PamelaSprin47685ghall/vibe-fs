# fragment-event-as-data — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Partial stream events, deltas, update ordering, or transport fragments are assembled into business facts instead of reading a complete snapshot.

## What to do

Transport fragments are being treated as domain data. Use events only as wake-up signals and read the complete authoritative snapshot.

## Reference

Family D, enforcement-d08, ordinal 38.
