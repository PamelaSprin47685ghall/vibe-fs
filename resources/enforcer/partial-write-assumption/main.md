# partial-write-assumption — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Recovery logic assumes an append or effect may be partially committed despite the storage contract defining committed versus unknown outcomes.

## What to do

Recovery is inventing a partial-write state. Follow the storage contract’s explicit committed and unknown outcomes.

## Reference

Family E, enforcement-e03, ordinal 43.
