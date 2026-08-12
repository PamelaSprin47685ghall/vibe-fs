# shared-mutable-concurrency — Main

Do not begin by choosing a cleverer lock. Begin by choosing who owns mutation.

The repair target is a semantic invariant, not a synchronization primitive. Identify the state that must remain coherent, the operations allowed to change it, and the single authority that can decide those transitions. Then make every other concurrent participant communicate **intent** across that boundary rather than directly editing the same state.

A strong shape is:

```text
many concurrent callers
        ↓ commands / immutable facts
one mutation owner
        ↓ serialized transition law
owned mutable state
        ↓ immutable observations / events
many concurrent readers
```

The owner may be an actor, queue consumer, aggregate, workflow state machine, or process-local coordinator. The name matters less than the property: only that owner performs mutation; callers cannot bypass it by holding a reference to the underlying state.

Prefer one writer when:

- several fields participate in one invariant;
- transition legality depends on current state;
- cancellation/supersession changes who is still entitled to commit;
- order matters semantically but should be decided by the owner, not by lock arrival;
- recovery must replay one authoritative history.

Keep a concurrent primitive directly when its semantics already match the whole problem. An atomic monotonic counter may need no actor. A concurrent set may be correct if membership operations are independent. A lock around one short-lived OS handle can be cleaner than inventing a service around it. Do not build ownership theater around a primitive whose native atomic law is already the desired domain law.

Common fake repairs:

- one enormous global mutex around all state;
- converting every field to atomic while cross-field invariants remain non-atomic;
- wrapping a shared object in a “thread-safe” facade while callers still own sequencing decisions;
- documenting lock order instead of shrinking the number of writers;
- moving shared mutable state into a singleton and calling that “centralized ownership” even though every caller can still mutate it;
- using a database transaction as a universal excuse while application-level ownership remains ambiguous.

Verification should attack authority, not just races. Try to mutate the state from a non-owner path; the API should make that impossible or clearly unauthorized. Permute scheduler order of concurrent commands and verify outcomes follow declared command semantics. Inject cancellation and stale callbacks and prove they cannot write once ownership has moved on.

Measure success by the shape of reasoning after the refactor. A maintainer should be able to answer:

- who may mutate this state;
- how commands reach that owner;
- where transition legality lives;
- what readers are allowed to observe;
- how ownership ends or transfers.

If the answer still begins with “take lock X, unless...”, the architecture is still paying synchronization folklore instead of expressing ownership.

> Concurrency should multiply progress, not multiply authorities over the same fact.
