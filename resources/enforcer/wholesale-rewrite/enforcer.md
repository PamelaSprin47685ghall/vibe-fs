# wholesale-rewrite — Enforcer

## Definition
A wholesale rewrite replaces a broad region of known-working structure when the required change can be established through a smaller transformation that preserves verified behavior and local knowledge.

## Governing Principle
Existing code contains more information than its visible design: bug fixes, operational constraints, edge cases, and compatibility facts accumulated through history. A rewrite discards that information wholesale and asks tests to rediscover every relevant constraint at once. Precision is therefore an epistemic strategy: preserve what is already proven and invalidate only the smallest assumptions the new requirement makes false.

## Trigger When
Trigger when large delete-and-recreate, generated replacement, or broad rearchitecture is chosen for a task whose acceptance criteria affect a materially smaller surface.

## Do Not Trigger When
- An explicitly authorized greenfield replacement is the task.
- A documented architecture decision establishes that the old structure itself is the defect and incremental preservation would perpetuate it.
- Generated artifacts are regenerated from an updated source of truth without discarding hand-proven logic.
- The required change invalidates the module’s core invariants, so a local rewrite of that module is the smallest correct move.

## Distinguish From
`scope-creep` expands the intent of a change. `half-finished-refactor` fails to complete a necessary migration. Tie-break: if the strategy is unnecessary blast radius on a smaller semantic delta, use this rule; if extra unrelated intent was added to the same change, use `scope-creep`.

## Decision Procedure
Identify the smallest set of existing invariants the new requirement invalidates. Preserve everything else mechanically and change only the ownership/representation that must become different.

## Examples
- positive: a one-field validation change rewrites an entire service “while we are here.”
- near-miss: an ADR authorizes replacing a proven-unsafe persistence model, and that module is rewritten.
- counterexample: a ticket grows to include unrelated cleanup and features — that is `scope-creep`.

## Nudge
Known-good structure is evidence, not clutter. Rewrite only when the structure itself is what must change; otherwise preserve proofs and make the smallest transformation that establishes the new contract.
