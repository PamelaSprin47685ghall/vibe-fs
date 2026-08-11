# blob-after-event — Main

## What To Do Now
Write the blob to its durable store, verify the store’s success condition, then append the event or manifest entry that references it.

## Why This Matters
A history that names missing content is not merely incomplete; it is self-contradictory. Replay trusts committed events as facts. Once such a reference is admitted, every recovery path must either lie about the past or invent exceptional repair semantics for a state that correct ordering could have made impossible.

## Repair Strategy
Make blob durability the precondition of reference publication. Prefer content-addressed identity where appropriate, and treat a failed blob write as “the event did not happen.” If atomic multi-object commit exists, use its actual guarantee rather than simulating one in memory.

## Wrong Fixes
Do not append first and “fill the blob soon after.” Do not tolerate missing blobs during replay as normal. Retries without stable blob identity can multiply the inconsistency.

## Verification
Crash the reasoning at every boundary: before blob commit, after blob commit but before event append, and after event append. Every surviving durable state must be replayable.

## Done When
No committed reference can exist without a durably readable referent, and recovery never needs to guess whether referenced content once existed.
