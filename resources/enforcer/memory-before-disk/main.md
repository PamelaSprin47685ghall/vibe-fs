# memory-before-disk — Main

Move authority behind the durability boundary.

Compute the candidate transition in memory if useful, but do not publish it, return success from it, launch dependent effects from it, or swap it into authoritative shared state until the durable commit that justifies the transition has succeeded.

The target sequence is:

```text
old authoritative state
        ↓ pure/isolated decision
candidate transition / fact
        ↓ durable commit
committed fact
        ↓ fold/apply
new authoritative memory
        ↓ consequences / success
```

This ordering creates the recovery asymmetry you want:

- persistence fails → the command did not happen, authoritative memory stays old;
- persistence succeeds but process dies before memory updates → restart can replay the committed fact and recover the new state.

The inverse ordering has no such safe interpretation. If memory advanced and influenced anything before persistence, a crash can erase the evidence while keeping consequences that were caused by the erased state.

Keep private speculative objects genuinely private. It is fine to compute `nextState` before commit, hash it, validate it, or prepare derived artifacts. The line you must not cross is **authoritative escape**: no other command, callback, provider response, child effect, publication, or shared reader may treat the candidate as true until commit succeeds.

Common fake repairs:

- mutate memory first, then “roll back” if persistence fails;
- publish success and persist asynchronously “for latency”;
- append to a process-local buffer and call that durable even though recovery cannot read it after crash;
- update cache/projection first because database commit is “expected to succeed”;
- perform external effects from candidate state, then persist the state afterward;
- catch persistence failure and leave memory advanced because “the process is still alive”;
- rely on graceful shutdown to flush pending facts when hard crash/power loss is within the durability contract.

Rollback is especially deceptive. Once advanced memory has been observed, rollback cannot retract the decisions those observers already made. It can only create another transition and hope every escaped consequence is reversible.

Verification needs fault injection around the exact ordering boundary:

1. fail before durable commit — no authoritative memory change and no dependent effect may escape;
2. fail during commit — outcome follows the storage protocol's explicit committed/not-committed/unknown semantics;
3. commit successfully, crash before memory apply — restart must reconstruct the new state;
4. commit + apply — observers see exactly what replay would reconstruct.

If durability is asynchronous or replicated, define precisely what “commit succeeded” means for recovery. Do not let one code path treat local write as committed while restart requires quorum/fsync.

Also test concurrent readers. They must not see candidate state before durable success merely because it has already been assigned to a mutable field.

You are done when every authoritative state transition has a durable witness that precedes its visibility, and every visible state can be reconstructed from the same durability boundary after restart.

> Durable history earns authority first. Memory is its fast projection, not its impatient predecessor.
