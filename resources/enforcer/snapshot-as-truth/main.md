# snapshot-as-truth — Main

Restore a single provenance direction.

If source facts are authoritative, the snapshot must be rebuildable from them, must carry enough identity to prove which source state it represents, and must lose every dispute against that source.

A useful invariant is:

> **Deleting every snapshot may cost time; it must not cost truth.**

Make that invariant executable.

Store provenance beside the snapshot: source offset/count, version, tree/hash digest, schema version, generation, or whatever identity the source boundary can actually prove. Do not use file mtime, process start time, “newer-looking” timestamps, or naming convention as substitutes for source identity.

At load/recovery:

1. verify the snapshot format/schema;
2. verify its provenance against the authoritative source;
3. if either check fails, reject it;
4. replay/rebuild from a trusted point;
5. optionally write a new snapshot **after** the rebuilt state is established.

Do not reconstruct the source history from a derived snapshot merely because replay is expensive. That reverses evidence direction. If replay cost is unacceptable, improve snapshot frequency/indexing/compaction while preserving the source's authority, or deliberately redesign which store is authoritative.

If the so-called snapshot is actually the system of record, say so and simplify the architecture. Do not maintain a ceremonial event log that nobody trusts on recovery while a checkpoint silently owns truth. Two sources with ambiguous precedence are worse than one honest source.

Common fake repairs:

- choose whichever file has the latest timestamp;
- keep a corrupt/mismatched snapshot because “replay would take too long”;
- add a second snapshot and vote between projections without consulting source facts;
- write corrections directly into a materialized view, then backfill the event log from it;
- compare only length/count without a digest/version capable of detecting substituted content;
- treat successful deserialization as proof the snapshot belongs to this history;
- retain old and new snapshot formats with different precedence rules and no one-way migration contract.

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

You are done when source-of-truth direction can be drawn as one arrow:

```text
authoritative facts → current state → snapshot
```

Never:

```text
snapshot ↔ facts   // whichever looks newer wins
```

> A cache may be disposable. Authority is not.