# shared-mutable-concurrency — Enforcer

Shared mutable concurrency is not “multiple threads exist.” It is the architectural decision to let several execution contexts hold **write authority over the same semantic state** and then reconstruct correctness through locks, atomics, timing discipline, or conventions.

The root problem is shared sovereignty.

A mutex can prove that two instructions did not execute simultaneously. It cannot, by itself, prove which fields form one invariant, which operation owns a transition, whether two locks compose safely, whether a callback is still entitled to mutate after cancellation, or whether a future maintainer remembered the same lock discipline on a new path.

That is why lock-heavy designs often grow into folklore:

```text
Take A before B.
Except in callback C.
Field x is protected by A, unless y is also touched.
Reads are lock-free because “it is only a read.”
Do not call helper D while holding A because D may acquire B.
```

At that point the concurrency model is no longer visible in the domain model. It lives in tribal memory.

Fire this rule when:

- several handlers/workers can directly mutate one domain object or durable projection;
- correctness of one business invariant depends on callers acquiring the same lock in the same way;
- compound state is split across several atomics and code assumes snapshots are coherent merely because each field is atomic;
- lock ordering becomes a second architecture beside ownership;
- callbacks retain references to mutable state and may write after the logical owner has moved on;
- tests need scheduler luck or giant critical sections to prove ordinary domain transitions.

Do not fire merely because shared concurrency primitives exist. A narrow concurrent queue, atomic counter, immutable snapshot cache, or well-specified lock around one truly shared low-level resource can be exactly right. The question is not “is there a lock?” The question is **does the lock carry domain ownership that should belong somewhere else?**

Distinguish carefully. `lost-update` is one concrete corruption where stale replacement erases an accepted write. `race-first-wins-semantics` is scheduler timing choosing business truth. `permit-leak` is capacity never returned. `shared-mutable-concurrency` is broader: several actors are jointly sovereign over one mutable domain state.

A strong diagnostic question is:

> If I removed every lock comment, could I still point to one component and say, “this is the authority that changes this state”?

If the honest answer is “no, correctness comes from all callers following the same locking convention,” then the architecture has distributed an invariant across participants that should probably have one owner.

The preferred repair is usually to move mutation behind a single semantic owner: actor, serialized command processor, aggregate, state machine, or other boundary where commands arrive and state changes one-at-a-time according to domain law. Concurrency happens **between owners**, not by several owners reaching into the same mutable world.

This is not a religion against locks. Sometimes the OS, a library, or a tiny performance-critical structure genuinely needs one. The rule attacks using synchronization as a substitute for ownership.

> A lock can exclude another hand. It cannot tell you whose hand had the right to change the thing in the first place.
