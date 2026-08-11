# shared-mutable-concurrency — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Concurrent workers coordinate by mutating shared state protected by ad hoc locks rather than owning state or exchanging messages.

## What to do

Concurrent workers share mutable state. Prefer ownership, message passing, or a single serialized writer.

## Reference

Family F, enforcement-f03, ordinal 53.
