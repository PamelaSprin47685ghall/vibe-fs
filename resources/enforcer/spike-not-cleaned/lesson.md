# spike-not-cleaned — Lesson

## Opinion

### Definition

A spike is uncleaned when experimental code built to discover feasibility is promoted into the production path without replacing assumptions, shortcuts, and missing contracts that were acceptable only while learning. The root-cause is that an epistemic prototype is treated as architecture, so temporary assumptions acquire permanence merely because the experiment succeeded.

### Governing Principle

A prototype optimizes for epistemic speed: it asks “can this work?” Production optimizes for durable correctness: “under which contracts will this keep working?” Confusing those objectives promotes evidence-gathering scaffolds into design. Hard-coded inputs, skipped failure semantics, global state, and hand-waved ownership then survive as if they had been chosen.

### Trigger When

Trigger when proof-of-concept code becomes shipping implementation while retaining experimental shortcuts, implicit assumptions, fake boundaries, or unhandled lifecycle or failure cases.

### Do Not Trigger When

- The spike remains isolated from production (branch, sandbox, docs-only experiment).
- The idea has been deliberately rebuilt until production contracts are explicit and verified.
- The remaining code is the smallest production design, and exploratory files are deleted.
- A checked-in spike is clearly labeled non-shipping and is not on the release path.

### Distinguish From

`leftover-scaffolding` concerns temporary support artifacts beside a real implementation. `dirty-hack` is a local workaround that was never a learning spike. `half-finished-refactor` leaves dual architecture after a required migration. This rule is the promotion of an epistemic prototype into the system’s enduring design. Tie-break: if prototype assumptions ship as architecture, this rule owns the case.

### Decision Procedure

1. List every assumption the spike was allowed to make because it was temporary.
2. Before promotion, turn each into an explicit contract with proof, or remove it through redesign.
3. Rebuild the smallest production design around real boundaries.
4. Delete the spike when its knowledge has transferred.

### Examples

- positive: a hardcoded in-memory spike is wired to production HTTP and still skips failure, ownership, and recovery.
- near-miss: a spike directory exists but is not referenced by the shipping binary.
- counterexample: the experiment’s idea is rewritten behind real boundaries, and the prototype files are deleted.

## Advice

### What To Do Now

Treat the spike as evidence, not as the final implementation. Preserve what it proved, then replace experimental shortcuts with explicit contracts, owned state, failure semantics, and production verification. Production contracts are who owns shipped structure; the spike owns only the knowledge it discovered.

### Why This Matters

Prototype code is often correct only inside the narrow experiment that gave it meaning. Shipping it unchanged silently extends that local success into claims about concurrency, recovery, security, malformed inputs, and maintenance that were never tested.

### Repair Strategy

Extract the essential idea, discard hard-coded and exploratory structure, then rebuild the smallest production design around the real boundaries and invariants. Delete the spike when its knowledge has transferred.

### Decision Branches

- If the spike is already on the production path, rebuild around contracts and delete experimental shortcuts.
- If the spike is isolated, keep it off the release path or delete it after the lesson is recorded.
- If a wrapper merely calls the spike, replace the wrapper and spike pair with one production implementation.

### Common Wrong Fixes

- Do not keep both “prototype” and “production wrapper” paths where the wrapper merely calls the spike.
- Do not rename the spike module to `prod` without changing assumptions.
- Do not add a few tests of the demo happy path and declare it production-ready.
- Do not leave hard-coded credentials, global state, or skipped failures “until later.”

### Verification

Exercise the production boundary, failure paths, and lifecycle rather than only the successful demo scenario that motivated the spike. The invariant is that shipped structure follows production contracts; the prototype contributes knowledge, not scaffolding.

### Done When

The shipped code owes its structure to production contracts, while the prototype’s only surviving contribution is the knowledge it discovered.

### Nudge

A successful experiment answers feasibility, not maintainability. Rebuild the discovered idea around production invariants before letting prototype shortcuts become architecture.
