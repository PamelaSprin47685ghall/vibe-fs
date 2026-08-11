# scope-creep — Enforcer

## Definition
Scope creep occurs when a change begins solving problems whose correction is not required by the stated outcome, governing invariant, or necessary consequences of the chosen design. The root-cause is that extra intent is attached to a finite proof, so necessity and opportunistic preference share one diff and causal attribution is lost.

## Governing Principle
A change is a proof with a boundary: it claims that a finite set of edits establishes a finite result. Unrelated cleanup enlarges both the proposition and the search space, making review less able to distinguish necessary consequences from preference. Restraint is therefore not conservatism; it preserves the chain from intent to modification.

## Trigger When
Trigger when implementation expands into unrelated redesign, cleanup, migrations, dependency changes, or behavior merely because the surrounding code is imperfect.

## Do Not Trigger When
- Adjacent changes are logically required to satisfy the acceptance criteria.
- An API change forces compilation or call-site updates that preserve behavior.
- The requested change directly disturbs an invariant that must be restored in the same delivery.
- Extra files are generated artifacts of the required edit, not independent redesign.

## Distinguish From
`wholesale-rewrite` chooses an unnecessarily broad replacement strategy for the same intent. `half-finished-refactor` stops a required migration midway. `big-batch-intent` fuses independent success conditions into one instruction before execution. This rule concerns work whose intent itself has expanded beyond justification. Tie-break: if extra unrelated intent is attached to this change, this rule owns the case.

## Decision Procedure
1. Name the stated outcome and the invariants it necessarily disturbs.
2. For each proposed edit, ask which acceptance criterion or necessary restore requires it.
3. If no direct chain exists, separate that work into another change.
4. Keep only transitive edits that compilation or the disturbed invariant demands.

## Examples
- positive: a bugfix for one parser also reformats an unrelated module and upgrades a dependency “while here.”
- near-miss: renaming a function requires updating every call site in the same change so the tree still compiles.
- counterexample: every material file in the diff maps to the stated acceptance criteria or a necessary invariant restore.

## Nudge
Keep one change answerable to one justified intent. Separate attractive but independent improvements so necessity, review, and rollback remain legible.
