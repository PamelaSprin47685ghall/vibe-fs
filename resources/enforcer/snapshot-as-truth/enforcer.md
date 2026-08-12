# snapshot-as-truth — Enforcer

A snapshot becomes dangerous when a representation created **from** history is later allowed to overrule the history that created it.

Snapshots, checkpoints, materialized views, caches, summaries, indexes, and projections are useful precisely because they forget information. They compress a longer derivation into a cheaper present. That lossiness is not a flaw when the source facts remain authoritative. It becomes a flaw when the compression is promoted into testimony.

The central question is provenance:

> If this snapshot disagrees with its source, which one has the right to call the other wrong?

If the answer is “the snapshot, because it is newer/faster/easier to load,” the optimization has become a competing source of truth.

Fire this rule when:

- recovery loads a checkpoint even when its digest/version/source position cannot be proven against the underlying fact stream;
- a materialized read model is edited directly and later used to reconstruct supposedly authoritative history;
- “current state” is copied from a cache while the fact log says something else;
- snapshot timestamps or file modification times are used as freshness proof instead of source identity;
- corruption in a projection becomes indistinguishable from a legitimate state transition;
- deleting a snapshot would lose semantic information that supposedly exists in a stronger history elsewhere.

Do not fire merely because a system has no event log. A database row can legitimately be the system of record. A materialized view can legitimately be authoritative if that is the actual contract. In that case, stop pretending there is a stronger hidden history behind it.

Also do not fire when snapshots are disposable acceleration: they carry enough provenance to prove which source prefix they represent, are rejected on mismatch, and can be rebuilt without semantic loss.

Nearby rules:

- `duplicated-truth` — two writable owners both claim authority over the same fact;
- `recovery-by-filesystem-state` — incidental path residue is used as lifecycle truth;
- `overwrite-history` — committed historical facts are mutated;
- `memory-before-disk` — volatile state outruns durable commitment.

Use this rule when the sharp defect is: **a derivative representation has been granted authority over the source it should only summarize.**

A decisive test is deletion. Remove every snapshot/checkpoint/cache and rebuild from the purported source facts. If semantic information disappears, either the source was never authoritative or the snapshot has quietly become a second owner. Both deserve an explicit architectural decision.

The repair is to make provenance mechanical. Record source position, event count, version, digest, schema, or other identity that proves exactly what fact prefix the snapshot represents. On mismatch, discard/rebuild. Never “repair” the source from the projection unless the projection is explicitly the true system of record.

> A snapshot is allowed to make history cheaper to read. It is not allowed to make history answerable to the shortcut.
