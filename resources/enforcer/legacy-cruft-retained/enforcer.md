# legacy-cruft-retained — Enforcer

## Definition
Legacy cruft is retained when obsolete aliases, names, formats, branches, or code paths survive despite a deliberate clean-break decision that removed any compatibility obligation.

## Governing Principle
A clean break is valuable because it collapses the state space: after the boundary, the old world is no longer a world the system must interpret. Retaining legacy paths nullifies that benefit. The project pays both histories forever while pretending the migration is complete, and future developers cannot know whether the old path is forbidden, tolerated, or secretly required.

## Trigger When
Trigger when an explicit clean-break policy exists yet obsolete code, aliases, compatibility branches, old names, or legacy formats remain reachable.

## Do Not Trigger When
- Do not trigger when a current external contract explicitly requires the old surface for a bounded migration period.
- Do not trigger when the remaining old name appears only in changelog or docs describing the break.
- Do not trigger for on-disk residue that current readers reject (unreadable leftover, not a live path).

## Distinguish From
compatibility-cruft lacks a justified external requirement. half-finished-refactor leaves migration incomplete. This rule is sharper: a decision already said the old world should cease to exist. Tie-break: if no clean break was decided and dual owners remain, use half-finished-refactor; if the break was decided and the old surface still lives, use this rule.

## Decision Procedure
Find the clean-break decision and identify every remaining representation of the retired surface. If no authorized exception exists, remove all of them rather than re-litigating the decision in code.

## Examples
- positive: A v1 parser and alias remain reachable after the project declared v2 the only supported format.
- near-miss: An external client still needs v1 until a dated retirement; that adapter is the only remaining old surface.
- counterexample: The old surface is deleted; git history remembers it.

## Nudge
A clean break should reduce the number of supported worlds to one. Honor the decision: remove the legacy surface instead of carrying a ghost contract forward.
