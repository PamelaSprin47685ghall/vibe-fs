# memory-before-disk — Lesson

## Opinion

### Definition

Memory-before-disk is a durability ordering defect: authoritative runtime state advances before the durable fact that is supposed to justify that advance has committed.

The subtle danger is not merely crash loss. It is **epistemic split-brain inside one process lifetime**.

Once memory moves first, later work in the same process may observe and act on a state that recovery cannot reconstruct. The system has temporarily created a world whose consequences are real while its evidence is not.

### Governing Principle

The core law is:

> **Durable commitment establishes the fact. Authoritative memory may project that fact afterward; it must not outrun it.**

### Trigger When

Typical sequence:

```text
compute transition
mutate in-memory aggregate
publish / answer / launch dependent work
append event or persist record
```

If persistence fails or the process dies in that gap, several impossible histories can result:

- callers were told the command succeeded, but restart forgets it;
- a dependent command acted on state that no durable history contains;
- an event was emitted from memory, then the journal append failed;
- a child effect was launched because memory said “accepted,” but recovery returns to “not accepted”;
- a cache/projection becomes the de facto authority because the durable owner lagged behind it.

### Do Not Trigger When

Do not confuse this with “every byte must hit spinning disk before memory changes.” The relevant durability boundary is whatever the recovery protocol treats as committed: transactional database commit, fsynced WAL, replicated quorum, append acknowledged by the durable journal, etc. A write-ahead log can legitimately be the durable authority even if final materialization happens later.

Also do not fire for private speculative state that cannot escape. Computing a candidate aggregate in memory before commit is fine if nobody can observe it, no effect depends on it, and it is discarded on commit failure. The smell begins when speculative memory acquires authoritative consequences.

### Distinguish From

Nearby rules:

- `blob-after-event` — durable event references content that was not durably present first;
- `snapshot-as-truth` — a derived snapshot is treated as canonical history;
- `overwrite-history` — already committed past facts are mutated;
- `partial-write-assumption` — interrupted persistence is assumed all-or-nothing;
- `unverified-completion-claim` — prose overclaims evidence; here the runtime itself overclaims durable reality.

### Decision Procedure

A decisive crash test is to stop execution at every boundary between “memory changed” and “durable fact committed.” Ask what externally visible behavior could already have happened. Then restart from durable state. If recovery cannot reconstruct the state that those effects assumed, memory had authority it had not earned.

The right implementation shape is usually:

1. derive the intended transition without mutating shared authority;
2. commit the durable fact atomically;
3. fold/apply the committed fact to authoritative memory;
4. only then expose success or launch consequences that rely on the new state.

If step 3 fails after commit, recovery can rebuild. If step 2 fails, the command did not happen. That asymmetry is the point.

### Examples


## Advice

### What To Do Now

Move authority behind the durability boundary.

Compute the candidate transition in memory if useful, but do not publish it, return success from it, launch dependent effects from it, or swap it into authoritative shared state until the durable commit that justifies the transition has succeeded.

### Why This Matters

This ordering creates the recovery asymmetry you want:

- persistence fails → the command did not happen, authoritative memory stays old;
- persistence succeeds but process dies before memory updates → restart can replay the committed fact and recover the new state.

The inverse ordering has no such safe interpretation. If memory advanced and influenced anything before persistence, a crash can erase the evidence while keeping consequences that were caused by the erased state.

### Repair Strategy

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

Keep private speculative objects genuinely private. It is fine to compute `nextState` before commit, hash it, validate it, or prepare derived artifacts. The line you must not cross is **authoritative escape**: no other command, callback, provider response, child effect, publication, or shared reader may treat the candidate as true until commit succeeds.

### Decision Branches

Rollback is especially deceptive. Once advanced memory has been observed, rollback cannot retract the decisions those observers already made. It can only create another transition and hope every escaped consequence is reversible.

### Common Wrong Fixes

Common fake repairs:

- mutate memory first, then “roll back” if persistence fails;
- publish success and persist asynchronously “for latency”;
- append to a process-local buffer and call that durable even though recovery cannot read it after crash;
- update cache/projection first because database commit is “expected to succeed”;
- perform external effects from candidate state, then persist the state afterward;
- catch persistence failure and leave memory advanced because “the process is still alive”;
- rely on graceful shutdown to flush pending facts when hard crash/power loss is within the durability contract.

### Verification

Verification needs fault injection around the exact ordering boundary:

1. fail before durable commit — no authoritative memory change and no dependent effect may escape;
2. fail during commit — outcome follows the storage protocol's explicit committed/not-committed/unknown semantics;
3. commit successfully, crash before memory apply — restart must reconstruct the new state;
4. commit + apply — observers see exactly what replay would reconstruct.

If durability is asynchronous or replicated, define precisely what “commit succeeded” means for recovery. Do not let one code path treat local write as committed while restart requires quorum/fsync.

Also test concurrent readers. They must not see candidate state before durable success merely because it has already been assigned to a mutable field.

### Done When

You are done when every authoritative state transition has a durable witness that precedes its visibility, and every visible state can be reconstructed from the same durability boundary after restart.

> Durable history earns authority first. Memory is its fast projection, not its impatient predecessor.

### Nudge

> Memory is allowed to be fast. It is not allowed to testify about a future that durable history has not yet admitted.
