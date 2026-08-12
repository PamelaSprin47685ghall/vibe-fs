# lost-update — Main

Repair the update protocol at the state owner. Do not patch individual call sites with ad hoc retries or partial locks.

The invariant is stronger than “writes do not crash.” It is:

> **No accepted intent may disappear unless a domain rule explicitly supersedes or merges it.**

Choose the ownership model that actually matches the domain.

If there is one natural authority, use one writer. Queue commands through that owner and let it serialize state transitions. This is often simpler than teaching every caller a distributed conflict protocol.

If multiple writers are legitimate, carry the version/etag/revision observed at read time into the commit and use an atomic compare-and-swap. A stale writer must receive an explicit conflict, re-read current state, recompute its intent, and decide again. Do not silently “retry the same write”; the computation was justified by an old world.

If concurrent intents can be combined, define a real merge law over **intent or facts**, not a heuristic merge of two replacement objects. State what must be associative/commutative/idempotent if those properties are required, and test the law under permutation.

Common fake repairs:

- locking only the final `write()` while reads remain concurrent;
- retrying unconditional replacement writes until one succeeds;
- adding timestamps and calling last-write-wins “conflict resolution” when no domain rule says later wall-clock arrival supersedes earlier intent;
- merging field-by-field without ownership rules, so one writer still erases a field another writer legitimately changed;
- returning 200/OK after storage accepted the stale write, then relying on audit logs to explain why data vanished;
- using an in-memory mutex in one process while several processes or hosts still write the same durable record.

Verification must force the actual conflict, not merely run two promises in parallel and hope timing overlaps. Arrange both writers to observe the same revision, pause them, commit A, then release B. Assert that B cannot erase A silently. Repeat for multi-process/storage boundaries if those are in the real topology.

Also test recovery semantics. If B is rejected and retried, the retry must recompute from the new state rather than replaying stale derived bytes. If merge is used, permute arrival order and prove the declared merge invariant.

You are done when every write can answer one of these questions clearly:

- **Which version justified me?**
- **Who serialized me?**
- **What merge law makes my stale premise safe?**

If the answer is “the database accepted it,” the defect is still present. Acceptance by storage is not proof that the write was causally entitled to overwrite the current state.
