# lost-update — Lesson

## Opinion

### Definition

A lost update is not merely “two writes happened near each other.” It is a specific corruption of history: two writers both make a decision from version N, one decision is accepted, and a later stale write commits as though version N were still current — erasing an already-accepted fact without anyone explicitly choosing to discard it.

The smell hides inside innocent read/modify/write code:

```text
read current
compute next from current
write next
```

That sequence contains an unstated promise: **the state used to justify this write is still the state being updated**. Under concurrency, that promise is false unless a protocol proves it.

### Governing Principle

The dangerous part is not that “last writer wins.” Last-writer-wins can be a valid domain rule when the domain really says later authority supersedes earlier authority. Lost update is different: scheduler timing silently decides that an older premise may overwrite a newer accepted consequence.

### Trigger When

Fire this rule when:

- two writers can read the same version and later replace the same state;
- update logic derives a whole replacement object from a snapshot and writes it unconditionally;
- a write API returns success even though another accepted change disappeared;
- retrying after conflict simply repeats the stale replacement;
- version/etag/CAS exists in storage but the application does not carry the read version into commit;
- “merge” really means choosing one whole object and dropping fields changed by the other writer.

### Do Not Trigger When

Do **not** fire merely because two operations are concurrent. Atomic commutative updates, append-only facts, a true single-writer owner, or a mathematically valid merge law may all allow concurrency without lost updates.

### Distinguish From

Nearby failures are different. `shared-mutable-concurrency` is about distributed write authority and lock choreography as architecture. `race-first-wins-semantics` is about arrival order choosing business truth. `optimistic-retry-assumption` is about retrying when the previous effect has unknown outcome. Use `lost-update` when the sharp fact is: **an accepted update can vanish because another writer committed from a stale premise**.

### Decision Procedure

A useful test is brutally simple. Start two writers from the same version. Let A commit. Then let B attempt to commit what it computed from the old version. One of only three things should happen:

1. B is rejected as stale and must re-read;
2. a single writer serialized the operations, so B never truly wrote from stale state;
3. a declared merge law preserves both intents.

If B can return success while A's accepted information disappears, the system has rewritten history without admitting it.

The repair mechanism is not “add some locking.” A lock that covers only the final write is useless; the stale premise was created at read time. The protocol must bind **read identity to commit identity** across the whole logical update.

### Examples


## Advice

### What To Do Now

Repair the update protocol at the state owner. Do not patch individual call sites with ad hoc retries or partial locks.

### Why This Matters

The invariant is stronger than “writes do not crash.” It is:

> **No accepted intent may disappear unless a domain rule explicitly supersedes or merges it.**

### Repair Strategy

Choose the ownership model that actually matches the domain.

If there is one natural authority, use one writer. Queue commands through that owner and let it serialize state transitions. This is often simpler than teaching every caller a distributed conflict protocol.

If multiple writers are legitimate, carry the version/etag/revision observed at read time into the commit and use an atomic compare-and-swap. A stale writer must receive an explicit conflict, re-read current state, recompute its intent, and decide again. Do not silently “retry the same write”; the computation was justified by an old world.

If concurrent intents can be combined, define a real merge law over **intent or facts**, not a heuristic merge of two replacement objects. State what must be associative/commutative/idempotent if those properties are required, and test the law under permutation.

### Decision Branches


### Common Wrong Fixes

Common fake repairs:

- locking only the final `write()` while reads remain concurrent;
- retrying unconditional replacement writes until one succeeds;
- adding timestamps and calling last-write-wins “conflict resolution” when no domain rule says later wall-clock arrival supersedes earlier intent;
- merging field-by-field without ownership rules, so one writer still erases a field another writer legitimately changed;
- returning 200/OK after storage accepted the stale write, then relying on audit logs to explain why data vanished;
- using an in-memory mutex in one process while several processes or hosts still write the same durable record.

### Verification

Verification must force the actual conflict, not merely run two promises in parallel and hope timing overlaps. Arrange both writers to observe the same revision, pause them, commit A, then release B. Assert that B cannot erase A silently. Repeat for multi-process/storage boundaries if those are in the real topology.

Also test recovery semantics. If B is rejected and retried, the retry must recompute from the new state rather than replaying stale derived bytes. If merge is used, permute arrival order and prove the declared merge invariant.

### Done When

You are done when every write can answer one of these questions clearly:

- **Which version justified me?**
- **Who serialized me?**
- **What merge law makes my stale premise safe?**

If the answer is “the database accepted it,” the defect is still present. Acceptance by storage is not proof that the write was causally entitled to overwrite the current state.

### Nudge

> A write derived from version N has no right to commit against N+1 unless a merge law explicitly grants that right.
