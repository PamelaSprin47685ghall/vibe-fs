# snapshot-as-truth — Lesson

## Opinion

### Definition

A snapshot becomes dangerous when a representation created **from** history is later allowed to overrule the history that created it.

Snapshots, checkpoints, materialized views, caches, summaries, indexes, and projections are useful precisely because they forget information. They compress a longer derivation into a cheaper present. That lossiness is not a flaw when the source facts remain authoritative. It becomes a flaw when the compression is promoted into testimony.

The central question is provenance:

> If this snapshot disagrees with its source, which one has the right to call the other wrong?

If the answer is "the snapshot, because it is newer/faster/easier to load," the optimization has become a competing source of truth.

### Governing Principle

A decisive test is deletion. Remove every snapshot/checkpoint/cache and rebuild from the purported source facts. If semantic information disappears, either the source was never authoritative or the snapshot has quietly become a second owner. Both deserve an explicit architectural decision.

The repair is to make provenance mechanical. Record source position, event count, version, digest, schema, or other identity that proves exactly what fact prefix the snapshot represents. On mismatch, discard/rebuild. Never "repair" the source from the projection unless the projection is explicitly the true system of record.

### Trigger When

Fire this rule when:

- recovery loads a checkpoint even when its digest/version/source position cannot be proven against the underlying fact stream;
- a materialized read model is edited directly and later used to reconstruct supposedly authoritative history;
- "current state" is copied from a cache while the fact log says something else;
- snapshot timestamps or file modification times are used as freshness proof instead of source identity;
- corruption in a projection becomes indistinguishable from a legitimate state transition;
- deleting a snapshot would lose semantic information that supposedly exists in a stronger history elsewhere.

### Do Not Trigger When

Do not fire merely because a system has no event log. A database row can legitimately be the system of record. A materialized view can legitimately be authoritative if that is the actual contract. In that case, stop pretending there is a stronger hidden history behind it.

Also do not fire when snapshots are disposable acceleration: they carry enough provenance to prove which source prefix they represent, are rejected on mismatch, and can be rebuilt without semantic loss.

### Distinguish From

Nearby rules:

- `duplicated-truth` — two writable owners both claim authority over the same fact;
- `recovery-by-filesystem-state` — incidental path residue is used as lifecycle truth;
- `overwrite-history` — committed historical facts are mutated;
- `memory-before-disk` — volatile state outruns durable commitment.

### Decision Procedure

Use this rule when the sharp defect is: **a derivative representation has been granted authority over the source it should only summarize.**

### Examples

A materialized read model is edited directly and later used to reconstruct supposedly authoritative history. "Current state" is copied from a cache while the fact log says something else. Snapshot timestamps are used as freshness proof instead of source identity.

## Advice

### What To Do Now

Restore a single provenance direction.

If source facts are authoritative, the snapshot must be rebuildable from them, must carry enough identity to prove which source state it represents, and must lose every dispute against that source.

A useful invariant is:

> **Deleting every snapshot may cost time; it must not cost truth.**

Make that invariant executable.

### Why This Matters

Store provenance beside the snapshot: source offset/count, version, tree/hash digest, schema version, generation, or whatever identity the source boundary can actually prove. Do not use file mtime, process start time, "newer-looking" timestamps, or naming convention as substitutes for source identity.

### Repair Strategy

At load/recovery:

1. verify the snapshot format/schema;
2. verify its provenance against the authoritative source;
3. if either check fails, reject it;
4. replay/rebuild from a trusted point;
5. optionally write a new snapshot **after** the rebuilt state is established.

Do not reconstruct the source history from a derived snapshot merely because replay is expensive. That reverses evidence direction. If replay cost is unacceptable, improve snapshot frequency/indexing/compaction while preserving the source's authority, or deliberately redesign which store is authoritative.

### Decision Branches

If the so-called snapshot is actually the system of record, say so and simplify the architecture. Do not maintain a ceremonial event log that nobody trusts on recovery while a checkpoint silently owns truth. Two sources with ambiguous precedence are worse than one honest source.

### Common Wrong Fixes

Common fake repairs:

- choose whichever file has the latest timestamp;
- keep a corrupt/mismatched snapshot because "replay would take too long";
- add a second snapshot and vote between projections without consulting source facts;
- write corrections directly into a materialized view, then backfill the event log from it;
- compare only length/count without a digest/version capable of detecting substituted content;
- treat successful deserialization as proof the snapshot belongs to this history;
- retain old and new snapshot formats with different precedence rules and no one-way migration contract.

### Verification

Verification should include hostile provenance cases, not just happy rebuild:

- stale snapshot over newer source;
- snapshot from another session/account/tree with plausible shape;
- corrupt bytes;
- valid bytes with wrong source digest;
- missing snapshot;
- snapshot at source prefix N followed by replay N+1...M;
- schema migration.

Every case must converge to the state obtained from the authoritative source alone.

Also test that a rebuilt snapshot cannot change earlier source facts. Snapshot creation is an optimization side effect, not a new semantic event unless the domain explicitly says otherwise.

### Done When

You are done when source-of-truth direction can be drawn as one arrow:

```text
authoritative facts → current state → snapshot
```

Never:

```text
snapshot ↔ facts   // whichever looks newer wins
```

> A cache may be disposable. Authority is not.

### Nudge

> A snapshot is allowed to make history cheaper to read. It is not allowed to make history answerable to the shortcut.
