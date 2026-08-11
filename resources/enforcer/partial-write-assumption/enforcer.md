# partial-write-assumption — Enforcer

Id: enforcement-e03 / Family: E / Ordinal: 43

## ScoreWhen

Recovery logic assumes an append or effect may be partially committed despite the storage contract defining committed versus unknown outcomes.

## Nudge

Recovery is inventing a partial-write state. Follow the storage contract’s explicit committed and unknown outcomes.
