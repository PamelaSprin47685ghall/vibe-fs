# blob-after-event — Enforcer

## Definition
A reference event is invalidly ordered when it becomes durable before the content it names is itself durably retrievable. The root-cause is that a durable event is appended before its named blob is itself durably retrievable, so replay can observe a committed reference to missing content.

## Governing Principle
A durable event is a promise to every future replay: “this fact existed.” If the event points to a blob that can still disappear, the log has recorded a world that never became reconstructible. Referential integrity is therefore temporal, not merely structural: the referent must become durable before the reference may become history.

## Trigger When
Trigger when a journal, event store, manifest, or index appends a durable reference to large content before the blob write has completed and been verified according to the storage contract.

## Do Not Trigger When
- The blob and reference are committed atomically by one storage transaction.
- The reference deliberately denotes a content address already guaranteed durable.
- The event stores the content inline and does not name an external blob.
- A provisional local cache entry is not yet published as durable history.

## Distinguish From
`memory-before-disk` orders volatile state against durable facts. `partial-write-assumption` invents storage states. This rule concerns durable references whose targets may not yet exist durably. Tie-break: if a committed history record can name missing content, this rule owns the case.

## Decision Procedure
1. Identify the durable reference.
2. Identify the storage guarantee for its target.
3. Ask whether replay can observe the reference before the target is guaranteed readable.
4. If yes, reverse the order or make the commit atomic.

## Examples
- positive: append an event with a blob id, then start the blob upload; a crash leaves a committed name with no bytes.
- near-miss: one storage transaction writes blob and reference together, so replay never sees a dangling name.
- counterexample: persist and verify the blob, then append the event that makes that content part of history.

## Nudge
Durability must flow from referent to reference. Persist and verify the blob first; only then append the event that makes its existence part of history.
