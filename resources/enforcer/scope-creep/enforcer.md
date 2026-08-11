# scope-creep — Enforcer

## Definition
Scope creep occurs when a change begins solving problems whose correction is not required by the stated outcome, governing invariant, or necessary consequences of the chosen design.

## Governing Principle
A change is a proof with a boundary: it claims that a finite set of edits establishes a finite result. Unrelated cleanup enlarges both the proposition and the search space, making review less able to distinguish necessary consequences from opportunistic preference. Restraint is therefore not conservatism; it preserves causal attribution between intent and modification.

## Trigger When
Trigger when implementation expands into unrelated redesign, cleanup, migrations, dependency changes, or behavior merely because the surrounding code is imperfect.

## Do Not Trigger When
- Adjacent changes are logically required to satisfy the acceptance criteria.
- An API change forces compilation/call-site updates that preserve behavior.
- The requested change directly disturbs an invariant that must be restored in the same delivery.
- The extra files are generated artifacts of the required edit, not independent redesign.

## Distinguish From
wholesale-rewrite chooses an unnecessarily broad replacement strategy. half-finished-refactor stops a required migration midway. This rule concerns work whose intent itself has expanded beyond justification. Tie-break: fire here when extra intent is attached to this change; fire wholesale-rewrite when the chosen strategy replaces more than needed to meet the same intent; fire half-finished-refactor when a required migration was started and then abandoned.

## Decision Procedure
For each proposed edit ask which acceptance criterion or necessary invariant consequence requires it. If no direct chain exists, separate the work into another change.

## Examples
- positive: a bugfix for one parser also reformats an unrelated module and upgrades a dependency “while here.”
- near-miss: renaming a function requires updating every call site in the same change so the tree still compiles.
- counterexample: the diff’s every file maps to the stated acceptance criteria or a necessary invariant restore.

## Nudge
Keep one change answerable to one justified intent. Separate attractive but independent improvements so necessity, review, and rollback remain legible.
