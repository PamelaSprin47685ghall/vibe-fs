# memory-before-disk — Enforcer

Id: enforcement-e01 / Family: E / Ordinal: 41

## ScoreWhen

Authoritative in-memory state is changed before the durable fact that justifies the change is committed.

## Nudge

Memory was updated before durability. Commit the fact first, then derive runtime state from it.
