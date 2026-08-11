# fragment-event-as-data — Enforcer

## Definition
A transport fragment is mistaken for domain data when partial stream updates, deltas, callback order, or notification payloads are assembled as though they were the authoritative business fact. The root-cause is that transport fragments that may drop, coalesce, or reorder are assembled as domain truth, so the client believes a world the source never promised.

## Governing Principle
Notification and state answer different questions. A notification says “something changed”; authoritative state says “what is true now.” Transport systems may coalesce, reorder, duplicate, or omit intermediate fragments while still honoring their contract. Building domain truth from those fragments silently strengthens the transport guarantee into one it never promised.

## Trigger When
Trigger when business state is reconstructed from incremental provider/stream events even though a complete authoritative snapshot is available and event delivery is not itself the durable source of truth.

## Do Not Trigger When
- The protocol is true event sourcing: each event is an ordered durable domain fact and replay is the specified authority.
- Notifications only wake a reader that then fetches the authoritative snapshot.
- A local UI hint is explicitly ephemeral and never written as domain state.
- Duplicate/coalesce behavior is tested against a documented durability and ordering contract the provider actually gives.

## Distinguish From
`log-as-recovery-protocol` elevates diagnostics to facts. `snapshot-as-truth` elevates a derived projection. This rule concerns ephemeral transport deltas being promoted into domain truth. Tie-break: if fragments may be dropped, coalesced, or reordered and yet are assembled as the business fact, this rule owns the case.

## Decision Procedure
Classify the stream: fact log or wake-up signal? If the contract permits missing/coalesced/reordered fragments, it cannot safely define domain state. React to it, then read the authoritative snapshot.

## Examples
- positive: a client folds websocket patches into an `Order` record and never re-reads the server snapshot.
- near-miss: the websocket only triggers `GET /orders/:id`, and domain logic uses that complete payload.
- counterexample: treat fragments as signals and read authoritative state for meaning, unless the protocol makes events durable ordered facts.

## Nudge
Do not infer truth from notification choreography. Treat fragments as signals unless the protocol explicitly makes them durable ordered facts; read the authoritative state for meaning.
