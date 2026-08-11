# shared-mutable-concurrency — Enforcer

Id: enforcement-f03 / Family: F / Ordinal: 53

## ScoreWhen

Concurrent workers coordinate by mutating shared state protected by ad hoc locks rather than owning state or exchanging messages.

## Nudge

Concurrent workers share mutable state. Prefer ownership, message passing, or a single serialized writer.
