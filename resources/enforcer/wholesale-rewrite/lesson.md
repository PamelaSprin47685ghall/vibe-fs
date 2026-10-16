# wholesale-rewrite — Lesson

## Opinion

### Definition

A wholesale rewrite replaces a broad region of known-working structure when the required change can be established through a smaller transformation that preserves verified behavior and local knowledge.

### Governing Principle

Existing code contains more information than its visible design: bug fixes, operational constraints, edge cases, and compatibility facts accumulated through history. The root-cause is discarding proven structure for a smaller semantic delta. A rewrite throws away that information wholesale and asks tests to rediscover every relevant constraint at once. Precision is therefore an epistemic strategy: preserve what is already proven and invalidate only the smallest assumptions the new requirement makes false.

### Trigger When

Trigger when large delete-and-recreate, generated replacement, or broad rearchitecture is chosen for a task whose acceptance criteria affect a materially smaller surface.

### Do Not Trigger When

- An explicitly authorized greenfield replacement is the task.
- A documented architecture decision establishes that the old structure itself is the defect and incremental preservation would perpetuate it.
- Generated artifacts are regenerated from an updated source of truth without discarding hand-proven logic.
- The required change invalidates the module’s core invariants, so a local rewrite of that module is the smallest correct move.

### Distinguish From

`scope-creep` expands the intent of a change. `half-finished-refactor` fails to complete a necessary migration. Tie-break: if the strategy is unnecessary blast radius on a smaller semantic delta, use this rule; if extra unrelated intent was added to the same change, use `scope-creep`.

### Decision Procedure

Identify the smallest set of existing invariants the new requirement invalidates. Preserve everything else mechanically and change only the ownership/representation that must become different.

### Examples

- positive: a one-field validation change rewrites an entire service “while we are here.”
- near-miss: an ADR authorizes replacing a proven-unsafe persistence model, and that module is rewritten.
- counterexample: a ticket grows to include unrelated cleanup and features — that is `scope-creep`.

## Advice

### What To Do Now

Reduce the change to the smallest structurally correct transformation that satisfies the new contract while preserving known-good code and verified behavior outside that boundary. Limit blast radius: who owns the invalidated invariants owns the rewrite surface.

### Why This Matters

A rewrite expands uncertainty faster than it expands value. Every replaced line reopens assumptions that previous production history may already have settled, so the verification burden grows from “prove this new behavior” toward “re-prove the subsystem.”

### Repair Strategy

Map the required semantic delta, retain unaffected owners and paths, migrate only the structures whose invariants truly changed, and use tests to protect preserved behavior during the transformation.

### Decision Branches

If the requirement invalidates only a small set of invariants, preserve the rest and transform that set.
If a recorded decision says the structure itself is the defect, rewrite that structure and still keep unrelated proven code.

### Common Wrong Fixes

- Equate fewer old lines with cleaner architecture.
- Copy behavior into a new package while leaving the old one half-alive.
- Rewrite tests from scratch so preserved behavior is no longer pinned.

### Verification

Invariant: the diff is a bounded semantic transformation of invalidated assumptions only. Unchanged behavior should remain covered without recreating it from scratch.

### Done When

The solution changes exactly the assumptions the requirement invalidated and leaves the rest of the system’s accumulated evidence intact.

### Nudge

Known-good structure is evidence, not clutter. Rewrite only when the structure itself is what must change; otherwise preserve proofs and make the smallest transformation that establishes the new contract.
