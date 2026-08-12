# memory-before-disk — Enforcer

Memory-before-disk is a durability ordering defect: authoritative runtime state advances before the durable fact that is supposed to justify that advance has committed.

The subtle danger is not merely crash loss. It is **epistemic split-brain inside one process lifetime**.

Once memory moves first, later work in the same process may observe and act on a state that recovery cannot reconstruct. The system has temporarily created a world whose consequences are real while its evidence is not.

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

The core law is:

> **Durable commitment establishes the fact. Authoritative memory may project that fact afterward; it must not outrun it.**

Do not confuse this with “every byte must hit spinning disk before memory changes.” The relevant durability boundary is whatever the recovery protocol treats as committed: transactional database commit, fsynced WAL, replicated quorum, append acknowledged by the durable journal, etc. A write-ahead log can legitimately be the durable authority even if final materialization happens later.

Also do not fire for private speculative state that cannot escape. Computing a candidate aggregate in memory before commit is fine if nobody can observe it, no effect depends on it, and it is discarded on commit failure. The smell begins when speculative memory acquires authoritative consequences.

Nearby rules:

- `blob-after-event` — durable event references content that was not durably present first;
- `snapshot-as-truth` — a derived snapshot is treated as canonical history;
- `overwrite-history` — already committed past facts are mutated;
- `partial-write-assumption` — interrupted persistence is assumed all-or-nothing;
- `unverified-completion-claim` — prose overclaims evidence; here the runtime itself overclaims durable reality.

A decisive crash test is to stop execution at every boundary between “memory changed” and “durable fact committed.” Ask what externally visible behavior could already have happened. Then restart from durable state. If recovery cannot reconstruct the state that those effects assumed, memory had authority it had not earned.

The right implementation shape is usually:

1. derive the intended transition without mutating shared authority;
2. commit the durable fact atomically;
3. fold/apply the committed fact to authoritative memory;
4. only then expose success or launch consequences that rely on the new state.

If step 3 fails after commit, recovery can rebuild. If step 2 fails, the command did not happen. That asymmetry is the point.

> Memory is allowed to be fast. It is not allowed to testify about a future that durable history has not yet admitted.
