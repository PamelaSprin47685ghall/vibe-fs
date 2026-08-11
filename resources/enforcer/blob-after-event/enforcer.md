# blob-after-event — Enforcer

Id: enforcement-e02 / Family: E / Ordinal: 42

## ScoreWhen

A journal event referencing large content is appended before the referenced blob is durably written.

## Nudge

A durable event can point to missing content. Write and verify the blob before appending the reference.
