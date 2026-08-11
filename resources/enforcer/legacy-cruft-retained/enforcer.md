# legacy-cruft-retained — Enforcer

## Definition
Legacy cruft is retained when obsolete aliases, names, formats, branches, or code paths survive despite a deliberate clean-break decision that removed any compatibility obligation.

## Governing Principle
A clean break is valuable because it collapses the state space: after the boundary, the old world is no longer a world the system must interpret. Retaining legacy paths nullifies that benefit. The project pays both histories forever while pretending the migration is complete, and future developers cannot know whether the old path is forbidden, tolerated, or secretly required.

## Trigger When
Trigger when an explicit clean-break policy exists yet obsolete code, aliases, compatibility branches, old names, or legacy formats remain reachable.

## Do Not Trigger When
Do not trigger when a current external contract explicitly requires the old surface for a bounded migration period.

## Distinguish From
compatibility-cruft lacks a justified external requirement. half-finished-refactor leaves migration incomplete. This rule is sharper: a decision already said the old world should cease to exist.

## Decision Procedure
Find the clean-break decision and identify every remaining representation of the retired surface. If no authorized exception exists, remove all of them rather than re-litigating the decision in code.

## Nudge
A clean break should reduce the number of supported worlds to one. Honor the decision: remove the legacy surface instead of carrying a ghost contract forward.
