# duplicated-truth — Lesson

## Opinion

### Definition

Truth is duplicated when the same fact has more than one representation that may be independently written and each is treated as authoritative. The root-cause is that one present fact has more than one writable authority.

### Governing Principle

A fact can have many projections but only one authority. Once two writable representations claim equal status, disagreement becomes a legal system state and every read must answer a new question: which copy wins? Synchronization code cannot eliminate this problem; it merely defines increasingly elaborate rituals for repairing the contradiction after the model allowed it.

### Trigger When

Trigger when one fact is stored, configured, cached, or encoded in multiple independently mutable places with no strict derivation relation.

### Do Not Trigger When

- Secondary representations are read-only projections that can be rebuilt deterministically from a declared source of truth.
- A cache is explicitly disposable and always derived; a miss never invents a competing write.
- Display copies, logs, or API views cannot write back into the fact.
- Two different facts happen to share a similar shape; they are not one truth.

### Distinguish From

`overwrite-history` changes prior facts. `snapshot-as-truth` promotes a projection to authority. This rule concerns simultaneous authorities for one present fact. Tie-break: if two writable copies of the same present fact can disagree, this rule owns the case.

### Decision Procedure

For any disagreement, ask which representation the system is obligated to believe. If the answer is ambiguous or context-dependent, choose one authority and make the others derived.

### Examples

- positive: user email is writable in both the profile table and a billing config blob, and either may be treated as current.
- near-miss: a read-only search index rebuilt from the profile table, never written independently.
- counterexample: keep one writable source and derive every other representation from it.

## Advice

### What To Do Now

Declare one canonical writable representation for the fact and turn every other copy into a deterministic projection, cache, or view of that source. The declared source is who owns the fact; caches and views are not who owns writes.

### Why This Matters

When two places can both define the same fact, inconsistency is no longer a bug in synchronization—it is a state the architecture permits. Every repair mechanism then needs precedence rules, timestamps, reconciliation, or manual judgment because the model refused to answer where truth lives.

### Repair Strategy

Trace all writers first. Remove duplicate write authority, then rebuild secondary forms from the owner through explicit projection or synchronization that has one direction. Preserve cache invalidation as an implementation concern, never as a second source of truth.

### Decision Branches

- If two stores can both write the fact, pick one authority and make the other a derived projection.
- If a cache can be rebuilt, keep it read-only and disposable; never let a cache miss invent a write.
- If a snapshot is being treated as live truth, that is `snapshot-as-truth`; demote the snapshot rather than adding a tie-breaker.

### Common Wrong Fixes

- Do not add “last write wins” merely to settle conflicts you could make impossible.
- Do not call one copy “primary” while allowing the other to overwrite it.
- Do not add bidirectional sync “to keep them equal.”
- Do not paper over disagreement with timestamps or operator dashboards.

### Verification

Create conflicting hypothetical values. The architecture should make one of those writes impossible or clearly subordinate, not require a runtime tie-breaker. The invariant is that every fact has one answer to “where is this defined?”

### Done When

Every fact has one answer to “where is this defined?”, while all other representations are visibly derivative and disposable.

### Nudge

Many views are fine; many authorities are not. Choose one writable truth and derive every other representation from it.
