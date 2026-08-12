# lost-update — Enforcer

A lost update is not merely “two writes happened near each other.” It is a specific corruption of history: two writers both make a decision from version N, one decision is accepted, and a later stale write commits as though version N were still current — erasing an already-accepted fact without anyone explicitly choosing to discard it.

The smell hides inside innocent read/modify/write code:

```text
read current
compute next from current
write next
```

That sequence contains an unstated promise: **the state used to justify this write is still the state being updated**. Under concurrency, that promise is false unless a protocol proves it.

The dangerous part is not that “last writer wins.” Last-writer-wins can be a valid domain rule when the domain really says later authority supersedes earlier authority. Lost update is different: scheduler timing silently decides that an older premise may overwrite a newer accepted consequence.

Fire this rule when:

- two writers can read the same version and later replace the same state;
- update logic derives a whole replacement object from a snapshot and writes it unconditionally;
- a write API returns success even though another accepted change disappeared;
- retrying after conflict simply repeats the stale replacement;
- version/etag/CAS exists in storage but the application does not carry the read version into commit;
- “merge” really means choosing one whole object and dropping fields changed by the other writer.

Do **not** fire merely because two operations are concurrent. Atomic commutative updates, append-only facts, a true single-writer owner, or a mathematically valid merge law may all allow concurrency without lost updates.

Nearby failures are different. `shared-mutable-concurrency` is about distributed write authority and lock choreography as architecture. `race-first-wins-semantics` is about arrival order choosing business truth. `optimistic-retry-assumption` is about retrying when the previous effect has unknown outcome. Use `lost-update` when the sharp fact is: **an accepted update can vanish because another writer committed from a stale premise**.

A useful test is brutally simple. Start two writers from the same version. Let A commit. Then let B attempt to commit what it computed from the old version. One of only three things should happen:

1. B is rejected as stale and must re-read;
2. a single writer serialized the operations, so B never truly wrote from stale state;
3. a declared merge law preserves both intents.

If B can return success while A's accepted information disappears, the system has rewritten history without admitting it.

The repair mechanism is not “add some locking.” A lock that covers only the final write is useless; the stale premise was created at read time. The protocol must bind **read identity to commit identity** across the whole logical update.

> A write derived from version N has no right to commit against N+1 unless a merge law explicitly grants that right.
