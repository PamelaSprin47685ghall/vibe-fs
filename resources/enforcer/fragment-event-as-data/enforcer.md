# fragment-event-as-data — Enforcer

Id: enforcement-d08 / Family: D / Ordinal: 38

## ScoreWhen

Partial stream events, deltas, update ordering, or transport fragments are assembled into business facts instead of reading a complete snapshot.

## Nudge

Transport fragments are being treated as domain data. Use events only as wake-up signals and read the complete authoritative snapshot.
