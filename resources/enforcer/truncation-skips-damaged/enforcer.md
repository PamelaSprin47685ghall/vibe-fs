# truncation-skips-damaged — Enforcer

Id: enforcement-e08 / Family: E / Ordinal: 48

## ScoreWhen

Recovery skips corruption in the middle of durable history and continues applying later facts.

## Nudge

Recovery is continuing past corrupted history. Only a final incomplete record may be truncated; interior corruption must fail closed.
