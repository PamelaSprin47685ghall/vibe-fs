# fragment-event-as-data — Main

## What To Do Now
Use partial transport events only to trigger a fresh read of the complete authoritative state, unless the protocol explicitly defines those events as durable ordered facts.

## Why This Matters
Incremental delivery is usually optimized for responsiveness, not historical completeness. A client that assembles business meaning from fragments implicitly depends on every update, exact ordering, and stable delta semantics. One dropped or coalesced fragment then creates a state that never existed at the source.

## Repair Strategy
Separate notification from truth. On wake-up, fetch or read the authoritative snapshot and derive domain behavior from that complete value. If the stream is genuinely event-sourced, document and test its stronger ordering/durability contract instead.

## Wrong Fixes
Do not add more buffering or reorder heuristics to compensate for a transport that never promised complete history. Better reconstruction cannot manufacture guarantees absent from the protocol.

## Verification
Drop, duplicate, or reorder non-authoritative notifications in a test. The resulting domain state should still converge to the authoritative snapshot.

## Done When
Transport behavior may affect when the system refreshes, but not what facts the system ultimately believes.
