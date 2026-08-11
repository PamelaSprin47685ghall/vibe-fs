# fragment-event-as-data — Main

## What To Do Now
Use partial transport events only to trigger a fresh read of the complete authoritative state, unless the protocol explicitly defines those events as durable ordered facts. The authoritative snapshot—or a documented durable ordered event log—is who owns domain facts; wake-up deltas are not.

## Why This Matters
Incremental delivery is usually optimized for responsiveness, not historical completeness. A client that assembles business meaning from fragments implicitly depends on every update, exact ordering, and stable delta semantics. One dropped or coalesced fragment then creates a state that never existed at the source.

## Repair Strategy
Separate notification from truth. On wake-up, fetch or read the authoritative snapshot and derive domain behavior from that complete value. If the stream is genuinely event-sourced, document and test its stronger ordering/durability contract instead.

## Decision Branches
- If the stream may drop, coalesce, or reorder, treat events as wake-ups and read the snapshot.
- If the protocol is durable ordered facts, document that contract and test replay; do not also invent a second snapshot authority.
- If a projection is being treated as live truth, that is `snapshot-as-truth`, not this transport mistake.

## Common Wrong Fixes
- Do not add more buffering or reorder heuristics to compensate for a transport that never promised complete history.
- Do not “debounce harder” and keep folding deltas into domain state.
- Do not persist the assembled fragment stream as the system of record.
- Do not ignore snapshot endpoints because the stream “usually” has every patch.

## Verification
Drop, duplicate, or reorder non-authoritative notifications in a test. The resulting domain state should still converge to the authoritative snapshot. The invariant is that transport may affect when the system refreshes, not what facts it ultimately believes.

## Done When
Transport behavior may affect when the system refreshes, but not what facts the system ultimately believes.
