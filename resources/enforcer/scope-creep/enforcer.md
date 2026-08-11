# scope-creep — Enforcer

## Definition
Scope creep occurs when a change begins solving problems whose correction is not required by the stated outcome, governing invariant, or necessary consequences of the chosen design.

## Governing Principle
A change is a proof with a boundary: it claims that a finite set of edits establishes a finite result. Unrelated cleanup enlarges both the proposition and the search space, making review less able to distinguish necessary consequences from opportunistic preference. Restraint is therefore not conservatism; it preserves causal attribution between intent and modification.

## Trigger When
Trigger when implementation expands into unrelated redesign, cleanup, migrations, dependency changes, or behavior merely because the surrounding code is imperfect.

## Do Not Trigger When
Do not trigger when adjacent changes are logically required to satisfy the acceptance criteria, preserve compilation after a necessary API change, or restore an invariant the requested change directly touches.

## Distinguish From
wholesale-rewrite chooses an unnecessarily broad replacement strategy. half-finished-refactor stops a required migration midway. This rule concerns work whose intent itself has expanded beyond justification.

## Decision Procedure
For each proposed edit ask which acceptance criterion or necessary invariant consequence requires it. If no direct chain exists, separate the work into another change.

## Nudge
Keep one change answerable to one justified intent. Separate attractive but independent improvements so necessity, review, and rollback remain legible.
