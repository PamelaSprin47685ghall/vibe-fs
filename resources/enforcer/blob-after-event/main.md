# blob-after-event — Main

## What To Do Now
Write the blob to its durable store, verify the store’s success condition, then append the event or manifest entry that references it.

## Why This Matters
A history that names missing content is not merely incomplete; it is self-contradictory. Replay trusts committed events as facts. Once such a reference is admitted, every recovery path must either lie about the past or invent exceptional repair semantics for a state that correct ordering could have made impossible.

## Repair Strategy
Make blob durability the precondition of reference publication. Prefer content-addressed identity where appropriate, and treat a failed blob write as “the event did not happen.” If atomic multi-object commit exists, use its actual guarantee rather than simulating one in memory.

## Decision Branches
- If the store offers a true atomic commit of blob plus reference, use that guarantee and do not simulate it in memory.
- If commits are separate, persist and verify the blob first; append the reference only after that success condition.
- If the blob write fails, do not append; the event did not happen.

## Common Wrong Fixes
- Do not append first and “fill the blob soon after.”
- Do not tolerate missing blobs during replay as a normal path.
- Do not retry uploads under a new identity after the old identity was already referenced.
- Do not treat an in-memory write as durability for a history event.

## Verification
Crash the reasoning at every boundary: before blob commit, after blob commit but before event append, and after event append. Every surviving durable state must be replayable. The invariant is that no committed reference exists without a durably readable referent.

## Done When
No committed reference can exist without a durably readable referent, and recovery never needs to guess whether referenced content once existed.
