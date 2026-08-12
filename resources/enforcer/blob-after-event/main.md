# blob-after-event — Main

Reverse the publication order or make it truly atomic.

If blob and reference are separate commits, persist the blob first, wait for the blob store's **recovery-grade durability guarantee**, verify identity as required, and only then append the event/manifest/index record that makes the reference part of history.

The desired invariant is:

> **Every committed reference resolves to a durably readable referent under the same recovery contract.**

A safe two-step protocol therefore looks like:

```text
prepare content
persist blob H
verify durable/committed H
append history reference H
publish consequences
```

If the process dies after blob commit but before event append, you may leave an orphan blob. That is usually acceptable: garbage collection can later remove unreferenced content according to a retention policy.

If the process dies after event append, replay must be able to read H. There should be no normal semantic branch for “history says content existed, but maybe upload had not finished.” Correct ordering removes that world.

If the underlying store offers a real atomic transaction spanning blob and reference, use it — but verify the guarantee instead of simulating atomicity by issuing two async writes close together.

For content-addressed storage:

- compute/verify identity from the exact bytes accepted by the store;
- ensure replay validates digest if corruption/substitution matters;
- retries must reuse the same content identity rather than publishing a new reference before the old one is resolved;
- garbage collection must never delete blobs still reachable from retained history.

Common fake repairs:

- append event first and queue blob upload “immediately after”;
- tolerate missing blob on replay and retry forever, turning corruption into normal control flow;
- write to a temp file, publish the reference, then rename/finalize later without an atomic rename contract that recovery trusts;
- trust an SDK callback that means “buffered locally” while recovery requires remote replication;
- publish a hash from pre-upload memory and never verify persisted bytes;
- catch blob upload failure after the event is already committed and emit another “blob missing” event — history is still internally contradictory at the earlier point;
- make the event mutable so the blob reference can be filled in later.

Verification should inject crashes at each boundary:

1. before blob durability — no committed reference;
2. after blob durability, before event — orphan content only;
3. after event — reference resolves;
4. during replay — digest/identity checks behave according to contract;
5. during garbage collection — reachable content survives.

Also test blob-store acknowledgement loss if relevant. Unknown blob outcome should be resolved by content identity/status before deciding whether the reference may publish.

You are done when a replay engine can treat every committed reference as a fact rather than as a request to participate in storage archaeology.

> Orphan bytes are cheaper than orphaned truth. Prefer durable content waiting for a reference over durable history waiting for content.