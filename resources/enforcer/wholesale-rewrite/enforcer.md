# wholesale-rewrite — Enforcer

## Definition
A wholesale rewrite replaces a broad region of known-working structure when the required change can be established through a smaller transformation that preserves verified behavior and local knowledge.

## Governing Principle
Existing code contains more information than its visible design: bug fixes, operational constraints, edge cases, and compatibility facts accumulated through history. A rewrite discards that information wholesale and asks tests to rediscover every relevant constraint at once. Precision is therefore an epistemic strategy: preserve what is already proven and invalidate only the smallest assumptions the new requirement makes false.

## Trigger When
Trigger when large delete-and-recreate, generated replacement, or broad rearchitecture is chosen for a task whose acceptance criteria affect a materially smaller surface.

## Do Not Trigger When
Do not trigger for an explicitly authorized greenfield replacement, or when a documented architecture decision establishes that the old structure itself is the defect and incremental preservation would perpetuate it.

## Distinguish From
scope-creep expands the intent of a change. half-finished-refactor fails to complete a necessary migration. This rule concerns choosing unnecessary blast radius as the implementation strategy.

## Decision Procedure
Identify the smallest set of existing invariants the new requirement invalidates. Preserve everything else mechanically and change only the ownership/representation that must become different.

## Nudge
Known-good structure is evidence, not clutter. Rewrite only when the structure itself is what must change; otherwise preserve proofs and make the smallest transformation that establishes the new contract.
