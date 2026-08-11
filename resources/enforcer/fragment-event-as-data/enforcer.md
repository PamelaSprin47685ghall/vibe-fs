# fragment-event-as-data — Enforcer

## Definition
A transport fragment is mistaken for domain data when partial stream updates, deltas, callback order, or notification payloads are assembled as though they were the authoritative business fact.

## Governing Principle
Notification and state answer different questions. A notification says “something changed”; authoritative state says “what is true now.” Transport systems may coalesce, reorder, duplicate, or omit intermediate fragments while still honoring their contract. Building domain truth from those fragments silently strengthens the transport guarantee into one it never promised.

## Trigger When
Trigger when business state is reconstructed from incremental provider/stream events even though a complete authoritative snapshot is available and event delivery is not itself the durable source of truth.

## Do Not Trigger When
Do not trigger for true event-sourced protocols where each event is an ordered durable domain fact and replay is the specified authority.

## Distinguish From
log-as-recovery-protocol elevates diagnostics to facts. snapshot-as-truth elevates a derived projection. This rule concerns ephemeral transport deltas being promoted into domain truth.

## Decision Procedure
Classify the stream: fact log or wake-up signal? If the contract permits missing/coalesced/reordered fragments, it cannot safely define domain state. React to it, then read the authoritative snapshot.

## Nudge
Do not infer truth from notification choreography. Treat fragments as signals unless the protocol explicitly makes them durable ordered facts; read the authoritative state for meaning.
