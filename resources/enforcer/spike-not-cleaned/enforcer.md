# spike-not-cleaned — Enforcer

## Definition
A spike is uncleaned when experimental code built to discover feasibility is promoted into the production path without replacing assumptions, shortcuts, and missing contracts that were acceptable only while learning.

## Governing Principle
A prototype optimizes for epistemic speed: it asks “can this work?” Production optimizes for durable correctness: “under which contracts will this keep working?” Confusing those objectives promotes evidence-gathering scaffolds into architecture. Hard-coded inputs, skipped failure semantics, global state, and hand-waved ownership then acquire permanence merely because the experiment succeeded.

## Trigger When
Trigger when proof-of-concept code becomes shipping implementation while retaining experimental shortcuts, implicit assumptions, fake boundaries, or unhandled lifecycle/failure cases.

## Do Not Trigger When
Do not trigger when the spike remains isolated from production or has been deliberately rebuilt/refactored until production contracts are explicit and verified.

## Distinguish From
leftover-scaffolding concerns temporary support artifacts. dirty-hack is a local workaround. This rule is the promotion of an epistemic prototype into the system’s enduring design.

## Decision Procedure
List every assumption the spike was allowed to make because it was temporary. Before promotion, either turn each into an explicit contract with proof or remove the assumption through redesign.

## Nudge
A successful experiment answers feasibility, not maintainability. Rebuild the discovered idea around production invariants before letting prototype shortcuts become architecture.
