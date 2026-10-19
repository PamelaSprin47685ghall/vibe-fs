# blob-after-event — Lesson

## Opinion

### Definition

`blob-after-event` is a temporal referential-integrity failure: durable history names content before that content has crossed its own durability boundary.

A reference is not just an identifier. Once a journal/event/manifest commits `blob = H`, it has made a promise to every future replay:

> The content identified by H exists as part of this history and can be recovered under the storage contract.

If the blob upload/write is still pending, only buffered in memory, only present in a temp path, or can still fail independently after the event becomes durable, the history has committed a statement it cannot yet prove.

That creates dangling history rather than merely a missing cache entry.

### Governing Principle

The ordering choice is deliberately asymmetric. An unreferenced durable blob is usually recoverable garbage; a referenced missing blob is a contradiction in history. Therefore referent-first ordering deliberately prefers harmless orphan possibility over unrecoverable dangling reference.

### Trigger When

Fire this rule when:

- an event/journal row containing a blob/content reference commits before the blob store confirms durable availability;
- a manifest/index is published first and “large payload upload” follows asynchronously;
- a content hash is computed from in-memory bytes, referenced durably, then the actual bytes are written later;
- a blob write returns from a local buffer/temporary location while recovery requires a stronger remote/fsync/quorum durability boundary;
- cleanup can remove a temp blob after its durable reference already exists;
- replay has a normal branch for “event exists but blob missing” even though the domain never intended dangling references as a legal state.

### Do Not Trigger When

Do not fire when blob and reference commit atomically under one real transaction whose recovery guarantee covers both. Do not fire when the reference points to content already durably present under content-addressed semantics. Inline content also has no separate referent ordering problem.

### Distinguish From

The relevant distinction from `memory-before-disk` is that both sides here may be durable artifacts. The defect is **ordering between durable referent and durable reference**, not volatile memory outrunning persistence.

`partial-write-assumption` asks whether recovery invented an unsupported storage state. `blob-after-event` is different: a perfectly valid event can become a real committed dangling pointer because application ordering allowed it.

### Decision Procedure

The repair must use the **actual durability success condition** of the blob store. “Upload request returned” is not enough if the storage API only guarantees availability after commit/quorum/finalize. Verify the boundary the replay path relies on.

For content-addressed stores, also verify hash identity from the bytes actually persisted, not only from the caller's pre-upload buffer. The durable reference should identify what recovery will read.

### Examples

A useful crash table:

```text
before blob durable               → no event reference may exist
blob durable, before event append → orphan blob is acceptable/collectable
after event append                → blob must be readable forever per retention policy
```

## Advice

### What To Do Now

Reverse the publication order or make it truly atomic.

If blob and reference are separate commits, persist the blob first, wait for the blob store's **recovery-grade durability guarantee**, verify identity as required, and only then append the event/manifest/index record that makes the reference part of history.

### Why This Matters

The desired invariant is:

> **Every committed reference resolves to a durably readable referent under the same recovery contract.**

### Repair Strategy

A safe two-step protocol therefore looks like:

```text
prepare content
persist blob H
verify durable/committed H
append history reference H
publish consequences
```

If the underlying store offers a real atomic transaction spanning blob and reference, use it — but verify the guarantee instead of simulating atomicity by issuing two async writes close together.

For content-addressed storage:

- compute/verify identity from the exact bytes accepted by the store;
- ensure replay validates digest if corruption/substitution matters;
- retries must reuse the same content identity rather than publishing a new reference before the old one is resolved;
- garbage collection must never delete blobs still reachable from retained history.

### Decision Branches

If the process dies after blob commit but before event append, you may leave an orphan blob. That is usually acceptable: garbage collection can later remove unreferenced content according to a retention policy.

If the process dies after event append, replay must be able to read H. There should be no normal semantic branch for “history says content existed, but maybe upload had not finished.” Correct ordering removes that world.

### Common Wrong Fixes

Common fake repairs:

- append event first and queue blob upload “immediately after”;
- tolerate missing blob on replay and retry forever, turning corruption into normal control flow;
- write to a temp file, publish the reference, then rename/finalize later without an atomic rename contract that recovery trusts;
- trust an SDK callback that means “buffered locally” while recovery requires remote replication;
- publish a hash from pre-upload memory and never verify persisted bytes;
- catch blob upload failure after the event is already committed and emit another “blob missing” event — history is still internally contradictory at the earlier point;
- make the event mutable so the blob reference can be filled in later.

### Verification

Verification should inject crashes at each boundary:

1. before blob durability — no committed reference;
2. after blob durability, before event — orphan content only;
3. after event — reference resolves;
4. during replay — digest/identity checks behave according to contract;
5. during garbage collection — reachable content survives.

Also test blob-store acknowledgement loss if relevant. Unknown blob outcome should be resolved by content identity/status before deciding whether the reference may publish.

### Done When

You are done when a replay engine can treat every committed reference as a fact rather than as a request to participate in storage archaeology.

> Orphan bytes are cheaper than orphaned truth. Prefer durable content waiting for a reference over durable history waiting for content.

### Nudge

> Make the content real before making its name historical.
