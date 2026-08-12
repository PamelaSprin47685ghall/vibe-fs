# blob-after-event — Enforcer

`blob-after-event` is a temporal referential-integrity failure: durable history names content before that content has crossed its own durability boundary.

A reference is not just an identifier. Once a journal/event/manifest commits `blob = H`, it has made a promise to every future replay:

> The content identified by H exists as part of this history and can be recovered under the storage contract.

If the blob upload/write is still pending, only buffered in memory, only present in a temp path, or can still fail independently after the event becomes durable, the history has committed a statement it cannot yet prove.

That creates dangling history rather than merely a missing cache entry.

Fire this rule when:

- an event/journal row containing a blob/content reference commits before the blob store confirms durable availability;
- a manifest/index is published first and “large payload upload” follows asynchronously;
- a content hash is computed from in-memory bytes, referenced durably, then the actual bytes are written later;
- a blob write returns from a local buffer/temporary location while recovery requires a stronger remote/fsync/quorum durability boundary;
- cleanup can remove a temp blob after its durable reference already exists;
- replay has a normal branch for “event exists but blob missing” even though the domain never intended dangling references as a legal state.

Do not fire when blob and reference commit atomically under one real transaction whose recovery guarantee covers both. Do not fire when the reference points to content already durably present under content-addressed semantics. Inline content also has no separate referent ordering problem.

The relevant distinction from `memory-before-disk` is that both sides here may be durable artifacts. The defect is **ordering between durable referent and durable reference**, not volatile memory outrunning persistence.

`partial-write-assumption` asks whether recovery invented an unsupported storage state. `blob-after-event` is different: a perfectly valid event can become a real committed dangling pointer because application ordering allowed it.

A useful crash table:

```text
before blob durable               → no event reference may exist
blob durable, before event append → orphan blob is acceptable/collectable
after event append                → blob must be readable forever per retention policy
```

Notice the asymmetry. An unreferenced durable blob is usually recoverable garbage; a referenced missing blob is a contradiction in history. Therefore referent-first ordering deliberately prefers harmless orphan possibility over unrecoverable dangling reference.

The repair must use the **actual durability success condition** of the blob store. “Upload request returned” is not enough if the storage API only guarantees availability after commit/quorum/finalize. Verify the boundary the replay path relies on.

For content-addressed stores, also verify hash identity from the bytes actually persisted, not only from the caller's pre-upload buffer. The durable reference should identify what recovery will read.

> Make the content real before making its name historical.