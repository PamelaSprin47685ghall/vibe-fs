# lost-update — Enforcer

Id: enforcement-f09 / Family: F / Ordinal: 59

## ScoreWhen

Concurrent writers perform read-modify-write without version checking, compare-and-swap, serialization, or another conflict protocol.

## Nudge

Concurrent updates can overwrite each other. Add a versioned compare-and-swap or a single writer.
